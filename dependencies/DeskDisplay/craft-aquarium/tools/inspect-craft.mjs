// Read-only inspection of this plugin's existing Qt remote-debug target.
const targets = await (await fetch('http://127.0.0.1:23518/json/list')).json();
for (const target of targets.filter(t => t.url.includes('com.mirabox.streamdock.aquarium.'))) {
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
  const result = new Promise(resolve => { socket.onmessage = e => { const message = JSON.parse(e.data); if (message.id === 1) resolve(message); }; });
  socket.send(JSON.stringify({ id: 1, method: 'Runtime.evaluate', params: { returnByValue: true, expression: `JSON.stringify({hidden:document.hidden,api:typeof connectSDSocket,agent:navigator.userAgent,host:window.aquariumHost?{closed:aquariumHost.closed,socket:aquariumHost.socket.readyState,clock:aquariumHost.clock?.active,instances:[...aquariumHost.instances].map(([context,e])=>({context,active:e.active,busy:e.busy,lastSent:e.lastSent,time:e.aquarium?.scene.time,running:e.aquarium?.running,size:e.aquarium?[e.aquarium.canvas.width,e.aquarium.canvas.height]:null}))}:null,error:document.getElementById('startup-error')?.textContent})` } }));
  console.log(target.id, JSON.stringify((await result).result)); socket.close();
}
