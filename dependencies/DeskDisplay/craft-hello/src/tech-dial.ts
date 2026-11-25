import {ensureTimeFont} from './fonts';

export const loadTechDialFont=()=>ensureTimeFont('manrope');
export const techDialDefaults={theme:'orange',smooth:true,showSeconds:true,showDate:true,railFlow:true,glow:85,transport:'compatible'};
const themes:any={
 orange:{accent:'#ff9418',hot:'#ffd274',dim:'#6d3a0b'},
 ice:{accent:'#27c8ff',hot:'#a9efff',dim:'#0b4968'},
 red:{accent:'#ff3c3c',hot:'#ffaaa0',dim:'#6e1515'}
};
const pad=(v:number)=>String(v).padStart(2,'0');
export function techDialKey(s:any,d=new Date()){const phase=s.smooth===false?d.getSeconds():Math.floor(d.getTime()/50);return [phase,s.showSeconds!==false?d.getSeconds():'',d.toDateString(),JSON.stringify(s)].join(':');}
function circle(c:CanvasRenderingContext2D,x:number,y:number,r:number){c.beginPath();c.arc(x,y,r,0,Math.PI*2);}
function rounded(c:CanvasRenderingContext2D,x:number,y:number,w:number,h:number,r:number){c.beginPath();c.roundRect(x,y,w,h,r);}
function polar(cx:number,cy:number,r:number,a:number){return [cx+Math.cos(a)*r,cy+Math.sin(a)*r] as const;}

