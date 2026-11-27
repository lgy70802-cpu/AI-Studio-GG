const fs=require('fs');let source=fs.readFileSync('src/ribbon-clock.ts','utf8');const layout=source.slice(source.indexOf('export function ribbonKey')).replaceAll('ribbon','fold').replaceAll('Ribbon','Fold');const head=`import {ensureTimeFont} from './fonts';
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
`;fs.writeFileSync('src/fold-clock.ts',head+layout);
let action=fs.readFileSync('src/plugin/actions/ribbonclock.ts','utf8').replaceAll('Ribbon','Fold').replaceAll('ribbon','fold');action=action.replace('drawFoldClock,foldKey','drawFoldClock,foldKey,loadFoldFont').replace('willAppear({context}){','async willAppear({context}){').replace('draw(context,true);plugin.Interval','const current=instances.get(context);try{await loadFoldFont();}catch(error){console.error(JSON.stringify({event:"fold_font_error",error:String(error)}));return;}if(instances.get(context)!==current)return;draw(context,true);plugin.Interval');fs.writeFileSync('src/plugin/actions/foldclock.ts',action);
let pi=fs.readFileSync('src/pages/actions/ribbonclock.vue','utf8').replaceAll('ribbon','fold').replaceAll('流线数字时钟','折光数字时钟').replace('平行线条 · 圆弧转折 · 红黑配色','弧面折带 · 黑白渐变 · 固定字位').replace('线条颜色','高光颜色');fs.writeFileSync('src/pages/actions/foldclock.vue',pi);
let preview=fs.readFileSync('ribbon-preview.html','utf8').replaceAll('Ribbon','Fold').replaceAll('ribbon','fold').replaceAll('流线数字时钟','折光数字时钟').replaceAll('#090909','#000000').replaceAll('#ff1735','#ffffff').replace('线条<input','高光<input').replace('平行曲线重绘的数字，固定字位，支持颜色与横竖布局。','参考折带视觉重新设计；渐变弧面、固定字位，非原图字形的精确复制。').replace('import {drawFoldClock}','import {drawFoldClock,loadFoldFont}').replace("const $=id",'await loadFoldFont();const $=id');fs.writeFileSync('fold-preview.html',preview);
const path='src/manifest.cjs';let manifest=fs.readFileSync(path,'utf8');if(!manifest.includes("UUID:'foldclock'"))manifest=manifest.replace('// !! Do not modify below',`Actions.push({UUID:'foldclock',Icon:'images/icon.svg',i18n:Object.fromEntries(Object.keys(Plugin.i18n).map(lang=>[lang,{Name:lang.startsWith('zh')?'折光数字时钟':'Folded Light Clock',Tooltip:lang.startsWith('zh')?'弧面折带数字':'Folded gradient digits'}])),Settings:{ActionGeometry:{width:1000,height:340},showSeconds:false,blink:true,bgColor:'#000000',lineColor:'#ffffff'},States:[],UserTitleEnabled:false,SupportedInMultiActions:false,Controllers:['SecondaryScreen']});
// !! Do not modify below`);fs.writeFileSync(path,manifest);