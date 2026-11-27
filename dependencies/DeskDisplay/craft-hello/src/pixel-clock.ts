export const pixelDefaults={showSeconds:true,blink:true,color:'#f4f8ff',brightness:90,glow:true};
export const pixelDigits=[
['11111','11011','11011','11011','11011','11011','11111'],
['00110','01110','00110','00110','00110','00110','11111'],
['11111','00011','00011','11111','11000','11000','11111'],
['11111','00011','00011','01111','00011','00011','11111'],
['11011','11011','11011','11111','00011','00011','00011'],
['11111','11000','11000','11111','00011','00011','11111'],
['11111','11000','11000','11111','11011','11011','11111'],
['11111','00011','00011','00110','01100','01100','01100'],
['11111','11011','11011','11111','11011','11011','11111'],
['11111','11011','11011','11111','00011','00011','11111']];
export function pixelKey(raw:any,date:Date){const s={...pixelDefaults,...raw};return [date.getHours(),date.getMinutes(),s.showSeconds?date.getSeconds():'',s.blink?date.getMilliseconds()<500:true].join(':');}
export function drawPixelClock(canvas:HTMLCanvasElement,raw:any,date=new Date()){
 const s={...pixelDefaults,...raw},dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const w=dim(s.ActionGeometry?.width,1100),h=dim(s.ActionGeometry?.height,320);if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.fillStyle='#050607';c.fillRect(0,0,w,h);
 const pairs=[date.getHours(),date.getMinutes(),...(s.showSeconds?[date.getSeconds()]:[])].map(n=>String(n).padStart(2,'0')),portrait=w/h<1;
 const cols=portrait?11:pairs.length*11+(pairs.length-1)*3,rows=portrait?pairs.length*9-2:7;
 const step=Math.max(.01,Math.min(w/(cols+3),h/(rows+3))),ox=(w-cols*step)/2,oy=(h-rows*step)/2;
 const intensity=Math.min(1,Math.max(.1,(Number(s.brightness)||90)/100));
 function cell(x:number,y:number,on:boolean){const left=Math.round(ox+x*step),top=Math.round(oy+y*step),gap=step>5?Math.max(1,Math.round(step*.14)):step*.12,size=Math.max(.1,Math.round(step)-gap);
 c.save();c.fillStyle=on?s.color:'#101317';c.globalAlpha=on?intensity:.65;if(on&&s.glow){c.shadowColor=s.color;c.shadowBlur=Math.min(8,step*.22);}c.fillRect(left,top,size,size);c.shadowBlur=0;
 if(on&&step>5){c.globalAlpha=intensity*.25;c.fillStyle='#fff';c.fillRect(left+size*.17,top+size*.12,size*.66,Math.max(1,size*.13));}c.restore();}
 pairs.forEach((pair,g)=>{const x=portrait?0:g*14,y=portrait?g*9:0;
 for(let i=0;i<2;i++)pixelDigits[Number(pair[i])].forEach((row,r)=>Array.from(row).forEach((bit,col)=>cell(x+i*6+col,y+r,bit==='1')));
 if(!portrait&&g<pairs.length-1){for(let row=0;row<7;row++)cell(x+12,row,(row===2||row===4)&&(!s.blink||date.getMilliseconds()<500));}
 });
}
