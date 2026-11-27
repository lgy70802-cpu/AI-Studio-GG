import dialSource from './porcelain-image';
const dialImage=new Image();let dialReady=false;let dialPromise:Promise<void>|undefined;
export function loadPorcelainDial(){return dialPromise??=new Promise<void>(resolve=>{const finish=(ok:boolean)=>{clearTimeout(timer);dialReady=ok;dialImage.onload=null;dialImage.onerror=null;if(!ok)console.error(JSON.stringify({event:'porcelain_image_failed',fallback:true}));resolve();};const timer=setTimeout(()=>finish(false),10000);dialImage.onload=()=>finish(true);dialImage.onerror=()=>finish(false);dialImage.src=dialSource;});}
export const dialGeometry={centerX:497,centerY:503,outerRadius:327};
export const porcelainDefaults={label:'本地时间',seconds:true,digital:true,sweep:false};
export function handAngles(date:Date,sweep=false){const seconds=date.getSeconds()+(sweep?date.getMilliseconds()/1000:0);return {hour:((date.getHours()%12)+date.getMinutes()/60+seconds/3600)*Math.PI/6,minute:(date.getMinutes()+seconds/60)*Math.PI/30,second:seconds*Math.PI/30};}
export function porcelainKey(s:any,d:Date){return [d.getHours(),d.getMinutes(),s.seconds!==false?d.getSeconds():'',s.seconds!==false&&s.sweep?Math.floor(d.getMilliseconds()/100):''].join(':');}
export function drawPorcelain(canvas:HTMLCanvasElement,raw:any,date=new Date()){
 const s={...porcelainDefaults,...raw};const dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const w=dim(s.ActionGeometry?.width,600),h=dim(s.ActionGeometry?.height,600);if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.fillStyle='#f7f7f7';c.fillRect(0,0,w,h);
 const wide=w/h>1.65,lw=wide?880:600,lh=600,k=Math.min(w/lw,h/lh);c.save();c.translate((w-lw*k)/2,(h-lh*k)/2);c.scale(k,k);
 const cx=wide?290:300,cy=wide?300:290,r=202;
 function circle(radius:number){c.beginPath();c.arc(cx,cy,radius,0,Math.PI*2);}
 c.textBaseline='middle';
 if(dialReady){
  const scale=(r+21)/dialGeometry.outerRadius;
  c.drawImage(dialImage,cx-dialGeometry.centerX*scale,cy-dialGeometry.centerY*scale,dialImage.naturalWidth*scale,dialImage.naturalHeight*scale);
 }else{
 c.shadowColor='rgba(0,0,0,.14)';c.shadowBlur=20;c.shadowOffsetY=12;circle(r+21);c.fillStyle='#fdfdfd';c.fill();c.shadowColor='rgba(255,255,255,.9)';c.shadowOffsetX=-7;c.shadowOffsetY=-7;circle(r+19);c.fill();c.shadowBlur=0;c.shadowOffsetX=c.shadowOffsetY=0;
 const face=c.createLinearGradient(0,cy-r,0,cy+r);face.addColorStop(0,'#ffffff');face.addColorStop(.65,'#f0f0f0');face.addColorStop(1,'#fbfbfb');circle(r);c.fillStyle=face;c.fill();
 c.strokeStyle='#e6e6e6';c.lineWidth=1;circle(r);c.stroke();
 for(let i=0;i<7;i++){circle(r-i);c.strokeStyle='rgba(0,0,0,'+(.028*(1-i/7))+')';c.stroke();}
 c.fillStyle='#666';c.textAlign='center';c.textBaseline='middle';c.font='600 30px "Segoe UI",sans-serif';c.fillText('12',cx,cy-r+39);c.fillText('3',cx+r-31,cy);
 }
 const angles=handAngles(date,s.sweep);
 function hand(angle:number,length:number,width:number,color:string,tail=0,shadow=true){c.save();c.translate(cx,cy);c.rotate(angle);c.lineCap='round';if(shadow){c.shadowColor='rgba(0,0,0,.25)';c.shadowBlur=8;c.shadowOffsetY=7;}c.strokeStyle=color;c.lineWidth=width;c.beginPath();c.moveTo(0,tail);c.lineTo(0,-length);c.stroke();c.restore();}
 hand(angles.hour,110,23,'#646464');hand(angles.minute,139,20,'#646464');
 circle(18);c.fillStyle='#595959';c.shadowColor='rgba(0,0,0,.3)';c.shadowBlur=7;c.shadowOffsetY=4;c.fill();c.shadowBlur=0;c.shadowOffsetY=0;
 if(s.seconds){hand(angles.second,169,2.6,'#cd5058',31,false);circle(8);c.fillStyle='#d34e56';c.fill();}
 c.textAlign=wide?'left':'center';c.fillStyle='#707070';c.font='500 24px "Microsoft YaHei",sans-serif';const textX=wide?565:300;c.fillText(String(s.label??'').slice(0,24),textX,wide?212:35,wide?280:500);
 if(s.digital){c.fillStyle='#cb5157';c.font='300 48px "Segoe UI",sans-serif';c.fillText(String(date.getHours()).padStart(2,'0')+':'+String(date.getMinutes()).padStart(2,'0'),textX,wide?295:548);c.fillStyle='#a1a1a1';c.font='16px "Microsoft YaHei",sans-serif';c.fillText((date.getMonth()+1)+'月'+date.getDate()+'日  星期'+'日一二三四五六'[date.getDay()],textX,wide?340:584);}
 c.restore();
}
