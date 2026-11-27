const fs=require('fs');
fs.writeFileSync('src/fonts.ts',`
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
`);
function edit(p,fn){fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8')))}
edit('src/render.ts',s=>"import {fontSpec} from './fonts';\n"+s.replace("const family=mono?'Consolas, monospace'","const spec=fontSpec(s.timeFont); const weight=mono?spec.weight:500;\n  const family=mono?spec.family+', sans-serif'").replaceAll("(mono?'600 ':'500 ')","(weight+' ')"));
edit('src/settings.ts',s=>s.replace("text: '你好，D92'","timeFont: 'consolas', text: '你好，D92'"));
edit('src/pages/actions/action1.vue',s=>s.replace("const property=usePropertyStore();","import {timeFonts} from '@/fonts';\nconst property=usePropertyStore();").replace('<div class="themes">',`<label>时间字体<select :value="property.settings.timeFont??defaults.timeFont" @change="property.settings.timeFont=($event.target as HTMLSelectElement).value"><option v-for="(font,key) in timeFonts" :value="key">{{font.label}}</option></select></label>
<div class="themes">`).replace('input,button{','input,button,select{'));
edit('src/plugin/actions/action1.ts',s=>s.replace("import { frameKey }","import {ensureTimeFont} from '@/fonts';\nimport { frameKey }").replace("const draw=(context:string,force=false)=>{","const pending=new Set<string>();\n const draw=async(context:string,force=false)=>{").replace("if(!a||!state)return;","if(!a||!state||pending.has(context))return;").replace("key=frameKey(a.settings,now)","key=JSON.stringify(a.settings)+frameKey(a.settings,now)").replace("try{drawClock", "pending.add(context);\n  try{await ensureTimeFont((a.settings as any).timeFont);if(instances.get(context)!==state)return;drawClock").replace("error:String(error)}));}","error:String(error)}));}finally{pending.delete(context);}"));
edit('preview.html',s=>s.replace('<fieldset id="themes">','<fieldset><label>时间字体<select id="font"></select></label></fieldset><fieldset id="themes">').replace("let settings={...defaults};","import {timeFonts,ensureTimeFont} from '/src/fonts.ts';\nlet settings={...defaults};").replace("const draw=()=>{","let revision=0;const draw=async()=>{const rev=++revision;await ensureTimeFont(settings.timeFont);if(rev!==revision)return;").replace("document.querySelector('#text').value=settings.text;","document.querySelector('#font').value=settings.timeFont;document.querySelector('#text').value=settings.text;").replace("for(const t of Object.values(themes))","for(const [key,font] of Object.entries(timeFonts)){const o=new Option(font.label,key);document.querySelector('#font').add(o)}document.querySelector('#font').onchange=e=>{settings.timeFont=e.target.value;draw()};\nfor(const t of Object.values(themes))"));
