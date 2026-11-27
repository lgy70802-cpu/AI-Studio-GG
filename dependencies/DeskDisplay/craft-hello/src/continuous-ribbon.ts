import {drawOrigamiDigit,origamiThemes} from './origami-clock';
export const continuousDefaults={theme:'red',animate:true,duration:1100,blink:true,showDate:true,breath:true,transport:'compatible'};
export const continuousThemes:any={red:{a:'#ff2639',b:'#eb0018',edge:'#8d0010',fold:'#77000d',bg:'#f5f2ed',text:'#766e69'},gold:{a:'#ffe07a',b:'#c58a1d',edge:'#704209',fold:'#4a2b06',bg:'#0f0e0c',text:'#d8c89e'},ice:{a:'#8feaff',b:'#18aee3',edge:'#075f91',fold:'#073d61',bg:'#061725',text:'#8fc5dc'}};
export const continuousDigits=(d=new Date())=>String(d.getHours()).padStart(2,'0')+String(d.getMinutes()).padStart(2,'0');
const durationOf=(s:any)=>Math.min(1800,Math.max(300,Number(s.duration)||900));

export class ContinuousRibbonState{
 from='0000';to='0000';started=0;demoUntil=0;revision=0;token:any;
 update(actual:string,now:number,s:any){if(this.demoUntil&&now>=this.demoUntil)this.demoUntil=0;if(!this.demoUntil&&actual!==this.to){this.from=this.to;this.to=actual;this.started=now;this.revision++;}}
 demo(now:number,s:any){this.from=this.to;this.to=this.to.split('').map((v,i)=>String((Number(v)+[2,4,3,5][i])%10)).join('');this.started=now;this.demoUntil=now+durationOf(s)+900;this.revision++;}
 progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.started)/durationOf(s)));}
}

