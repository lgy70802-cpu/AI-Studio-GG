import {ensureTimeFont} from './fonts';
export const loadFoldFont=()=>ensureTimeFont('manrope');
export const foldDefaults={showSeconds:false,blink:true,bgColor:'#000000',lineColor:'#ffffff'};
const cache=new Map<string,HTMLCanvasElement>();
export function drawFoldDigit(c:CanvasRenderingContext2D,digit:string,s:any){
 const key=digit+':'+s.lineColor;let tile=cache.get(key);
 if(!tile){
 tile=document.createElement('canvas');tile.width=280;tile.height=440;const t=tile.getContext('2d')!;
 t.font='600 380px ClockManrope';t.textAlign='center';t.textBaseline='alphabetic';
 const m=t.measureText(digit),scale=Math.min(1,240/m.width);t.save();t.translate(140,0);t.scale(scale,1);t.fillText(digit,0,220+(m.actualBoundingBoxAscent-m.actualBoundingBoxDescent)/2);t.restore();
 // Paint on a separate layer, then retain only the glyph alpha mask.
 const paint=document.createElement('canvas');paint.width=280;paint.height=440;const p=paint.getContext('2d')!;
 const gradient=(x1:number,y1:number,x2:number,y2:number,reverse=false)=>{const g=p.createLinearGradient(x1,y1,x2,y2);g.addColorStop(0,reverse?'#191719':s.lineColor);g.addColorStop(.3,reverse?'#555255':s.lineColor);g.addColorStop(.75,reverse?s.lineColor:'#555255');g.addColorStop(1,reverse?s.lineColor:'#191719');return g;};
 p.fillStyle=gradient(0,40,80,420);p.fillRect(0,0,280,440);
 const n=Number(digit);p.beginPath();p.moveTo(280,55+n*3);p.bezierCurveTo(225,190,35,150,20,420);p.lineTo(280,440);p.closePath();p.fillStyle=gradient(25,220,270,300,true);p.fill();
 if([0,3,6,8,9].includes(n)){p.beginPath();p.moveTo(0,170);p.bezierCurveTo(155,100,270,285,245,390);p.bezierCurveTo(185,290,85,285,0,310);p.closePath();p.fillStyle=gradient(0,165,210,350);p.fill();}
 t.globalCompositeOperation='source-in';t.drawImage(paint,0,0);t.globalCompositeOperation='source-over';
 if(cache.size>=40)cache.clear();cache.set(key,tile);
 }
 c.drawImage(tile,0,-8,112,208);
}
export function foldKey(s:any,d=new Date()){return [d.getHours(),d.getMinutes(),s.showSeconds?d.getSeconds():'',s.blink!==false?d.getMilliseconds()<500:true].join(':');}
export function drawFoldClock(canvas:HTMLCanvasElement,raw:any,date=new Date()){
 const s={...foldDefaults,...raw};const dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const w=dim(s.ActionGeometry?.width,1000),h=dim(s.ActionGeometry?.height,340);if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.fillStyle=s.bgColor;c.fillRect(0,0,w,h);
 const groups=[date.getHours(),date.getMinutes(),...(s.showSeconds?[date.getSeconds()]:[])].map(n=>String(n).padStart(2,'0'));
 const portrait=w/h<1,lw=portrait?300:groups.length*310+20,lh=portrait?groups.length*240+30:270;
 const scale=Math.min(w/lw,h/lh);c.save();c.translate((w-lw*scale)/2,(h-lh*scale)/2);c.scale(scale,scale);
 groups.forEach((group,g)=>{const x=portrait?25:25+g*310,y=portrait?25+g*240:35;
 for(let i=0;i<2;i++){c.save();c.translate(x+i*135,y);drawFoldDigit(c,group[i],s);c.restore();}
 if(!portrait&&g<groups.length-1){c.save();c.fillStyle=s.lineColor;c.globalAlpha=s.blink===false||date.getMilliseconds()<500?1:.15;for(const dy of [70,130]){c.beginPath();c.arc(x+277,y+dy,5,0,Math.PI*2);c.fill();}c.restore();}
 });c.restore();
}
