export const auroraDefaults={animate:true,duration:800,breath:true};
const curves=[
'M25 50 C25 8 90 8 90 50 L90 155 C90 197 25 197 25 155 Z',
'M25 43 Q55 37 64 19 L64 187',
'M23 49 C23 5 94 5 94 50 C94 85 64 98 47 112 Q23 134 23 183 L96 183',
'M22 42 C45 6 94 14 94 51 Q94 92 55 96 Q96 97 96 140 C96 195 40 202 21 168',
'M81 18 L20 126 L106 126 M81 18 L81 190',
'M98 20 L25 20 L25 87 C107 73 114 188 60 188 Q30 189 20 166',
'M92 31 C43 -5 21 51 23 130 C18 209 102 205 96 143 C91 94 26 105 23 143',
'M17 20 L98 20 Q67 89 45 188',
'M59 96 C7 93 13 18 59 18 C106 18 112 93 59 96 C1 98 8 189 59 189 C109 189 116 98 59 96',
'M94 70 C99 0 23 1 23 62 C23 118 91 115 94 65 L94 133 Q94 206 32 177'
];
type Dot={x:number,y:number,angle:number,size:number,color:string};
const banks=new Map<string,Dot[]>();
export function particles(digit:string,step=4):Dot[]{
 const key=digit+':'+step;if(banks.has(key))return banks.get(key)!;
 const path=document.createElementNS('http://www.w3.org/2000/svg','path');path.setAttribute('d',curves[Number(digit)]||curves[0]);const length=path.getTotalLength(),out:Dot[]=[];
 const rows=Math.max(45,Math.round(length/step)),lanes=9;
 for(let row=0;row<rows;row++){const u=row/(rows-1),at=u*length,p=path.getPointAtLength(at),a=path.getPointAtLength(Math.max(0,at-.7)),b=path.getPointAtLength(Math.min(length,at+.7)),angle=Math.atan2(b.y-a.y,b.x-a.x);
 for(let j=0;j<lanes;j++){const v=(j-4)/4,offset=v*15,twist=Math.sin(u*Math.PI*3)*v*2;
 const hue=190+110*(.5+.5*Math.sin(u*8+j*.16+Number(digit)*.7));
 out.push({x:p.x-Math.sin(angle)*offset+Math.cos(angle)*twist,y:p.y+Math.cos(angle)*offset+Math.sin(angle)*twist,angle:angle+v*.24,size:1.4+1.05*(1-Math.abs(v)),color:Math.sin(u*8+j*.16+Number(digit)*.7)>.94?'#f5dc91':'hsl('+hue+',85%,'+(58+17*(1-Math.abs(v)))+'%)'});
 }}
 banks.set(key,out);return out;
}
export const timeDigits=(d=new Date())=>String(d.getHours()).padStart(2,'0')+String(d.getMinutes()).padStart(2,'0');
export class AuroraState{
 from='';to='';start=0;until=0;revision=0;
 update(value:string,now:number){if(this.until&&now<this.until)return;this.until=0;if(value!==this.to){this.from=this.to||value;this.to=value;this.start=now;this.revision++;}}
 demo(now:number){this.from=this.to;this.to=this.to==='2048'?'1359':'2048';this.start=now;this.until=now+1800;this.revision++;}
 progress(now:number,s:any){return s.animate===false?1:Math.min(1,Math.max(0,(now-this.start)/(Number(s.duration)||800)));}
}
export function pulse(timestamp:number){return .4+.6*(.5+.5*Math.cos(timestamp/1000*Math.PI*2));}
export class AuroraRenderer{
 canvas=document.createElement('canvas');
 draw(from:string,to:string,p:number,w:number,h:number,brightness=1){
 const c=this.canvas;if(c.width!==w)c.width=w;if(c.height!==h)c.height=h;
 const ctx=c.getContext('2d')!;ctx.fillStyle='#090c35';ctx.fillRect(0,0,w,h);
 const portrait=w/h<1,lw=portrait?300:650,lh=portrait?500:270,k=Math.min(w/lw,h/lh);
 ctx.save();ctx.translate((w-lw*k)/2,(h-lh*k)/2);ctx.scale(k,k);
 const ease=p*p*(3-2*p),step=k<.8?6:4;
 for(let digit=0;digit<4;digit++){
 const x=portrait?28+(digit%2)*135:24+digit*142+(digit>1?35:0),y=portrait?24+Math.floor(digit/2)*240:32;
 const target=particles(to[digit],step),source=particles(from[digit]||to[digit],step),changing=from[digit]!==to[digit]&&p<1,n=changing?Math.max(source.length,target.length):target.length;
 for(let i=0;i<n;i++){
 const b=target[Math.min(target.length-1,Math.floor(i*target.length/n))],a=source[Math.min(source.length-1,Math.floor(i*source.length/n))];
 const arc=changing?Math.sin(p*Math.PI)*Math.sin(i*2.399)*6:0;
 const xx=changing?a.x+(b.x-a.x)*ease+arc:b.x, yy=changing?a.y+(b.y-a.y)*ease+arc*.65:b.y;
 ctx.save();ctx.translate(x+xx,y+yy);ctx.rotate(changing?a.angle+(b.angle-a.angle)*ease:b.angle);ctx.fillStyle=b.color;ctx.fillRect(-b.size/2,-b.size/2,b.size,b.size);ctx.restore();
 }
 }
 ctx.globalAlpha=brightness;
 const cx=portrait?150:326,cy=portrait?244:126;
 for(const dy of [-12,12])for(let j=0;j<12;j++){const angle=j*Math.PI/6;ctx.fillStyle=j%2?'#ff9bdf':'#73e8ff';ctx.fillRect(cx+Math.cos(angle)*4-1,cy+dy+Math.sin(angle)*4-1,2,2);}
 ctx.restore();return c;
 }
}