type P=[number,number];
// Every glyph is one unbroken route, like a strip of paper laid on a table.
const ROUTES:Record<string,P[]>={
 '0':[[.28,.10],[.72,.10],[.86,.24],[.86,.76],[.72,.90],[.28,.90],[.14,.76],[.14,.24],[.28,.10]],
 '1':[[.25,.28],[.52,.10],[.52,.90],[.28,.90],[.78,.90]],
 '2':[[.16,.25],[.30,.10],[.72,.10],[.86,.24],[.82,.39],[.18,.82],[.28,.90],[.86,.90]],
 '3':[[.16,.18],[.30,.10],[.72,.10],[.86,.24],[.72,.48],[.45,.50],[.72,.52],[.86,.76],[.72,.90],[.27,.90],[.14,.80]],
 '4':[[.74,.90],[.74,.10],[.18,.63],[.14,.72],[.87,.72]],
 '5':[[.84,.10],[.25,.10],[.16,.47],[.68,.47],[.86,.62],[.82,.80],[.70,.90],[.25,.90],[.14,.78]],
 '6':[[.80,.16],[.67,.10],[.30,.10],[.14,.34],[.14,.74],[.29,.90],[.70,.90],[.86,.74],[.82,.58],[.69,.48],[.20,.48]],
 '7':[[.14,.10],[.86,.10],[.62,.36],[.40,.90]],
 '8':[[.28,.49],[.15,.34],[.18,.20],[.30,.10],[.70,.10],[.84,.24],[.82,.36],[.70,.49],[.30,.49],[.16,.63],[.17,.77],[.30,.90],[.70,.90],[.84,.76],[.82,.62],[.70,.49],[.28,.49]],
 '9':[[.20,.83],[.32,.90],[.70,.90],[.86,.67],[.86,.26],[.72,.10],[.29,.10],[.14,.25],[.18,.41],[.31,.51],[.80,.51]]
};
function sample(route:P[],count=84){const lengths:number[]=[0];let total=0;for(let i=1;i<route.length;i++){total+=Math.hypot(route[i][0]-route[i-1][0],route[i][1]-route[i-1][1]);lengths.push(total);}const out:P[]=[];for(let n=0;n<count;n++){const d=total*n/(count-1);let i=1;while(i<lengths.length-1&&lengths[i]<d)i++;const span=lengths[i]-lengths[i-1]||1,q=(d-lengths[i-1])/span,a=route[i-1],b=route[i];out.push([a[0]+(b[0]-a[0])*q,a[1]+(b[1]-a[1])*q]);}return out;}
const SAMPLES=Object.fromEntries(Object.entries(ROUTES).map(([k,v])=>[k,sample(v)])) as Record<string,P[]>;
const ease=(v:number)=>v<.5?4*v*v*v:1-Math.pow(-2*v+2,3)/2;
function morph(a:string,b:string,p:number){const aa=SAMPLES[a]||SAMPLES['0'],bb=SAMPLES[b]||SAMPLES['0'],q=ease(p);return aa.map((v,i)=>[v[0]+(bb[i][0]-v[0])*q,v[1]+(bb[i][1]-v[1])*q] as P);}
function path(c:CanvasRenderingContext2D,pts:P[],x:number,y:number,w:number,h:number){c.beginPath();c.moveTo(x+pts[0][0]*w,y+pts[0][1]*h);for(let i=1;i<pts.length;i++)c.lineTo(x+pts[i][0]*w,y+pts[i][1]*h);}
function drawRibbon(c:CanvasRenderingContext2D,pts:P[],x:number,y:number,w:number,h:number,color:any,pulse:number){
 c.save();c.lineCap='butt';c.lineJoin='bevel';path(c,pts,x,y,w,h);c.strokeStyle='rgba(30,0,0,.18)';c.lineWidth=39;c.shadowColor='rgba(60,0,0,.24)';c.shadowBlur=10;c.shadowOffsetY=6;c.stroke();c.shadowColor='transparent';
 const g=c.createLinearGradient(x,y,x+w,y+h);g.addColorStop(0,color.a);g.addColorStop(.42,color.b);g.addColorStop(.72,color.a);g.addColorStop(1,color.b);path(c,pts,x,y,w,h);c.strokeStyle=g;c.lineWidth=32+pulse;c.stroke();
 path(c,pts,x,y,w,h);c.strokeStyle='rgba(255,255,255,.14)';c.lineWidth=2;c.stroke();
 // Small reverse-side diamonds appear only at pronounced bends in the live path.
 for(let i=4;i<pts.length-4;i+=4){const a=pts[i-3],m=pts[i],b=pts[i+3],cross=(m[0]-a[0])*(b[1]-m[1])-(m[1]-a[1])*(b[0]-m[0]);if(Math.abs(cross)<.006)continue;const px=x+m[0]*w,py=y+m[1]*h,ang=Math.atan2(b[1]-a[1],(b[0]-a[0])*w/h);c.save();c.translate(px,py);c.rotate(ang);c.beginPath();c.moveTo(-17,0);c.lineTo(0,cross>0?13:-13);c.lineTo(17,0);c.closePath();c.fillStyle=color.fold;c.globalAlpha=.62;c.fill();c.restore();}
 c.restore();
}
export function drawContinuousRibbon(canvas:HTMLCanvasElement,from:string,to:string,p:number,raw:any,date=new Date()){
 const s={...continuousDefaults,...raw},color=continuousThemes[s.theme]||continuousThemes.red,w=Math.max(1,Math.round(Number(s.ActionGeometry?.width)||1200)),h=Math.max(1,Math.round(Number(s.ActionGeometry?.height)||420));if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;const c=canvas.getContext('2d')!;c.fillStyle=color.bg;c.fillRect(0,0,w,h);const k=Math.min(w/1200,h/420);c.save();c.translate((w-1200*k)/2,(h-420*k)/2);c.scale(k,k);
 const xs=[105,330,650,875],pulse=s.breath===false?0:Math.sin(date.getTime()/700)*1.3,foldColor=origamiThemes[s.theme]||origamiThemes.red;
 for(let i=0;i<4;i++){
  const oldDigit=from[i]||to[i],newDigit=to[i];
  if(oldDigit===newDigit||p>=.999){c.save();c.translate(xs[i]+7,47);drawOrigamiDigit(c,newDigit,newDigit,1,foldColor);c.restore();continue;}
  // Two-stage motion: exact folded glyph opens into one strip, then folds back
  // into the exact target glyph. The path font never remains as the final face.
  const ribbonAlpha=Math.min(1,p/.24,(1-p)/.24);c.save();c.globalAlpha=Math.max(0,ribbonAlpha);drawRibbon(c,morph(oldDigit,newDigit,p),xs[i],38,190,310,color,pulse);c.restore();
  if(p<.34){c.save();c.globalAlpha=1-p/.34;c.translate(xs[i]+7,47);drawOrigamiDigit(c,oldDigit,oldDigit,1,foldColor);c.restore();}
  if(p>.66){c.save();c.globalAlpha=(p-.66)/.34;c.translate(xs[i]+7,47);drawOrigamiDigit(c,newDigit,newDigit,1,foldColor);c.restore();}
 }
 c.fillStyle=color.b;c.globalAlpha=s.blink===false||date.getMilliseconds()<500?1:.16;for(const cy of [158,228]){c.save();c.translate(600,cy);c.rotate(Math.PI/4);c.fillRect(-9,-9,18,18);c.restore();}c.globalAlpha=1;
 if(s.showDate!==false){const wd=['SUN','MON','TUE','WED','THU','FRI','SAT'][date.getDay()],mo=['JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'][date.getMonth()];c.fillStyle=color.text;c.textAlign='center';c.font='500 18px "Segoe UI",sans-serif';c.fillText(wd+'  ·  '+mo+' '+String(date.getDate()).padStart(2,'0'),600,390);}c.restore();return canvas;
}
