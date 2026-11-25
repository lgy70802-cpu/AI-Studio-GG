import {fontSpec} from './fonts';
import { normalize,timeParts } from './settings';
export function drawClock(canvas:HTMLCanvasElement, raw:any, now=new Date()) {
 const s=normalize(raw); const size=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const w=size(s.ActionGeometry?.width,600),h=size(s.ActionGeometry?.height,240);
 if(canvas.width!==w)canvas.width=w;if(canvas.height!==h)canvas.height=h;
 const c=canvas.getContext('2d')!;c.clearRect(0,0,w,h);c.save();
 if(!s.transparent){c.fillStyle=s.bgColor;c.fillRect(0,0,w,h);}
 c.fillStyle=s.textColor;c.textAlign='center';c.textBaseline='middle';
 const pad=Math.min(w,h)*.09,inner=w-pad*2,portrait=w/h<.95;
 const text=String(s.text??'').slice(0,60), footer=[s.showDate?now.getFullYear()+' / '+String(now.getMonth()+1).padStart(2,'0')+' / '+String(now.getDate()).padStart(2,'0'):'',s.showWeekday?'星期'+'日一二三四五六'[now.getDay()]:''].filter(Boolean).join('   ');
 const fit=(value:string,y:number,fontSize:number,mono=false)=>{
  const spec=fontSpec(s.timeFont); const weight=mono?spec.weight:500;
  const family=mono?spec.family+', sans-serif':'"Microsoft YaHei", sans-serif';
  c.font=(weight+' ')+fontSize+'px '+family;
  if(mono){
   // Reserve the widest digit advance for every digit. Measure all ten,
   // never the current time, so both the font size and anchors stay fixed.
   const digitWidth=Math.max(...Array.from('0123456789',digit=>c.measureText(digit).width));
   const colonWidth=c.measureText(':').width;
   const widths=Array.from(value,char=>char===':'?colonWidth:digitWidth);
   const total=widths.reduce((sum,width)=>sum+width,0);
   const scale=total>inner?inner/total:1;
   c.font=(weight+' ')+(fontSize*scale)+'px '+family;
   let x=(w-total*scale)/2;
   Array.from(value).forEach((char,index)=>{
    const width=widths[index]*scale;
    c.fillText(char,x+width/2,y);
    x+=width;
   });
   return;
  }
  const measured=c.measureText(value).width;if(measured>inner)c.font=(weight+' ')+(fontSize*inner/measured)+'px '+family;
  c.fillText(value,w/2,y);
 };
 if(text)fit(text,pad+Math.min(h*.055,w*.05),Math.min(h*.085,w*.065));
 const top=text?pad+h*.16:pad, bottom=footer?h-pad-h*.13:h-pad;
 const {digits,period}=timeParts(s,now);const lines=portrait?digits:[digits.join(':')];
 const periodSpace=period?Math.min(h*.07,w*.07):0, available=Math.max(1,bottom-top-periodSpace);
 lines.forEach((line,i)=>fit(line,top+available*(i+.5)/lines.length,Math.min(available/lines.length*.82,portrait?w*.65:inner*.24),true));
 if(period){c.globalAlpha=.7;fit(period,bottom-periodSpace*.3,periodSpace*.8);c.globalAlpha=1;}
 if(footer){c.globalAlpha=.65;fit(footer,h-pad-Math.min(h*.02,w*.02),Math.min(h*.067,w*.047));}
 c.restore();
}
