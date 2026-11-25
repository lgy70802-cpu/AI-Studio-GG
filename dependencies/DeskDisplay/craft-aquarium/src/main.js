import { Aquarium } from './Aquarium.js';
import { CraftTarget } from './CraftTarget.js';
import { log } from './assetLoader.js';
let preview, craft, generation = 0, previewTimer;
window.connectSDSocket = function(port, uuid, registerEvent, info, actionInfo) {
  clearTimeout(previewTimer);
  generation++; preview?.dispose(); craft?.dispose();
  document.getElementById('startup-error').hidden = true;
  document.querySelector('canvas').style.display = 'none';
  if (!actionInfo) craft = new CraftTarget(port, uuid, registerEvent);
  // V1 deliberately has no property-inspector controls.
  window.aquariumHost = craft;
};
window.connectSocket = window.connectElgatoStreamDeckSocket = window.connectMiraBoxSDSocket = window.connectSDSocket;
async function boot() {
  const token = generation;
  try {
    const instance = await Aquarium.create(document.querySelector('canvas'));
    if (token !== generation) { instance.dispose(); return; }
    preview = instance; window.aquarium = preview;
  } catch (error) {
    if (token !== generation) return;
    log('startup-error', { message: error.message });
    const message = document.getElementById('startup-error');
    message.textContent = '鱼缸加载失败，请重新构建后打开 preview.html。错误：' + error.message;
    message.hidden = false;
  }
}
if (document.body.dataset.target === 'preview') void boot();
else previewTimer = setTimeout(() => { if (!generation) void boot(); }, 250);
