export const ribbonDefaults={showSeconds:false,blink:true,bgColor:'#090909',lineColor:'#ff1735'};
const outlines=[
'M20 50 C20 6 90 6 90 50 L90 150 C90 194 20 194 20 150 Z',
'M22 43 Q58 40 65 16 L65 182 M20 182 L102 182',
'M20 50 C20 6 90 6 90 50 C90 82 75 88 55 94 C33 100 20 115 20 141 L20 182 L100 182',
'M20 47 C20 5 90 5 90 48 C90 74 75 91 53 91 C78 91 94 109 94 139 C94 198 20 198 20 150',
'M78 18 C30 18 20 49 20 78 L20 123 L106 123 M78 18 L78 190',
'M102 18 L20 18 L20 71 C20 96 39 98 57 98 C84 98 94 119 94 147 C94 199 20 199 20 151',
'M90 48 C90 6 20 6 20 52 L20 148 C20 196 94 196 94 148 C94 99 20 99 20 145',
'M12 18 L94 18 L94 190',
'M55 96 C8 96 8 18 55 18 C102 18 102 96 55 96 C3 96 3 182 55 182 C107 182 107 96 55 96',
'M94 59 C94 5 20 5 20 59 C20 111 94 111 94 59 L94 149 C94 198 20 198 20 157'
];
const paths=outlines.map(p=>new Path2D(p));
export function drawRibbonDigit(c:CanvasRenderingContext2D,digit:string,s:any){
 const path=paths[Number(digit)]||paths[0];c.save();c.lineCap='butt';c.lineJoin='round';
 // Nested strokes cut four clean channels into a broad ribbon.
 for(let width=29; width>=1;width-=3.1){
  const index=Math.round((29-width)/3.1);c.lineWidth=width;c.strokeStyle=index%2===0?s.lineColor:s.bgColor;c.stroke(path);
 }c.restore();
}
export function ribbonKey(s:any,d=new Date()){return [d.getHours(),d.getMinutes(),s.showSeconds?d.getSeconds():'',s.blink!==false?d.getMilliseconds()<500:true].join(':');}
export function drawRibbonClock(canvas:HTMLCanvasElement,raw:any,date=new Date()){
 const s={...ribbonDefaults,...raw};const dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const w=dim(s.ActionGeometry?.width,1000),h=dim(s.ActionGeometry?.height,340);if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.fillStyle=s.bgColor;c.fillRect(0,0,w,h);
 const groups=[date.getHours(),date.getMinutes(),...(s.showSeconds?[date.getSeconds()]:[])].map(n=>String(n).padStart(2,'0'));
 const portrait=w/h<1,lw=portrait?300:groups.length*310+20,lh=portrait?groups.length*240+30:270;
 const scale=Math.min(w/lw,h/lh);c.save();c.translate((w-lw*scale)/2,(h-lh*scale)/2);c.scale(scale,scale);
 groups.forEach((group,g)=>{const x=portrait?25:25+g*310,y=portrait?25+g*240:35;
 for(let i=0;i<2;i++){c.save();c.translate(x+i*135,y);drawRibbonDigit(c,group[i],s);c.restore();}
 if(!portrait&&g<groups.length-1){c.save();c.fillStyle=s.lineColor;c.globalAlpha=s.blink===false||date.getMilliseconds()<500?1:.15;for(const dy of [70,130]){c.beginPath();c.arc(x+277,y+dy,5,0,Math.PI*2);c.fill();}c.restore();}
 });c.restore();
}
