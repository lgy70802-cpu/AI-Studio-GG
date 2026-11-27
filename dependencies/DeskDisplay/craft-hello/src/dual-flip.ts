import {ensureTimeFont} from './fonts';

export const loadDualFlipFont=()=>ensureTimeFont('manrope');
export const dualFlipDefaults={animate:true,duration:450,transport:'compatible',showDate:true,hour12:false};
export const durationOf=(s:any)=>Math.min(1200,Math.max(220,Number(s.duration)||450));

export function clockPairs(d=new Date(),hour12=false){
 let h=d.getHours();
 if(hour12)h=h%12||12;
 return [String(h).padStart(2,'0'),String(d.getMinutes()).padStart(2,'0')];
}

export class DualFlipState{
 from=['00','00'];to=['00','00'];started=0;demoUntil=0;demoToken:any;revision=0;
 update(actual:string[],now:number,s:any){
  if(this.demoUntil&&now>=this.demoUntil)this.demoUntil=0;
  if(!this.demoUntil&&(actual[0]!==this.to[0]||actual[1]!==this.to[1])){
   this.from=[...this.to];this.to=[...actual];this.started=now;this.revision++;
  }
 }
 demo(now:number,s:any){
  this.from=[...this.to];
  this.to=[String((Number(this.to[0])+1)%24).padStart(2,'0'),String((Number(this.to[1])+11)%60).padStart(2,'0')];
  this.started=now;this.demoUntil=now+durationOf(s)+700;this.revision++;
 }
 progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.started)/durationOf(s)));}
}

const CARD_W=360,CARD_H=286;
function rounded(c:CanvasRenderingContext2D,x:number,y:number,w:number,h:number,r:number){c.beginPath();c.roundRect(x,y,w,h,r);}

export class DualFlipRenderer{
 canvas=document.createElement('canvas');tiles=new Map<string,HTMLCanvasElement>();
 tile(value:string){
  if(this.tiles.has(value))return this.tiles.get(value)!;
  const a=document.createElement('canvas');a.width=CARD_W;a.height=CARD_H;const c=a.getContext('2d')!;
  const g=c.createLinearGradient(0,0,0,CARD_H);g.addColorStop(0,'#303030');g.addColorStop(.47,'#242424');g.addColorStop(.505,'#171717');g.addColorStop(.535,'#242424');g.addColorStop(1,'#1c1c1c');
  rounded(c,0,0,CARD_W,CARD_H,19);c.fillStyle=g;c.fill();c.save();c.clip();
  const shine=c.createLinearGradient(0,0,CARD_W,0);shine.addColorStop(0,'rgba(255,255,255,.035)');shine.addColorStop(.5,'rgba(255,255,255,.09)');shine.addColorStop(1,'rgba(255,255,255,.018)');c.fillStyle=shine;c.fillRect(0,0,CARD_W,CARD_H);
  c.strokeStyle='rgba(255,255,255,.018)';c.lineWidth=.7;for(let y=2;y<CARD_H;y+=3){c.beginPath();c.moveTo(0,y);c.lineTo(CARD_W,y);c.stroke();}
  c.fillStyle='#f3f3f3';c.font='300 244px ClockManrope';c.textAlign='center';c.textBaseline='middle';c.fillText(value,CARD_W/2,CARD_H/2-4);
  c.restore();this.tiles.set(value,a);return a;
 }
 draw(from:string[],to:string[],p:number,width:number,height:number,showDate=true){
  const a=this.canvas;if(a.width!==width)a.width=width;if(a.height!==height)a.height=height;const c=a.getContext('2d')!;
  c.fillStyle='#000';c.fillRect(0,0,width,height);
  const logicalW=1420,logicalH=420,k=Math.min(width/logicalW,height/logicalH);c.save();c.translate((width-logicalW*k)/2,(height-logicalH*k)/2);c.scale(k,k);
  const xs=[170,568],y=67;
  for(let i=0;i<2;i++){
   const x=xs[i],old=this.tile(from[i]||to[i]),next=this.tile(to[i]);c.save();c.translate(x,y);
   c.shadowColor='rgba(0,0,0,.9)';c.shadowBlur=24;rounded(c,-2,-2,CARD_W+4,CARD_H+4,21);c.fillStyle='#141414';c.fill();c.shadowBlur=0;c.strokeStyle='#474747';c.lineWidth=1.5;c.stroke();
   const changed=from[i]!==to[i]&&p<1;
   if(!changed)c.drawImage(next,0,0);
   else{
    c.drawImage(next,0,0,CARD_W,CARD_H/2,0,0,CARD_W,CARD_H/2);
    c.drawImage(old,0,CARD_H/2,CARD_W,CARD_H/2,0,CARD_H/2,CARD_W,CARD_H/2);
    const first=p<.5,factor=first?Math.cos(p*Math.PI):Math.sin((p-.5)*Math.PI),piece=first?old:next,sy=first?0:CARD_H/2,dy=first?CARD_H/2-CARD_H/2*factor:CARD_H/2,dh=CARD_H/2*factor;
    if(dh>.2){c.drawImage(piece,0,sy,CARD_W,CARD_H/2,0,dy,CARD_W,dh);const shade=c.createLinearGradient(0,dy,0,dy+dh);shade.addColorStop(0,'rgba(0,0,0,'+(first?.08:.34)+')');shade.addColorStop(1,'rgba(0,0,0,'+(first?.55:.04)+')');c.fillStyle=shade;c.fillRect(0,dy,CARD_W,dh);}
   }
   c.fillStyle='#090909';c.fillRect(0,CARD_H/2-2,CARD_W,4);c.fillStyle='rgba(255,255,255,.19)';c.fillRect(0,CARD_H/2+2,CARD_W,1);
   for(const hx of [-13,CARD_W+3]){const metal=c.createLinearGradient(hx,0,hx+10,0);metal.addColorStop(0,'#080808');metal.addColorStop(.35,'#656565');metal.addColorStop(.55,'#111');metal.addColorStop(1,'#030303');c.fillStyle=metal;rounded(c,hx,CARD_H/2-27,10,54,3);c.fill();c.strokeStyle='#050505';c.stroke();}
   c.restore();
  }
  if(showDate){const d=new Date(),week=['SUN','MON','TUE','WED','THU','FRI','SAT'][d.getDay()],month=['JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'][d.getMonth()];c.fillStyle='#d2d2d2';c.textAlign='left';c.textBaseline='middle';c.font='300 35px ClockManrope';c.fillText(week+', '+month+' '+String(d.getDate()).padStart(2,'0'),1006,210);}
  c.restore();return a;
 }
}
