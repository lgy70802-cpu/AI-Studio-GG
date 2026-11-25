import {ensureTimeFont} from './fonts';
export const flipDefaults={animate:true,duration:400,transport:'compatible'};
export const loadFlipFont=()=>ensureTimeFont('barlow');
export const clockDigits=(d=new Date())=>String(d.getHours()).padStart(2,'0')+String(d.getMinutes()).padStart(2,'0')+String(d.getSeconds()).padStart(2,'0');
export function durationOf(s:any){return Math.min(1200,Math.max(200,Number(s.duration)||400));}
export class MonoFlipState{
 from='';to='';started=0;demoUntil=0;demoToken:any;revision=0;
 update(actual:string,now:number,s:any){
  if(this.demoUntil&&now>=this.demoUntil)this.demoUntil=0;
  if(!this.demoUntil&&actual!==this.to){this.from=this.to||actual;this.to=actual;this.started=now;this.revision++;}
 }
 demo(now:number,s:any){this.from=this.to;this.to=String((Number(this.to)+111111)%240000).padStart(6,'0');this.started=now;this.demoUntil=now+durationOf(s)+700;this.revision++;}
 progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.started)/durationOf(s)));}
}
export const colonIsLit=(timestamp=Date.now())=>timestamp%1000<500;
const W=220,H=320;
function round(c:CanvasRenderingContext2D,x:number,y:number,w:number,h:number,r:number){c.beginPath();c.roundRect(x,y,w,h,r);}
export class MonoFlipRenderer{
 canvas=document.createElement('canvas');tiles=new Map<string,HTMLCanvasElement>();
 tile(digit:string){
  if(this.tiles.has(digit))return this.tiles.get(digit)!;
  const a=document.createElement('canvas');a.width=W;a.height=H;const c=a.getContext('2d')!;
  const g=c.createLinearGradient(0,0,0,H);g.addColorStop(0,'#303030');g.addColorStop(.49,'#202020');g.addColorStop(.51,'#292929');g.addColorStop(1,'#202020');
  round(c,0,0,W,H,20);c.fillStyle=g;c.fill();c.save();c.clip();
  c.strokeStyle='rgba(80,65,40,.045)';c.lineWidth=.6;
  for(let y=2;y<H;y+=3){c.beginPath();c.moveTo(0,y);c.lineTo(W,y);c.stroke();}
  for(let x=2;x<W;x+=4){c.beginPath();c.moveTo(x,0);c.lineTo(x,H);c.stroke();}
  c.fillStyle='#eeeeee';c.font='800 286px ClockBarlow';c.textAlign='center';c.textBaseline='alphabetic';
  const m=c.measureText(digit);const top=m.actualBoundingBoxAscent, bottom=m.actualBoundingBoxDescent;
  c.translate(W/2,0);c.scale(.64,1);c.fillText(digit,0,(H+top-bottom)/2);
  c.restore();this.tiles.set(digit,a);return a;
 }
 draw(from:string,to:string,p:number,width:number,height:number,showDate=true){
  const a=this.canvas;if(a.width!==width)a.width=width;if(a.height!==height)a.height=height;
  const c=a.getContext('2d')!;c.fillStyle='#050605';c.fillRect(0,0,width,height);
  const k=Math.min(width/1500,height/560);c.save();c.translate((width-1500*k)/2,(height-560*k)/2);c.scale(k,k);
  const xs=[28,268,518,758,1008,1248], y=112;
  for(let i=0;i<6;i++){
   const x=xs[i],old=this.tile(from[i]||to[i]),next=this.tile(to[i]);c.save();c.translate(x,y);
   c.shadowColor='#000';c.shadowBlur=15;round(c,-7,-7,W+14,H+14,26);c.fillStyle='#191919';c.fill();c.shadowBlur=0;
   c.strokeStyle='#292929';c.lineWidth=2;c.stroke();
   const changed=from[i]!==to[i]&&p<1;
   if(!changed)c.drawImage(next,0,0);
   else{
    c.drawImage(next,0,0,W,H/2,0,0,W,H/2);c.drawImage(old,0,H/2,W,H/2,0,H/2,W,H/2);
    const first=p<.5, factor=first?Math.cos(p*Math.PI):Math.sin((p-.5)*Math.PI);
    const piece=first?old:next,sy=first?0:H/2,dy=first?H/2-H/2*factor:H/2,dh=H/2*factor;
    if(dh>.1){c.drawImage(piece,0,sy,W,H/2,0,dy,W,dh);c.fillStyle='rgba(0,0,0,'+(.32*(1-factor))+')';c.fillRect(0,dy,W,dh);}
   }
   c.fillStyle='#080a08';c.fillRect(0,H/2-2,W,4);c.fillStyle='rgba(255,255,255,.3)';c.fillRect(0,H/2+2,W,1);
   for(const hx of [-10,W+3]){const g=c.createLinearGradient(hx,0,hx+8,0);g.addColorStop(0,'#101210');g.addColorStop(.5,'#8f9083');g.addColorStop(1,'#181a17');c.fillStyle=g;round(c,hx,H/2-23,8,46,3);c.fill();}
   c.restore();
  }
  c.fillStyle='#777';c.textAlign='center';c.textBaseline='middle';c.font='18px "Segoe UI",sans-serif';['HOUR','MIN','SEC'].forEach((label,i)=>c.fillText(label,258+i*490,476));
  if(showDate){const d=new Date();c.fillStyle='#aaa';c.font='18px "Segoe UI",sans-serif';c.fillText(d.getFullYear()+' . '+String(d.getMonth()+1).padStart(2,'0')+' . '+String(d.getDate()).padStart(2,'0')+'   '+['SUN','MON','TUE','WED','THU','FRI','SAT'][d.getDay()],750,52);}
  c.restore();return a;
 }
}
