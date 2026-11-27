export const origamiDefaults={theme:'red',animate:true,duration:650,blink:true,showDate:true,transport:'compatible'};
export const origamiThemes:any={
 red:{front:'#ef0014',light:'#ff2434',back:'#b80012',fold:'#87000d',bg:'#f5f2ed',text:'#7d7470'},
 gold:{front:'#d9a72e',light:'#ffd66b',back:'#8d5b12',fold:'#563507',bg:'#11100e',text:'#d8c9a3'},
 ice:{front:'#28b9e7',light:'#83e8ff',back:'#12699b',fold:'#0b4268',bg:'#071522',text:'#8eb9cf'}
};
export const origamiDigits=(d=new Date())=>String(d.getHours()).padStart(2,'0')+String(d.getMinutes()).padStart(2,'0');
const durationOf=(s:any)=>Math.min(1400,Math.max(250,Number(s.duration)||650));

export class OrigamiState{
 from='0000';to='0000';started=0;demoUntil=0;revision=0;token:any;
 update(actual:string,now:number,s:any){if(this.demoUntil&&now>=this.demoUntil)this.demoUntil=0;if(!this.demoUntil&&actual!==this.to){this.from=this.to;this.to=actual;this.started=now;this.revision++;}}
 demo(now:number,s:any){this.from=this.to;this.to=this.to.split('').map((v,i)=>String((Number(v)+[1,3,2,4][i])%10)).join('');this.started=now;this.demoUntil=now+durationOf(s)+800;this.revision++;}
 progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.started)/durationOf(s)));}
}

// Seven shared ribbon positions: top, upper-right, lower-right, bottom,
// lower-left, upper-left and middle. Chamfered ends create the folded-paper form.
const MAP:any={0:[0,1,2,3,4,5],1:[1,2],2:[0,1,6,4,3],3:[0,1,6,2,3],4:[5,6,1,2],5:[0,5,6,2,3],6:[0,5,6,4,2,3],7:[0,1,2],8:[0,1,2,3,4,5,6],9:[0,1,2,3,5,6]};
const SEG=[
 [[30,8],[132,8],[152,28],[132,48],[30,48],[10,28]],
 [[136,32],[156,12],[176,32],[176,132],[156,152],[136,132]],
 [[136,148],[156,128],[176,148],[176,248],[156,268],[136,248]],
 [[30,252],[132,252],[152,272],[132,292],[30,292],[10,272]],
 [[6,148],[26,128],[46,148],[46,248],[26,268],[6,248]],
 [[6,32],[26,12],[46,32],[46,132],[26,152],[6,132]],
 [[30,130],[132,130],[152,150],[132,170],[30,170],[10,150]]
];
function polygon(c:CanvasRenderingContext2D,p:number[][]){c.beginPath();c.moveTo(p[0][0],p[0][1]);for(let i=1;i<p.length;i++)c.lineTo(p[i][0],p[i][1]);c.closePath();}
function drawSegment(c:CanvasRenderingContext2D,index:number,color:any,alpha:number){
 const p=SEG[index];c.save();c.globalAlpha=Math.max(0,Math.min(1,alpha));polygon(c,p);const vertical=index===1||index===2||index===4||index===5,g=c.createLinearGradient(vertical?0:10,vertical?10:0,vertical?176:152,vertical?270:0);g.addColorStop(0,color.back);g.addColorStop(.28,color.front);g.addColorStop(.7,color.front);g.addColorStop(1,color.light);c.fillStyle=g;c.fill();
 // Dark triangular reverse side at alternating ribbon folds.
 c.beginPath();if(vertical){c.moveTo(p[0][0],p[0][1]);c.lineTo(p[1][0],p[1][1]);c.lineTo(p[0][0],p[0][1]+38);}else{c.moveTo(p[0][0],p[0][1]);c.lineTo(p[5][0],p[5][1]);c.lineTo(p[0][0]+38,p[0][1]);}c.closePath();c.fillStyle=color.fold;c.globalAlpha*=.54;c.fill();
 c.restore();
}
export function drawOrigamiDigit(c:CanvasRenderingContext2D,oldDigit:string,newDigit:string,p:number,color:any){
 const oldSet=new Set<number>(MAP[oldDigit]||[]),newSet=new Set<number>(MAP[newDigit]||[]);
 for(let i=0;i<7;i++){
  const oldOn=oldSet.has(i),newOn=newSet.has(i);if(oldOn&&newOn){drawSegment(c,i,color,1);continue;}
  const delay=i*.045,span=.55,q=Math.max(0,Math.min(1,(p-delay)/span));if(oldOn)drawSegment(c,i,color,1-q);if(newOn)drawSegment(c,i,color,q);
 }
}
export function drawOrigamiClock(canvas:HTMLCanvasElement,from:string,to:string,p:number,raw:any,date=new Date()){
 const s={...origamiDefaults,...raw},color=origamiThemes[s.theme]||origamiThemes.red,w=Math.max(1,Math.round(Number(s.ActionGeometry?.width)||1200)),h=Math.max(1,Math.round(Number(s.ActionGeometry?.height)||420));if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;const c=canvas.getContext('2d')!;c.fillStyle=color.bg;c.fillRect(0,0,w,h);
 const logicalW=1200,logicalH=420,k=Math.min(w/logicalW,h/logicalH);c.save();c.translate((w-logicalW*k)/2,(h-logicalH*k)/2);c.scale(k,k);
 const xs=[124,334,656,866],y=42;c.shadowColor='rgba(70,0,0,.16)';c.shadowBlur=9;c.shadowOffsetY=6;
 for(let i=0;i<4;i++){c.save();c.translate(xs[i],y);drawOrigamiDigit(c,from[i]||to[i],to[i],p,color);c.restore();}
 c.shadowColor='transparent';c.fillStyle=color.front;c.globalAlpha=s.blink===false||date.getMilliseconds()<500?1:.18;for(const cy of [155,225]){c.save();c.translate(583,cy);c.rotate(Math.PI/4);c.fillRect(-10,-10,20,20);c.restore();}c.globalAlpha=1;
 if(s.showDate!==false){const week=['SUN','MON','TUE','WED','THU','FRI','SAT'][date.getDay()],month=['JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'][date.getMonth()];c.fillStyle=color.text;c.textAlign='center';c.font='500 18px "Segoe UI",sans-serif';c.fillText(week+'  ·  '+month+' '+String(date.getDate()).padStart(2,'0'),600,386);}
 c.restore();return canvas;
}
