import {particles,AuroraState,timeDigits} from './aurora';
export {AuroraState as StardustState,timeDigits};
function noise(n:number){const x=Math.sin(n*127.1+311.7)*43758.5453;return x-Math.floor(x);}
export class StardustRenderer{
 canvas=document.createElement('canvas');
 draw(from:string,to:string,p:number,w:number,h:number,time=0){
 const canvas=this.canvas;if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.fillStyle='#040914';c.fillRect(0,0,w,h);
 const portrait=w/h<1,lw=portrait?300:650,lh=portrait?500:270,k=Math.min(w/lw,h/lh);
 c.save();c.translate((w-lw*k)/2,(h-lh*k)/2);c.scale(k,k);
 // Sparse deterministic background stars. Their positions never jump on refresh.
 for(let i=0;i<65;i++){c.globalAlpha=.13+.16*noise(i+40);c.fillStyle='#aac5ec';c.beginPath();c.arc(noise(i)*lw,noise(i+200)*lh,.3+.5*noise(i+300),0,Math.PI*2);c.fill();}
 const ease=p*p*(3-2*p),step=k<.8?6:4;
 for(let digit=0;digit<4;digit++){
 const x=portrait?28+digit%2*135:24+digit*142+(digit>1?35:0),y=portrait?24+Math.floor(digit/2)*240:32;
 const b=particles(to[digit],step),a=particles(from[digit]||to[digit],step),changing=from[digit]!==to[digit]&&p<1,n=changing?Math.max(a.length,b.length):b.length;
 for(let i=0;i<n;i++){
 const target=b[Math.min(b.length-1,Math.floor(i*b.length/n))],old=a[Math.min(a.length-1,Math.floor(i*a.length/n))],seed=i+digit*3200;
 const drift=Math.sin(p*Math.PI)*(noise(seed+8)-.5)*24;
 const px=x+(changing?old.x+(target.x-old.x)*ease+drift:target.x)+(noise(seed)-.5)*2.6;
 const py=y+(changing?old.y+(target.y-old.y)*ease-drift*.5:target.y)+(noise(seed+9)-.5)*2.6;
 const twinkle=.65+.35*(.5+.5*Math.sin(time*.0012+seed*1.7));
 c.globalAlpha=twinkle;c.fillStyle=noise(seed+50)>.9?'#ffe4ad':noise(seed+60)>.5?'#dcefff':'#7cbce8';
 const radius=.35+noise(seed+17)*.85;c.beginPath();c.arc(px,py,radius,0,Math.PI*2);c.fill();
 if(i%67===0){c.globalAlpha=.32*twinkle;c.fillRect(px-3,py-.25,6,.5);c.fillRect(px-.25,py-3,.5,6);}
 }
 // Only a few motes drift outside each stable digit silhouette.
 for(let i=0;i<18;i++){const anchor=b[Math.floor(noise(i+digit*50)*b.length)],seed=i+digit*21;
 const px=x+anchor.x+Math.sin(time*.00045+seed)*9,py=y+anchor.y+Math.cos(time*.0003+seed*2)*11;
 c.globalAlpha=.12+.16*noise(seed+9);c.fillStyle='#acd9ff';c.beginPath();c.arc(px,py,.5+noise(seed),0,Math.PI*2);c.fill();}
 }
 const cx=portrait?150:326,cy=portrait?244:126;
 for(const dy of [-12,12]){c.globalAlpha=.7+.3*Math.cos(time*Math.PI/1000)**2;const g=c.createRadialGradient(cx,cy+dy,0,cx,cy+dy,7);g.addColorStop(0,'#e6f5ff');g.addColorStop(.25,'#90caff');g.addColorStop(1,'rgba(80,150,255,0)');c.fillStyle=g;c.fillRect(cx-7,cy+dy-7,14,14);}
 c.restore();return canvas;
 }
}