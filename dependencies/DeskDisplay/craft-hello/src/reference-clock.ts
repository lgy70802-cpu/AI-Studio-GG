import referenceImage from './reference-image';
import {AuroraState,timeDigits} from './aurora';
export {AuroraState as ReferenceState,timeDigits};
const atlas=new Image();let loading:Promise<void>|undefined;let background='#0b0767';
export function loadReference(){return loading??=(async()=>{await new Promise<void>((resolve,reject)=>{const timer=setTimeout(()=>{atlas.onload=null;atlas.onerror=null;reject(new Error('Reference image load timed out'));},15000);atlas.onload=()=>{clearTimeout(timer);atlas.onload=null;atlas.onerror=null;resolve();};atlas.onerror=()=>{clearTimeout(timer);reject(new Error('Reference image decode failed'));};atlas.src=referenceImage;});const c=document.createElement('canvas');c.width=c.height=1;const x=c.getContext('2d')!;x.drawImage(atlas,0,0);const p=x.getImageData(0,0,1,1).data;background='rgb('+p[0]+','+p[1]+','+p[2]+')';})().catch(error=>{loading=undefined;throw error;});}
// Coordinates use the supplied 1080 x 1503 reference layout, not the screen size.
const rects:Record<string,number[]>={1:[68,10,209,480],2:[360,10,318,480],3:[720,10,340,480],4:[0,515,360,480],5:[388,515,315,480],6:[725,515,333,480],7:[30,1014,310,480],8:[401,1014,301,480],9:[730,1014,322,458]};
export class ReferenceRenderer{
 canvas=document.createElement('canvas');zero=document.createElement('canvas');
 constructor(){this.zero.width=360;this.zero.height=480;const c=this.zero.getContext('2d')!;
 // The supplied image has no zero. Draw a separate elliptical particle band.
 for(let row=0;row<150;row++){const t=row/150*Math.PI*2;for(let lane=0;lane<17;lane++){const v=lane/16,rx=92+v*66,ry=160+v*65;const x=180+Math.cos(t)*rx,y=240+Math.sin(t)*ry;const size=1.2+v*2.1;const hue=190+115*(.5+.5*Math.sin(t*2+v));c.save();c.translate(x,y);c.rotate(Math.atan2(ry*Math.cos(t),-rx*Math.sin(t))+.25*v);c.fillStyle=Math.sin(t*3+v)>.94?'#efdcad':'hsl('+hue+',78%,'+(62+12*v)+'%)';c.fillRect(-size/2,-size/2,size,size);c.restore();}}}
 digit(c:CanvasRenderingContext2D,digit:string,x:number,y:number){c.fillStyle=background;c.fillRect(x,y,360,480);if(digit==='0'){c.drawImage(this.zero,x,y,360,480);return;}const r=rects[digit];if(!r)return;const sx=atlas.naturalWidth/1080,sy=atlas.naturalHeight/1503;const scale=Math.min(360/r[2],480/r[3]);c.drawImage(atlas,r[0]*sx,r[1]*sy,r[2]*sx,r[3]*sy,x+(360-r[2]*scale)/2,y+(480-r[3]*scale)/2,r[2]*scale,r[3]*scale);}
 draw(from:string,to:string,p:number,w:number,h:number,time=0){const a=this.canvas;if(a.width!==w)a.width=w;if(a.height!==h)a.height=h;const c=a.getContext('2d')!;c.fillStyle=background;c.fillRect(0,0,w,h);const portrait=w/h<1,lw=portrait?840:1700,lh=portrait?1100:600,k=Math.min(w/lw,h/lh);c.save();c.translate((w-lw*k)/2,(h-lh*k)/2);c.scale(k,k);
 for(let i=0;i<4;i++){const x=portrait?40+i%2*400:40+i*400+(i>1?60:0),y=portrait?35+Math.floor(i/2)*550:60;
 if(from[i]!==to[i]&&p<1){this.digit(c,from[i],x,y);c.globalAlpha=p;this.digit(c,to[i],x,y);c.globalAlpha=1;}else this.digit(c,to[i],x,y);}
 c.fillStyle='#a8ddff';c.globalAlpha=time%1000<500?1:.25;for(const offset of [-26,26]){c.beginPath();c.arc(portrait?420:830,(portrait?555:300)+offset,7,0,Math.PI*2);c.fill();}c.restore();return a;}
}