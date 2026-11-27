import { Aquarium } from './Aquarium.js';
import { aquariumConfig } from './aquariumConfig.js';
import { loadAssets, log } from './assetLoader.js';
import { CraftClock } from './CraftClock.js';
export class CraftTarget {
  constructor(port, uuid, registerEvent) {
    this.port = port; this.uuid = uuid; this.registerEvent = registerEvent;
    this.instances = new Map(); this.closed = false;
    this.assets = loadAssets(aquariumConfig); this.assets.catch(error => log('asset-error', { message: error.message }));
    this.encoder = new TextEncoder();
    this.clock = new CraftClock(now => {
      for (const entry of this.instances.values()) if (entry.active) entry.aquarium?.frame(now);
    }, aquariumConfig.maxFPS);
    this.connect();
    this.unload = () => this.dispose(); window.addEventListener('pagehide', this.unload);
  }
  connect() {
    if (this.closed) return;
    const socket = this.socket = new WebSocket(`ws://127.0.0.1:${this.port}`);
    socket.onopen = () => socket.send(JSON.stringify({ event: this.registerEvent, uuid: this.uuid }));
    socket.onmessage = event => {
      try { this.handle(JSON.parse(event.data)); } catch (error) { log('host-message-error', { message: error.message }); }
    };
    socket.onerror = () => socket.close();
    socket.onclose = () => {
      for (const context of this.instances.keys()) this.remove(context);
      if (!this.closed) this.retry = setTimeout(() => this.connect(), 1500);
    };
  }
  handle(message) {
    const { event, context, payload, action } = message;
    if (action && action !== 'com.mirabox.streamdock.aquarium.action1') return;
    if (event === 'willDisappear') this.remove(context);
    if (event === 'willAppear') void this.start(context, payload?.settings || {});
    if (event === 'didReceiveSettings' && this.instances.has(context)) {
      const entry = this.instances.get(context); entry.settings = payload?.settings || {};
      this.resize(entry);
    }
  }
  resize(entry) {
    const geometry = entry.settings.ActionGeometry || {};
    const size = (v, fallback) => Number.isFinite(Number(v)) && Number(v) > 0 ? Math.min(8192, Number(v)) : fallback;
    entry.aquarium?.resize(size(geometry.width, aquariumConfig.designWidth), size(geometry.height, aquariumConfig.designHeight));
  }
  async start(context, settings) {
    this.remove(context);
    const entry = { settings, active: true, busy: false, lastSent: -Infinity, aquarium: null };
    this.instances.set(context, entry);
    try {
      const assets = await this.assets;
      if (!entry.active || this.closed) return;
      const canvas = document.createElement('canvas');
      // The exact Scene/Renderer used by preview also drives the external target.
      entry.aquarium = await Aquarium.create(canvas, {}, { assets, observeResize: false, externalClock: true,
        onFrame: (frame, now) => this.sendFrame(context, entry, frame, now) });
      if (!entry.active || this.closed) { entry.aquarium.dispose(); return; }
      this.resize(entry);
      this.sendFrame(context, entry, canvas, performance.now());
      this.clock.start();
      log('craft-output-started', { context, hidden: document.hidden, transport: entry.aquarium.config.outputTransport });
    } catch (error) { log('host-start-error', { context, message: error.message }); this.remove(context); }
  }
  sendFrame(context, entry, canvas, now) {
    const config = entry.aquarium.config, socket = this.socket;
    if (!entry.active || entry.busy || socket.readyState !== WebSocket.OPEN || socket.bufferedAmount > 1024 * 1024 || now - entry.lastSent < 1000 / config.outputFPS - 0.5) return;
    entry.busy = true; entry.lastSent = now;
    if (config.outputTransport !== 'binary') {
      try {
        const image = canvas.toDataURL(config.outputFormat, config.outputQuality);
        socket.send(JSON.stringify({ event: 'setImage', context, payload: { target: 0, image } }));
      } catch (error) { log('encode-error', { message: error.message }); }
      finally { entry.busy = false; }
      return;
    }
    // At most one encoder task per context. Never enqueue stale frames.
    try { canvas.toBlob(async blob => {
      try {
        if (!blob || !entry.active || this.closed || socket !== this.socket || socket.readyState !== WebSocket.OPEN) return;
        const bytes = await blob.arrayBuffer();
        if (!entry.active || this.closed || socket.readyState !== WebSocket.OPEN || socket.bufferedAmount > 1024 * 1024) return;
        const header = this.encoder.encode(JSON.stringify({ event: 'setImage', context, payload: { type: blob.type } }));
        const packet = new Uint8Array(4 + header.length + bytes.byteLength);
        new DataView(packet.buffer).setUint32(0, header.length, false);
        packet.set(header, 4); packet.set(new Uint8Array(bytes), 4 + header.length); socket.send(packet.buffer);
      } catch (error) { log('frame-error', { message: error.message }); }
      finally { entry.busy = false; }
    }, config.outputFormat, config.outputQuality); }
    catch (error) { entry.busy = false; log('encode-error', { message: error.message }); }
  }
  remove(context) {
    const entry = this.instances.get(context);
    if (entry) { entry.active = false; entry.aquarium?.dispose(); this.instances.delete(context); }
    if (!this.instances.size) this.clock.stop();
  }
  dispose() {
    if (this.closed) return;
    this.closed = true; clearTimeout(this.retry);
    for (const context of this.instances.keys()) this.remove(context);
    this.clock.dispose();
    this.socket.onclose = this.socket.onmessage = this.socket.onerror = null; this.socket.close();
    window.removeEventListener('pagehide', this.unload);
  }
}
