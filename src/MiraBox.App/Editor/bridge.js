// The final prototype editor is hosted inside the native workspace.
window.hostInitialize = function (payload) {
  const storage = window.localStorage;
  const draftKey = 'd92-te-draft-1';
  const storageKey = 'editor-draft-' + payload.projectId;
  const get = Storage.prototype.getItem, set = Storage.prototype.setItem, remove = Storage.prototype.removeItem;
  Storage.prototype.getItem = function (key) { return get.call(this, key === draftKey ? storageKey : key); };
  Storage.prototype.setItem = function (key, value) { return set.call(this, key === draftKey ? storageKey : key, value); };
  Storage.prototype.removeItem = function (key) { return remove.call(this, key === draftKey ? storageKey : key); };
  if (payload.draftRecovery) storage.setItem(draftKey, JSON.stringify(payload.draftRecovery));
  const capOriginal = capStart;
  capStart = async (mode, callback) => {
    try {
      const stream = await navigator.mediaDevices.getDisplayMedia({ video: { cursor: 'always' }, audio: false });
      const video = document.createElement('video');
      video.srcObject = stream; video.muted = true; await video.play();
      window.actualCaptureVideo = video;
      capOriginal(mode, callback);
      window.chrome.webview.postMessage({ action: 'captureOptions', includeApp: false });
      const includeApp = document.querySelector('#capApp');
      if (includeApp) includeApp.addEventListener('change', () => window.chrome.webview.postMessage({ action: 'captureOptions', includeApp: includeApp.checked }));
      const cursor = document.querySelector('#capCur');
      if (cursor) cursor.addEventListener('change', () => stream.getVideoTracks()[0].applyConstraints({ cursor: cursor.checked ? 'always' : 'never' }).catch(error => toast('光标选项无法应用：' + error.message)));
      const observer = new MutationObserver(() => {
        if (!document.querySelector('.cap')) { stream.getTracks().forEach(track => track.stop()); observer.disconnect(); window.chrome.webview.postMessage({ action: 'captureEnd' }); }
      });
      observer.observe(document.body, { childList: true });
    } catch (error) { toast('未开始采集：' + error.message); }
  };
  resources.splice(0, resources.length, ...payload.resources);
  devices.splice(0, devices.length, ...payload.devices);
  const style = document.createElement('style');
  style.textContent = '.sidebar,.topbar,.footerbar{display:none!important}.app{display:block!important;padding:0!important;margin:0!important}body{padding:0!important;background:transparent!important}#main{padding:8px!important;max-width:none!important}.te-wrap{grid-template-columns:auto minmax(0,1fr) 300px}.te-stage{min-height:540px}@media(max-width:1100px){.te-wrap{grid-template-columns:auto minmax(0,1fr)}.te-side.r{grid-column:1/-1}}';
  document.head.appendChild(style);
  const mediaCache = new Map();
  async function retainMedia(value) {
    if (!value || typeof value !== 'object') return;
    for (const key of Object.keys(value)) {
      const item = value[key];
      if (typeof item === 'string' && item.startsWith('blob:')) {
        if (!mediaCache.has(item)) mediaCache.set(item, fetch(item).then(r => r.blob()).then(blob => new Promise(resolve => {
          const reader = new FileReader(); reader.onload = () => resolve(reader.result); reader.readAsDataURL(blob);
        })));
        value[key] = await mediaCache.get(item);
      } else if (item && typeof item === 'object') await retainMedia(item);
    }
  }
  let sending = Promise.resolve();
  const send = action => {
    const r = getR(editorResource);
    const name = document.querySelector('.te-top .nm');
    if (name) editorName = name.value;
    const message = { action, resource: structuredClone(r), name: editorName, dirty: editorDirty,
      captures: resources.filter(item => item.cap && item.added).map(item => structuredClone(item)) };
    if (message.resource.themeData) delete message.resource.themeData.hostBackgrounds;
    message.original = structuredClone(message.resource.themeData);
    sending = sending.then(async () => {
      await retainMedia(message.resource);
      await retainMedia(message.captures);
      window.chrome.webview.postMessage(message);
    }).catch(error => toast('素材保存失败：' + error.message));
  };
  const oldGo = go;
  go = function (p) { if (p === 'creator') { send('back'); return; } oldGo(p); };
  saveEditor = () => send('save');
  const dropDraft = teDropDraft;
  teDropDraft = () => { dropDraft(); send('dropDraft'); };
  startApply = () => send('use');
  tePreview = () => send('preview');
  const paste = tePaste;
  tePaste = at => {
    const incoming = (te.clip || []).filter(element => element.kind === 'video');
    if (incoming.length > 1) { toast('一个主题最多 1 个视频'); return; }
    const theme = getR(editorResource).themeData;
    const existing = theme.bg.kind === 'video' || [theme.master, ...theme.layouts.map(layout => layout.elements || [])]
      .some(elements => elements.some(element => element.kind === 'video'));
    if (incoming.length && existing) {
      const clip = structuredClone(te.clip);
      te.vfn = () => { te.clip = clip; paste(at); };
      modal('<h2>一个主题最多放 1 个视频</h2><p>要使用复制的视频替换现有视频吗？</p><div class="row gap"><button onclick="te.vfn=null;closeModal()">取消</button><button class="primary" onclick="teVideoReplace()">替换现有视频</button></div>', true);
    } else paste(at);
  };
  window.hostSaved = (durable, savedResource, savedName, originalDocument) => {
    const current = getR(editorResource);
    const document = structuredClone(current.themeData); delete document.hostBackgrounds;
    const changed = JSON.stringify(document) !== JSON.stringify(originalDocument) || editorName !== savedName || current.type !== savedResource.type;
    const aliases = new Map();
    function compare(original, retained) {
      if (!original || !retained || typeof original !== 'object') return;
      for (const key of Object.keys(original)) {
        if (['src','url','vurl','poster'].includes(key) && typeof original[key] === 'string' && typeof retained[key] === 'string') aliases.set(original[key], retained[key]);
        else if (original[key] && typeof original[key] === 'object') compare(original[key], retained[key]);
      }
    }
    function apply(value) {
      if (!value || typeof value !== 'object') return;
      for (const key of Object.keys(value)) {
        if (['src','url','vurl','poster'].includes(key) && aliases.has(value[key])) value[key] = aliases.get(value[key]);
        else if (value[key] && typeof value[key] === 'object') apply(value[key]);
      }
    }
    compare(originalDocument, durable); apply(current.themeData);
    current.themeData.hostBackgrounds = durable.hostBackgrounds;
    editorBackup = structuredClone(savedResource); editorBackup.themeData = durable; editorBackup.name = savedName;
    editorDirty = changed;
    if (!changed) storage.removeItem(draftKey);
    render(); toast(changed ? '主题已保存，后续修改尚未保存' : '主题已保存');
  };
  window.hostBackgroundMarkup = index => {
    const resource = structuredClone(getR(editorResource));
    resource.themeData.master = [];
    resource.themeData.layouts.forEach(layout => { layout.elements = []; layout.follow = false; });
    return themeArt(resource, index, {});
  };
  document.addEventListener('input', () => { editorDirty = true; setTimeout(() => send('change'), 0); });
  let last = '';
  setInterval(() => {
    if (page !== 'editor') return;
    const value = JSON.stringify([editorName, getR(editorResource), editorDirty]);
    if (value !== last) { last = value; send('change'); }
  }, 300);
  if (payload.fresh && !payload.draftRecovery) window.__teFresh = 1;
  openEditor(1);
};
