type Point=[number,number];
export const stringDefaults={showSeconds:false,threads:true,shadow:true,blink:true,bgColor:'#9d85a5',lineColor:'#121014'};
const paths:Point[][][]=[
 [[[0,24],[24,0],[66,0],[90,24],[90,166],[66,190],[24,190],[0,166],[0,24]]],
 [[[45,0],[45,190]]],
 [[[0,24],[24,0],[66,0],[90,24],[90,62],[66,86],[24,86],[0,110],[0,166],[24,190],[66,190],[90,166]]],
 [[[0,24],[24,0],[66,0],[90,24],[90,62],[66,86],[24,86]],[[66,86],[90,110],[90,166],[66,190],[24,190],[0,166]]],
 [[[60,0],[0,86],[90,86]],[[60,0],[60,190]]],
 [[[76,0],[24,0],[0,24],[0,78],[34,88],[66,88],[90,112],[90,166],[66,190],[24,190],[0,166]]],
 [[[12,0],[12,102],[12,166],[36,190],[78,190],[100,166],[100,128],[76,102],[12,102]]],
 [[[0,62],[0,24],[24,0],[66,0],[90,24],[90,190]]],
 [[[24,86],[0,62],[0,24],[24,0],[66,0],[90,24],[90,62],[66,86],[24,86],[0,110],[0,166],[24,190],[66,190],[90,166],[90,110],[66,86]]],
 [[[90,88],[24,88],[0,64],[0,24],[24,0],[66,0],[90,24],[90,190]]]
];
export function drawStringDigit(c:CanvasRenderingContext2D,digit:string,s:any){
 const strokes=paths[Number(digit)]||paths[0],points:Point[]=[];
 for(const path of strokes)for(const point of path)if(!points.some(p=>p[0]===point[0]&&p[1]===point[1]))points.push(point);
 c.save();c.strokeStyle=s.lineColor;c.fillStyle=s.lineColor;c.lineCap='round';c.lineJoin='round';
 if(s.threads!==false){c.save();c.globalAlpha=.24;c.lineWidth=1.05;
 // Deterministic long chords: never regenerate randomness on a clock tick.
 for(let i=0;i<points.length;i++)for(let j=i+2;j<points.length;j++){
  const a=points[i],b=points[j];if(Math.hypot(a[0]-b[0],a[1]-b[1])<55||(i+j)%3===0)continue;
  c.beginPath();c.moveTo(...a);c.lineTo(...b);c.stroke();
 }c.restore();}
 if(s.shadow!==false){c.shadowColor='rgba(20,10,25,.32)';c.shadowBlur=7;c.shadowOffsetX=3;c.shadowOffsetY=6;}
 c.lineWidth=3;for(const path of strokes){c.beginPath();path.forEach((p,i)=>i?c.lineTo(...p):c.moveTo(...p));c.stroke();}
 c.shadowBlur=0;c.shadowOffsetX=0;c.shadowOffsetY=0;
 for(const p of points){c.beginPath();c.arc(p[0],p[1],3.7,0,Math.PI*2);c.fill();}c.restore();
}
export function stringKey(s:any,date=new Date()){return [date.getHours(),date.getMinutes(),s.showSeconds?date.getSeconds():'',s.blink!==false?date.getMilliseconds()<500:true].join(':');}
export function drawStringClock(canvas:HTMLCanvasElement,raw:any,date=new Date()){
 const s={...stringDefaults,...raw};const dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const w=dim(s.ActionGeometry?.width,1000),h=dim(s.ActionGeometry?.height,320);if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.fillStyle=s.bgColor;c.fillRect(0,0,w,h);
 const groups=[date.getHours(),date.getMinutes(),...(s.showSeconds?[date.getSeconds()]:[])].map(n=>String(n).padStart(2,'0'));
 const portrait=w/h<1,logicalW=portrait?280:groups.length*270-30+80,logicalH=portrait?groups.length*235+35:270;
 const scale=Math.min(w/logicalW,h/logicalH);c.save();c.translate((w-logicalW*scale)/2,(h-logicalH*scale)/2);c.scale(scale,scale);
 groups.forEach((group,g)=>{const x=portrait?30:40+g*270,y=portrait?30+g*235:40;
 for(let i=0;i<2;i++){c.save();c.translate(x+i*120,y);drawStringDigit(c,group[i],s);c.restore();}
 if(!portrait&&g<groups.length-1){c.save();c.fillStyle=s.lineColor;c.globalAlpha=s.blink===false||date.getMilliseconds()<500?1:.16;for(const dy of [70,120]){c.beginPath();c.arc(x+240,y+dy,4,0,Math.PI*2);c.fill();}c.restore();}
 });c.restore();
}