export function drawTechDial(canvas:HTMLCanvasElement,raw:any,d=new Date()){
 const s={...techDialDefaults,...raw},t=themes[s.theme]||themes.orange,w=Math.max(1,Math.round(Number(s.ActionGeometry?.width)||1200)),h=Math.max(1,Math.round(Number(s.ActionGeometry?.height)||400));if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!,LW=1200,LH=400,k=Math.min(w/LW,h/LH);c.fillStyle='#11151a';c.fillRect(0,0,w,h);c.save();c.translate((w-LW*k)/2,(h-LH*k)/2);c.scale(k,k);
 // Brushed dark background.
 const bg=c.createRadialGradient(600,190,30,600,190,720);bg.addColorStop(0,'#30363e');bg.addColorStop(.46,'#20252b');bg.addColorStop(1,'#0d1115');c.fillStyle=bg;c.fillRect(0,0,LW,LH);c.globalAlpha=.09;c.strokeStyle='#aab1b9';c.lineWidth=.45;for(let y=1;y<LH;y+=3){c.beginPath();c.moveTo(0,y);c.lineTo(LW,y);c.stroke();}c.globalAlpha=1;
 const cx=600,cy=200,outer=176,inner=137;
 // Horizontal metal rails behind the dial.
 for(const yy of [190,210]){const rail=c.createLinearGradient(0,yy-4,0,yy+4);rail.addColorStop(0,'#59616a');rail.addColorStop(.35,'#0a0d10');rail.addColorStop(.7,'#252b31');rail.addColorStop(1,'#050708');c.fillStyle=rail;c.fillRect(0,yy-3,LW,6);}
 c.strokeStyle='#06080a';c.lineWidth=3;c.beginPath();c.moveTo(0,181);c.lineTo(410,181);c.quadraticCurveTo(432,181,438,160);c.stroke();c.beginPath();c.moveTo(790,181);c.quadraticCurveTo(768,181,762,160);c.lineTo(762,160);c.stroke();
 // Moving light pulses in both rails.
 if(s.railFlow!==false){const q=((d.getTime()/2600)%1),leftX=160+q*250,rightX=1040-q*250;for(const x of [leftX,rightX]){const g=c.createLinearGradient(x-70,0,x+70,0);g.addColorStop(0,'rgba(255,150,30,0)');g.addColorStop(.5,t.accent);g.addColorStop(1,'rgba(255,150,30,0)');c.save();c.shadowColor=t.accent;c.shadowBlur=16;c.fillStyle=g;c.fillRect(x-70,194,140,4);c.restore();}}
 // Outer casing.
 c.save();c.shadowColor='#000';c.shadowBlur=24;c.shadowOffsetY=9;circle(c,cx,cy,outer+8);c.fillStyle='#090c0f';c.fill();c.restore();
 const rim=c.createRadialGradient(cx-45,cy-55,40,cx,cy,outer+8);rim.addColorStop(0,'#4b525b');rim.addColorStop(.55,'#22272d');rim.addColorStop(.84,'#0b0e12');rim.addColorStop(1,'#4e565e');circle(c,cx,cy,outer+5);c.fillStyle=rim;c.fill();circle(c,cx,cy,outer);c.fillStyle='#151a20';c.fill();c.strokeStyle='#aeb5bb';c.lineWidth=1.3;c.stroke();
 // Minute/second ticks.
 const sec=d.getSeconds(),ms=s.smooth===false?0:d.getMilliseconds(),second=sec+ms/1000;for(let i=0;i<60;i++){const a=i*Math.PI/30-Math.PI/2,long=i%5===0,r1=outer-(long?24:17),r2=outer-8,[x1,y1]=polar(cx,cy,r1,a),[x2,y2]=polar(cx,cy,r2,a);c.beginPath();c.moveTo(x1,y1);c.lineTo(x2,y2);const active=i===sec;c.strokeStyle=active?t.hot:long?'#9299a1':'#69717a';c.lineWidth=active?3.8:long?2.2:1.2;if(active){c.save();c.shadowColor=t.accent;c.shadowBlur=13;c.stroke();c.restore();}else c.stroke();}
 // Inner face and recessed border.
 circle(c,cx,cy,inner+3);c.fillStyle='#070a0d';c.fill();c.strokeStyle='#626a72';c.lineWidth=1.2;c.stroke();const face=c.createRadialGradient(cx-38,cy-48,18,cx,cy,inner);face.addColorStop(0,'#2b3138');face.addColorStop(.65,'#171c21');face.addColorStop(1,'#080b0e');circle(c,cx,cy,inner);c.fillStyle=face;c.fill();
 // Inner moving light strip: its leading edge points at the current second.
 const angle=second*Math.PI/30-Math.PI/2,arcLen=Math.PI*.19,glow=Math.max(20,Math.min(100,Number(s.glow)||85))/100;c.save();c.lineCap='round';c.shadowColor=t.accent;c.shadowBlur=22*glow;c.strokeStyle=t.accent;c.lineWidth=4.5;c.globalAlpha=.92*glow;c.beginPath();c.arc(cx,cy,inner-3,angle-arcLen,angle);c.stroke();c.shadowBlur=7;c.strokeStyle=t.hot;c.lineWidth=1.5;c.beginPath();c.arc(cx,cy,inner-3,angle-arcLen*.55,angle);c.stroke();c.restore();
 // Subtle dim trail behind the moving strip.
 const trail=c.createConicGradient(angle-Math.PI/2,cx,cy);trail.addColorStop(0,'rgba(255,140,20,0)');trail.addColorStop(.92,'rgba(255,140,20,0)');trail.addColorStop(1,t.dim);c.save();circle(c,cx,cy,inner-9);c.strokeStyle=trail;c.lineWidth=1;c.globalAlpha=.7;c.stroke();c.restore();
 // Date and digital time.
 c.textAlign='center';c.textBaseline='middle';if(s.showDate!==false){const week=['周日','周一','周二','周三','周四','周五','周六'][d.getDay()];c.fillStyle='#c6cbd0';c.font='400 14px ClockManrope,"Microsoft YaHei",sans-serif';c.fillText(`${d.getFullYear()}年${pad(d.getMonth()+1)}月${pad(d.getDate())}日  ${week}`,cx,145);c.fillStyle=t.accent;c.fillRect(cx-13,166,26,1.3);}
 const text=s.showSeconds===false?`${pad(d.getHours())}:${pad(d.getMinutes())}`:`${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;c.save();c.shadowColor='#000';c.shadowBlur=6;c.shadowOffsetY=4;c.fillStyle='#d9dde1';c.font=`300 ${s.showSeconds===false?59:48}px ClockManrope,"Segoe UI",sans-serif`;c.fillText(text,cx,222);c.restore();
 // Glass reflection and outer highlights.
 const glass=c.createLinearGradient(0,58,0,270);glass.addColorStop(0,'rgba(255,255,255,.10)');glass.addColorStop(.42,'rgba(255,255,255,.015)');glass.addColorStop(1,'rgba(255,255,255,0)');c.save();circle(c,cx,cy,inner-7);c.clip();c.fillStyle=glass;c.fillRect(cx-inner,cy-inner,inner*2,inner*1.35);c.restore();circle(c,cx,cy,outer+5);c.strokeStyle='rgba(255,255,255,.38)';c.lineWidth=.8;c.stroke();
 c.restore();return canvas;
}
