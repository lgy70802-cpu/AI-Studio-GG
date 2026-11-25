import {ensureTimeFont} from './fonts';
export const loadCalendarFlipFont=()=>ensureTimeFont('barlow');
export const calendarFlipDefaults={animate:true,duration:440,hour12:false,labels:'en',wood:'maple',transport:'compatible'};
export const calendarValues=(d=new Date(),hour12=false)=>{let h=d.getHours();if(hour12)h=h%12||12;return [String(d.getMonth()+1).padStart(2,'0'),String(d.getDate()).padStart(2,'0'),String(h).padStart(2,'0'),String(d.getMinutes()).padStart(2,'0'),String(d.getSeconds()).padStart(2,'0')];};
const durationOf=(s:any)=>Math.min(1200,Math.max(220,Number(s.duration)||440));
export class CalendarFlipState{from=['01','01','00','00','00'];to=['01','01','00','00','00'];started=0;revision=0;update(values:string[],now:number){if(values.some((v,i)=>v!==this.to[i])){this.from=[...this.to];this.to=[...values];this.started=now;this.revision++;}}progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.started)/durationOf(s)));}}
const CW=236,CH=260;
function rounded(c:CanvasRenderingContext2D,x:number,y:number,w:number,h:number,r:number){c.beginPath();c.roundRect(x,y,w,h,r);}
function circle(c:CanvasRenderingContext2D,x:number,y:number,r:number){c.beginPath();c.arc(x,y,r,0,Math.PI*2);}
const styles:any={ivory:{top:'#f4ecdd',bottom:'#e8ddca',text:'#3e3128',edge:'#c8b493',label:'#786957'},dark:{top:'#3e3d3a',bottom:'#292927',text:'#f1eadb',edge:'#151513',label:'#c7bba5'},gold:{top:'#f4d58d',bottom:'#e2b95f',text:'#513517',edge:'#b68a39',label:'#846539'}};
export class CalendarFlipRenderer{
 canvas=document.createElement('canvas');tiles=new Map<string,HTMLCanvasElement>();
 tile(value:string,kind:string){const key=kind+value;if(this.tiles.has(key))return this.tiles.get(key)!;const s=styles[kind],a=document.createElement('canvas');a.width=CW;a.height=CH;const c=a.getContext('2d')!,g=c.createLinearGradient(0,0,0,CH);g.addColorStop(0,s.top);g.addColorStop(.49,s.top);g.addColorStop(.51,s.bottom);g.addColorStop(1,s.bottom);rounded(c,0,0,CW,CH,14);c.fillStyle=g;c.fill();c.save();c.clip();c.globalAlpha=kind==='dark'?.035:.075;c.strokeStyle=kind==='dark'?'#fff':'#765c3e';c.lineWidth=.6;for(let y=2;y<CH;y+=3){c.beginPath();c.moveTo(0,y);c.lineTo(CW,y);c.stroke();}c.globalAlpha=1;c.fillStyle=s.text;c.font='800 158px ClockBarlow';c.textAlign='center';c.textBaseline='middle';c.fillText(value,CW/2,CH/2+6);c.restore();this.tiles.set(key,a);return a;}
 draw(valuesFrom:string[],valuesTo:string[],p:number,raw:any){const s={...calendarFlipDefaults,...raw},w=Math.max(1,Math.round(Number(s.ActionGeometry?.width)||1920)),h=Math.max(1,Math.round(Number(s.ActionGeometry?.height)||462));if(this.canvas.width!==w)this.canvas.width=w;if(this.canvas.height!==h)this.canvas.height=h;const c=this.canvas.getContext('2d')!,LW=1920,LH=462,k=Math.min(w/LW,h/LH);c.fillStyle='#d8b57e';c.fillRect(0,0,w,h);c.save();c.translate((w-LW*k)/2,(h-LH*k)/2);c.scale(k,k);
  // Procedural maple/walnut grain, so the background remains sharp at every size.
  const wood=s.wood==='walnut'?['#5f3b28','#815536','#2f2018']:['#e8c58d','#f5d7a3','#a97945'],bg=c.createLinearGradient(0,0,LW,LH);bg.addColorStop(0,wood[0]);bg.addColorStop(.48,wood[1]);bg.addColorStop(1,wood[0]);c.fillStyle=bg;c.fillRect(0,0,LW,LH);c.globalAlpha=s.wood==='walnut'?.18:.13;c.strokeStyle=wood[2];c.lineWidth=1;for(let y=7;y<LH;y+=8){c.beginPath();for(let x=0;x<=LW;x+=18){const yy=y+Math.sin(x*.018+y*.031)*2.2+Math.sin(x*.006)*1.5;x?c.lineTo(x,yy):c.moveTo(x,yy);}c.stroke();}for(const [x,y] of [[145,92],[1510,340],[520,46],[300,374]]){c.beginPath();c.ellipse(x,y,38,9,.2,0,Math.PI*2);c.stroke();c.beginPath();c.ellipse(x,y,18,4,.2,0,Math.PI*2);c.stroke();}c.globalAlpha=1;
  const xs=[220,510,895,1185,1490],kinds=['ivory','ivory','dark','dark','gold'],labels=s.labels==='zh'?['月份','日期','小时','分钟','秒钟']:['MONTH','DAY','HOURS','MINUTES','SECONDS'],y=121;
  // Date/time separator.
  const sep=c.createLinearGradient(0,74,0,390);sep.addColorStop(0,'rgba(120,72,28,0)');sep.addColorStop(.15,'#b17b42');sep.addColorStop(.5,'#f5d39a');sep.addColorStop(.85,'#a86f34');sep.addColorStop(1,'rgba(120,72,28,0)');c.fillStyle=sep;c.fillRect(829,66,3,316);
  for(let i=0;i<5;i++){const x=xs[i],kind=kinds[i],st=styles[kind],old=this.tile(valuesFrom[i]||valuesTo[i],kind),next=this.tile(valuesTo[i],kind);c.save();c.translate(x,y);
   c.shadowColor='rgba(50,25,8,.42)';c.shadowBlur=14;c.shadowOffsetY=8;rounded(c,-9,-61,CW+18,CH+74,18);c.fillStyle=kind==='dark'?'#343330':kind==='gold'?'#efd18d':'#efe5d4';c.fill();c.shadowColor='transparent';c.strokeStyle=st.edge;c.lineWidth=2;c.stroke();
   c.fillStyle=st.label;c.textAlign='center';c.textBaseline='middle';c.font='700 15px ClockBarlow,"Segoe UI",sans-serif';c.fillText(labels[i],CW/2,-31);
   // Corner screws.
   for(const sx of [12,CW-12]){const sg=c.createRadialGradient(sx-3,-48,1,sx,-45,10);sg.addColorStop(0,'#fff4c5');sg.addColorStop(.42,'#a78143');sg.addColorStop(1,'#3d2a14');circle(c,sx,-45,9);c.fillStyle=sg;c.fill();c.save();c.translate(sx,-45);c.rotate(-.65);c.fillStyle='rgba(40,25,12,.8)';c.fillRect(-6,-1,12,2);c.restore();}
   const changed=valuesFrom[i]!==valuesTo[i]&&p<1;if(!changed)c.drawImage(next,0,0);else{c.drawImage(next,0,0,CW,CH/2,0,0,CW,CH/2);c.drawImage(old,0,CH/2,CW,CH/2,0,CH/2,CW,CH/2);const first=p<.5,factor=first?Math.cos(p*Math.PI):Math.sin((p-.5)*Math.PI),piece=first?old:next,sy=first?0:CH/2,dy=first?CH/2-CH/2*factor:CH/2,dh=CH/2*factor;if(dh>.2){c.drawImage(piece,0,sy,CW,CH/2,0,dy,CW,dh);c.fillStyle='rgba(0,0,0,'+(.38*(1-factor))+')';c.fillRect(0,dy,CW,dh);}}
   c.fillStyle='rgba(50,35,22,.72)';c.fillRect(0,CH/2-1.5,CW,3);c.fillStyle='rgba(255,255,255,.28)';c.fillRect(0,CH/2+2,CW,1);
   for(const hx of [-14,CW+4]){const hg=c.createLinearGradient(hx,0,hx+10,0);hg.addColorStop(0,'#49331d');hg.addColorStop(.45,'#e2bd7e');hg.addColorStop(.7,'#765029');hg.addColorStop(1,'#352312');rounded(c,hx,CH/2-23,10,46,3);c.fillStyle=hg;c.fill();}
   c.restore();
  }
  // Brass colon pairs.
  for(const x of [1160,1458])for(const yy of [192,268]){const dot=c.createRadialGradient(x-3,yy-4,1,x,yy,9);dot.addColorStop(0,'#fff1a4');dot.addColorStop(.42,'#c58a2c');dot.addColorStop(1,'#5a3510');circle(c,x,yy,8);c.fillStyle=dot;c.shadowColor='#8b5418';c.shadowBlur=7;c.fill();c.shadowBlur=0;}
  c.restore();return this.canvas;
 }
}
