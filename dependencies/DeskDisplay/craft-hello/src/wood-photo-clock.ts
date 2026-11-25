import {ensureTimeFont} from './fonts';
export const woodPhotoDefaults={animate:true,duration:440,hour12:false,transport:'compatible'};
let background:HTMLImageElement|null=null,loading:Promise<void>|null=null;
export function loadWoodPhotoAssets(){if(!loading)loading=(async()=>{
 // Craft ships an older embedded browser on some versions. Image.decode()
 // is not reliable there, so use the broadly supported load event instead.
 try{await ensureTimeFont('barlow');}catch(error){console.warn(JSON.stringify({event:'wood_photo_font_fallback',error:String(error)}));}
 const image=new Image();await new Promise<void>((resolve,reject)=>{const timer=setTimeout(()=>{image.onload=null;image.onerror=null;reject(new Error('Wood background load timed out'));},15000);image.onload=()=>{clearTimeout(timer);image.onload=null;image.onerror=null;resolve();};image.onerror=()=>{clearTimeout(timer);image.onload=null;image.onerror=null;reject(new Error('Wood background load failed'));};image.src=new URL('images/wood-calendar-bg.jpg',document.baseURI).href;});background=image;
 })().catch(error=>{loading=null;throw error;});return loading;}
export const woodPhotoValues=(d=new Date(),hour12=false)=>{let h=d.getHours();if(hour12)h=h%12||12;return [String(d.getMonth()+1).padStart(2,'0'),String(d.getDate()).padStart(2,'0'),String(h).padStart(2,'0'),String(d.getMinutes()).padStart(2,'0'),String(d.getSeconds()).padStart(2,'0')];};
const durationOf=(s:any)=>Math.min(1200,Math.max(220,Number(s.duration)||440));
export class WoodPhotoState{from=['01','01','00','00','00'];to=['01','01','00','00','00'];started=0;revision=0;update(v:string[],now:number){if(v.some((x,i)=>x!==this.to[i])){this.from=[...this.to];this.to=[...v];this.started=now;this.revision++;}}progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.started)/durationOf(s)));}}
const BW=2557,BH=615,TW=310,TH=294;
// The source artwork's physical hinge is at y=312. With a 294 px digit tile,
// y=165 places the tile split exactly on that hinge.
const CENTERS=[492,858,1317,1704,2090],TOP=165;
const COLORS=['#42332a','#42332a','#eee7d8','#eee7d8','#593817'];
export class WoodPhotoRenderer{
 canvas=document.createElement('canvas');tiles=new Map<string,HTMLCanvasElement>();
 tile(value:string,index:number){const key=index+value;if(this.tiles.has(key))return this.tiles.get(key)!;const a=document.createElement('canvas');a.width=TW;a.height=TH;const c=a.getContext('2d')!;c.fillStyle=COLORS[index];c.font='800 174px ClockBarlow';c.textAlign='center';c.textBaseline='middle';
  // Draw the two digits in independent fixed-width cells. Centering the whole
  // string makes a narrow glyph such as "1" change the position of its mate.
  const digits=String(value).padStart(2,'0').slice(-2),digitSpacing=102;
  c.fillText(digits[0],TW/2-digitSpacing/2,TH/2);c.fillText(digits[1],TW/2+digitSpacing/2,TH/2);
  this.tiles.set(key,a);return a;}
 draw(from:string[],to:string[],p:number,raw:any){const s={...woodPhotoDefaults,...raw},w=Math.max(1,Math.round(Number(s.ActionGeometry?.width)||1920)),h=Math.max(1,Math.round(Number(s.ActionGeometry?.height)||462));if(this.canvas.width!==w)this.canvas.width=w;if(this.canvas.height!==h)this.canvas.height=h;const c=this.canvas.getContext('2d')!;c.fillStyle='#e4bd82';c.fillRect(0,0,w,h);if(!background)return this.canvas;
  // Cover crop is below one pixel at D92 ratio and preserves the original composition.
  const scale=Math.max(w/BW,h/BH),dw=BW*scale,dh=BH*scale,ox=(w-dw)/2,oy=(h-dh)/2;c.drawImage(background,ox,oy,dw,dh);c.save();c.translate(ox,oy);c.scale(scale,scale);
  for(let i=0;i<5;i++){const x=CENTERS[i]-TW/2,y=TOP,old=this.tile(from[i]||to[i],i),next=this.tile(to[i],i),changed=from[i]!==to[i]&&p<1;c.save();c.translate(x,y);
   if(!changed)c.drawImage(next,0,0);else{
    // The static halves establish the old/new state. Each changed digit then
    // receives its own clipped flap, so 23 -> 24 only animates the final digit.
    c.drawImage(next,0,0,TW,TH/2,0,0,TW,TH/2);c.drawImage(old,0,TH/2,TW,TH/2,0,TH/2,TW,TH/2);
    const first=p<.5,factor=first?Math.cos(p*Math.PI):Math.sin((p-.5)*Math.PI),piece=first?old:next,sy=first?0:TH/2,dy=first?TH/2-TH/2*factor:TH/2,dh2=TH/2*factor;
    for(let digit=0;digit<2;digit++)if(from[i][digit]!==to[i][digit]){const clipX=digit===0?38:TW/2,clipW=digit===0?TW/2-38:TW/2-38;c.save();c.beginPath();c.rect(clipX,0,clipW,TH);c.clip();
     if(dh2>.2)c.drawImage(piece,0,sy,TW,TH/2,0,dy,TW,dh2);
     // Keep the cast shadow at the physical hinge instead of following the
     // moving flap edge or spilling onto the side hardware.
     const alpha=(first?.16:.13)*(1-factor),band=7,by=first?TH/2:TH/2-band,shade=c.createLinearGradient(0,by,0,by+band);if(first){shade.addColorStop(0,'rgba(0,0,0,'+alpha+')');shade.addColorStop(1,'rgba(0,0,0,0)');}else{shade.addColorStop(0,'rgba(0,0,0,0)');shade.addColorStop(1,'rgba(0,0,0,'+alpha+')');}c.fillStyle=shade;c.fillRect(clipX,by,clipW,band);c.restore();
    }
   }
   // Keep the photo's physical center seam visible over dynamic digits.
   c.fillStyle=i===2||i===3?'rgba(18,17,15,.62)':'rgba(83,57,35,.43)';
   // Stop the generated seam before the side hinges. The photographed
   // hardware already contains its own lighting and must remain untouched.
   const seamInset=24;c.fillRect(seamInset,TH/2-1.2,TW-seamInset*2,2.4);c.restore();
  }
  c.restore();return this.canvas;
 }
}
