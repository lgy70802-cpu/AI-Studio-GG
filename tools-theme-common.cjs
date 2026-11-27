const {chromium}=require('playwright');
exports.connect=async(port=9227)=>{
 const browser=await chromium.connectOverCDP('http://127.0.0.1:'+port),page=browser.contexts()[0].pages().find(p=>p.url().includes('studio.local')&&!p.url().includes('device-render'));
 const req=(command,payload={})=>page.evaluate(({command,payload})=>new Promise((resolve,reject)=>{const id=crypto.randomUUID(),timer=setTimeout(()=>{window.chrome.webview.removeEventListener('message',fn);reject(Error('timeout '+command));},60000),fn=e=>{if(e.data.id!==id||e.data.progress||e.data.updateProgress)return;clearTimeout(timer);window.chrome.webview.removeEventListener('message',fn);e.data.error?reject(Error(e.data.error)):resolve(e.data.result);};window.chrome.webview.addEventListener('message',fn);window.chrome.webview.postMessage({id,command,...payload});}),{command,payload});
 return {browser,page,req};
};
