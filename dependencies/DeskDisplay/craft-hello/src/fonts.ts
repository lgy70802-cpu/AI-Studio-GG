
export const timeFonts = {
 montserrat:{label:'Montserrat',family:'ClockMontserrat',weight:600,file:'montserrat.ttf'},
 barlow:{label:'Barlow ExtraBold',family:'ClockBarlow',weight:800,file:'barlow.ttf'},
 comfortaa:{label:'Comfortaa',family:'ClockComfortaa',weight:600,file:'comfortaa.ttf'},
 bahnschrift:{label:'Bahnschrift',family:'Bahnschrift',weight:600,file:''},
 manrope:{label:'Manrope',family:'ClockManrope',weight:600,file:'manrope.ttf'},
 consolas:{label:'Consolas（原字体）',family:'Consolas',weight:600,file:''}
};
export function fontSpec(key:string){return timeFonts[key]||timeFonts.consolas;}
const loads=new Map<string,Promise<void>>();
export function ensureTimeFont(key:string):Promise<void>{
 const spec=fontSpec(key);if(!spec.file)return Promise.resolve();
 if(!loads.has(key))loads.set(key,(async()=>{
 const face=new FontFace(spec.family,'url("'+new URL('fonts/'+spec.file,document.baseURI).href+'")',{weight:spec.file==='barlow.ttf'?'800':'100 900'});
 document.fonts.add(await face.load());
 })());return loads.get(key)!;
}
