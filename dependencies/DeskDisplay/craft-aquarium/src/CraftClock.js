// Craft renders into an offscreen web host: its visibility is not display visibility.
// Acknowledged ticks keep at most one worker message outstanding.
export class CraftClock {
  constructor(tick, fps) {
    const source = `let timer, running = false, pending = false, delay = 33, sent = 0;
      function pulse() { if (!running || pending) return; pending = true; sent = performance.now(); postMessage('tick'); }
      onmessage = ({data}) => {
        if (data.event === 'start') { clearTimeout(timer); delay = data.delay; running = true; pending = false; pulse(); }
        else if (data.event === 'stop') { running = false; pending = false; clearTimeout(timer); }
        else if (data.event === 'ack' && running && pending) {
          pending = false; timer = setTimeout(pulse, Math.max(0, delay - (performance.now() - sent)));
        }
      };`;
    this.url = URL.createObjectURL(new Blob([source], { type: 'text/javascript' }));
    this.worker = new Worker(this.url); this.active = false; this.fps = fps;
    this.worker.onmessage = () => {
      if (!this.active) return;
      try { tick(performance.now()); }
      finally { if (this.active) this.worker.postMessage({ event: 'ack' }); }
    };
  }
  start() { if (!this.active) { this.active = true; this.worker.postMessage({ event: 'start', delay: 1000 / this.fps }); } }
  stop() { this.active = false; this.worker.postMessage({ event: 'stop' }); }
  dispose() { this.active = false; this.worker.onmessage = null; this.worker.terminate(); URL.revokeObjectURL(this.url); }
}
