import { AlertTracker } from '@/alerts';
import { usePluginStore, useWatchEvent } from '@/hooks/plugin';
import { createFeed, draw, screenSeconds, type Scene, type Settings } from '@/stocks';
export default function (name: string) {
  const plugin = usePluginStore(),
    ActionID = `${window.argv[3].plugin.uuid}.${name}`,
    cleanup = new Map<string, () => void>();
  const trackers = new Map<string, AlertTracker>();
  function start(context: string) {
    cleanup.get(context)?.();
    const settings = plugin.getAction(context)?.settings as Settings;
    if (!settings) return;
    const canvas = document.createElement('canvas');
    let scene: Scene = { quotes: [], lines: {}, lineTimes: {}, error: '正在连接行情…' },
      page = 0,
      dirty = true,
      elapsed = 0;
    const render = () => {
      draw(canvas, scene, settings, page);
      plugin.getAction(context)?.setImage(canvas.toDataURL());
      dirty = false;
    };
    if (!trackers.has(context)) trackers.set(context, new AlertTracker());
    const feed = createFeed(
      settings,
      (s) => {
        scene = s;
        dirty = true;
      },
      trackers.get(context)
    );
    render();
    void feed.tick();
    plugin.Interval(ActionID + context, 1000, () => {
      void feed.tick();
      if (++elapsed >= screenSeconds(settings, page)) {
        elapsed = 0;
        page++;
        dirty = true;
      }
      // The header clock includes seconds, so repaint at 1 fps. Data polling remains independent.
      render();
    });
    cleanup.set(context, () => {
      feed.stop();
      plugin.Unterval(ActionID + context);
      cleanup.delete(context);
    });
  }
  useWatchEvent('action', {
    ActionID,
    willAppear: ({ context }) => start(context),
    didReceiveSettings: ({ context }) => start(context),
    willDisappear: ({ context }) => {
      cleanup.get(context)?.();
      trackers.delete(context);
    }
  });
  window.addEventListener('pagehide', () => {
    for (const stop of cleanup.values()) stop();
  });
}
