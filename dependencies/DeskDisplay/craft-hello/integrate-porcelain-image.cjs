const fs=require('fs');let p='src/porcelain.ts',s=fs.readFileSync(p,'utf8');s=`import dialSource from './porcelain-image';
const dialImage=new Image();let dialReady=false;let dialPromise:Promise<void>|undefined;
export function loadPorcelainDial(){return dialPromise??=new Promise<void>(resolve=>{const finish=(ok:boolean)=>{clearTimeout(timer);dialReady=ok;dialImage.onload=null;dialImage.onerror=null;if(!ok)console.error(JSON.stringify({event:'porcelain_image_failed',fallback:true}));resolve();};const timer=setTimeout(()=>finish(false),10000);dialImage.onload=()=>finish(true);dialImage.onerror=()=>finish(false);dialImage.src=dialSource;});}
export const dialGeometry={centerX:497,centerY:503,outerRadius:327};
`+s;
s=s.replace(" c.shadowColor='rgba(0,0,0,.14)'",` c.textBaseline='middle';
 if(dialReady){
  const scale=(r+21)/dialGeometry.outerRadius;
  c.drawImage(dialImage,cx-dialGeometry.centerX*scale,cy-dialGeometry.centerY*scale,dialImage.naturalWidth*scale,dialImage.naturalHeight*scale);
 }else{
 c.shadowColor='rgba(0,0,0,.14)'`);s=s.replace(' const angles=handAngles',' }\n const angles=handAngles');fs.writeFileSync(p,s);
p='src/plugin/actions/porcelainclock.ts';s=fs.readFileSync(p,'utf8').replace('drawPorcelain,porcelainKey','drawPorcelain,porcelainKey,loadPorcelainDial');s=s.replace("states.set(context,{canvas:document.createElement('canvas'),key:''});draw(context,true);","states.set(context,{canvas:document.createElement('canvas'),key:''});const instance=states.get(context);loadPorcelainDial().then(()=>{if(states.get(context)===instance)draw(context,true);});draw(context,true);");fs.writeFileSync(p,s);
p='porcelain-preview.html';s=fs.readFileSync(p,'utf8').replace('drawPorcelain,porcelainKey','drawPorcelain,porcelainKey,loadPorcelainDial').replace("let last='';","await loadPorcelainDial();let last='';");fs.writeFileSync(p,s);