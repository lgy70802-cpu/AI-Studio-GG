import {ensureTimeFont} from './fonts';
export const loadTripleRingFont=()=>ensureTimeFont('manrope');
export const tripleRingDefaults={theme:'orange',smooth:true,showSeconds:true,showDate:true,labels:true,glow:85,transport:'compatible'};
const themes:any={orange:{accent:'#ff9418',hot:'#ffe0a1',track:'#4b2c12'},ice:{accent:'#20c8ff',hot:'#baf3ff',track:'#123d4b'},violet:{accent:'#aa6cff',hot:'#ead7ff',track:'#35234b'}};
const pad=(n:number)=>String(n).padStart(2,'0');
export function tripleRingKey(s:any,d=new Date()){return [s.smooth===false?d.getSeconds():Math.floor(d.getTime()/50),d.toDateString(),JSON.stringify(s)].join(':');}
function circle(c:CanvasRenderingContext2D,x:number,y:number,r:number){c.beginPath();c.arc(x,y,r,0,Math.PI*2);}
function polar(cx:number,cy:number,r:number,a:number){return [cx+Math.cos(a)*r,cy+Math.sin(a)*r] as const;}
function ring(c:CanvasRenderingContext2D,cx:number,cy:number,r:number,p:number,color:any,width:number,glow:number){
 c.save();c.lineCap='round';circle(c,cx,cy,r);c.strokeStyle='rgba(255,255,255,.075)';c.lineWidth=width+5;c.stroke();circle(c,cx,cy,r);c.strokeStyle=color.track;c.lineWidth=width;c.stroke();
 if(p>0){c.beginPath();c.arc(cx,cy,r,-Math.PI/2,-Math.PI/2+p*Math.PI*2);c.strokeStyle=color.accent;c.lineWidth=width;c.shadowColor=color.accent;c.shadowBlur=18*glow;c.stroke();c.beginPath();c.arc(cx,cy,r,-Math.PI/2,-Math.PI/2+p*Math.PI*2);c.strokeStyle=color.hot;c.globalAlpha=.68;c.lineWidth=2;c.shadowBlur=4;c.stroke();}
 const a=-Math.PI/2+p*Math.PI*2,[x,y]=polar(cx,cy,r,a);circle(c,x,y,width*.38);c.fillStyle=color.hot;c.shadowColor=color.accent;c.shadowBlur=15*glow;c.globalAlpha=1;c.fill();c.restore();
}
export function drawTripleRing(canvas:HTMLCanvasElement,raw:any,d=new Date()){
 const s={...tripleRingDefaults,...raw},t=themes[s.theme]||themes.orange,w=Math.max(1,Math.round(Number(s.ActionGeometry?.width)||1200)),h=Math.max(1,Math.round(Number(s.ActionGeometry?.height)||400));if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;const c=canvas.getContext('2d')!,LW=1200,LH=400,k=Math.min(w/LW,h/LH);c.fillStyle='#0c1014';c.fillRect(0,0,w,h);c.save();c.translate((w-LW*k)/2,(h-LH*k)/2);c.scale(k,k);
 const bg=c.createRadialGradient(600,188,20,600,200,700);bg.addColorStop(0,'#2c3239');bg.addColorStop(.5,'#181d22');bg.addColorStop(1,'#090c10');c.fillStyle=bg;c.fillRect(0,0,LW,LH);c.globalAlpha=.08;c.strokeStyle='#d0d5da';c.lineWidth=.5;for(let y=2;y<LH;y+=3){c.beginPath();c.moveTo(0,y);c.lineTo(LW,y);c.stroke();}c.globalAlpha=1;
 // Side rails make the circular module feel embedded in the ultra-wide display.
 for(const y of [187,213]){const g=c.createLinearGradient(0,y-4,0,y+4);g.addColorStop(0,'#555d66');g.addColorStop(.38,'#090c0f');g.addColorStop(.7,'#242a30');g.addColorStop(1,'#050708');c.fillStyle=g;c.fillRect(0,y-3,LW,6);}
 const cx=600,cy=200,glow=Math.max(20,Math.min(100,Number(s.glow)||85))/100,ms=s.smooth===false?0:d.getMilliseconds();const sec=(d.getSeconds()+ms/1000)/60,min=(d.getMinutes()+(d.getSeconds()+ms/1000)/60)/60,hour=((d.getHours()%12)+(d.getMinutes()+d.getSeconds()/60)/60)/12;
 c.save();c.shadowColor='#000';c.shadowBlur=28;c.shadowOffsetY=10;circle(c,cx,cy,190);c.fillStyle='#07090c';c.fill();c.restore();const shell=c.createRadialGradient(cx-50,cy-65,20,cx,cy,190);shell.addColorStop(0,'#47505a');shell.addColorStop(.55,'#20262c');shell.addColorStop(.9,'#090c10');shell.addColorStop(1,'#727a82');circle(c,cx,cy,187);c.fillStyle=shell;c.fill();c.strokeStyle='#9aa1a8';c.lineWidth=1;c.stroke();circle(c,cx,cy,176);c.fillStyle='#11161b';c.fill();
 // Fine outer tick scale.
 for(let i=0;i<60;i++){const a=i*Math.PI/30-Math.PI/2,[x1,y1]=polar(cx,cy,180,i%5===0?a:a),[x2,y2]=polar(cx,cy,i%5===0?170:174,a);c.beginPath();c.moveTo(x1,y1);c.lineTo(x2,y2);c.strokeStyle=i%5===0?'#7f8790':'#424950';c.lineWidth=i%5===0?1.7:.8;c.stroke();}
 ring(c,cx,cy,158,sec,t,10,glow);ring(c,cx,cy,132,min,t,11,glow);ring(c,cx,cy,105,hour,t,12,glow);
 // Inner face.
 const face=c.createRadialGradient(cx-30,cy-35,10,cx,cy,89);face.addColorStop(0,'#252b31');face.addColorStop(1,'#090c0f');circle(c,cx,cy,88);c.fillStyle=face;c.fill();c.strokeStyle='rgba(255,255,255,.18)';c.lineWidth=1;c.stroke();
 c.textAlign='center';c.textBaseline='middle';const text=s.showSeconds===false?`${pad(d.getHours())}:${pad(d.getMinutes())}`:`${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;c.fillStyle='#e1e4e7';c.font=`300 ${s.showSeconds===false?39:31}px ClockManrope,"Segoe UI",sans-serif`;c.save();c.shadowColor='#000';c.shadowBlur=6;c.shadowOffsetY=3;c.fillText(text,cx,202);c.restore();
 if(s.showDate!==false){const week=['SUN','MON','TUE','WED','THU','FRI','SAT'][d.getDay()];c.fillStyle='#969da4';c.font='500 11px ClockManrope,"Segoe UI",sans-serif';c.fillText(`${d.getFullYear()}.${pad(d.getMonth()+1)}.${pad(d.getDate())}  ${week}`,cx,238);}
 if(s.labels!==false){c.textAlign='right';c.font='500 11px ClockManrope,"Segoe UI",sans-serif';[['SEC',158],['MIN',132],['HOUR',105]].forEach(([label,r],i)=>{const yy=105+i*23;c.fillStyle=i===0?t.hot:'#858d95';c.fillText(String(label),411,yy);c.beginPath();c.moveTo(420,yy);c.lineTo(cx-Number(r)-8,yy);c.strokeStyle='rgba(255,255,255,.16)';c.lineWidth=.8;c.stroke();});}
 // Cardinal indicators outside the rings.
 for(let i=0;i<4;i++){const a=i*Math.PI/2-Math.PI/2,[x,y]=polar(cx,cy,185,a);c.save();c.translate(x,y);c.rotate(a+Math.PI/2);c.fillStyle=t.accent;c.shadowColor=t.accent;c.shadowBlur=12;c.fillRect(-10,-1.5,20,3);c.restore();}
 c.restore();return canvas;
}
