import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {MonoFlipRenderer,MonoFlipState,clockDigits,loadFlipFont,colonIsLit} from '@/mono-flip';
export default function(name:string){
 const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name;
 const states=new Map<string,any>();
 async function start(context:string){stop(context);const st={renderer:new MonoFlipRenderer(),clock:new MonoFlipState(),ready:false,busy:false,last:'',settings:'',token:undefined};states.set(context,st);
 try{await loadFlipFont();if(states.get(context)!==st)return;st.ready=true;tick(context);plugin.Interval(ActionID+context,33,()=>tick(context));}catch(e){console.error(JSON.stringify({event:'flip_font_error',error:String(e)}));}}
 function stop(context:string){plugin.Unterval(ActionID+context);states.delete(context);}
 function tick(context:string){
  const st=states.get(context),a=plugin.getAction(context);if(!st?.ready||!a||st.busy)return;
  const s=a.settings as any,now=performance.now();st.clock.update(clockDigits(),now,s);
  if(st.token===undefined)st.token=s.demoToken||0;
  if(st.token!==(s.demoToken||0)){st.token=s.demoToken||0;st.clock.demo(now,s);}
  const lit=s.showDate!==false;const p=st.clock.progress(now,s),key=JSON.stringify(s)+st.clock.revision+':'+p+':'+lit;
  if(st.last===key)return;
  const size=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.round(Number(v)):f;
  const canvas=st.renderer.draw(st.clock.from,st.clock.to,p,size(s.ActionGeometry?.width,1500),size(s.ActionGeometry?.height,560),lit);
  if(s.transport==='binary'){st.busy=true;canvas.toBlob(async blob=>{try{if(blob&&states.get(context)===st){const data=await blob.arrayBuffer();if(states.get(context)===st){a.setImageBlob(data,'image/png');st.last=key;}}}finally{st.busy=false;}},'image/png');}
  else{try{a.setImage(canvas.toDataURL('image/png'));st.last=key;}catch(e){console.error(JSON.stringify({event:'flip_send_error',error:String(e)}));}}
 }
 useWatchEvent('action',{ActionID,willAppear:({context})=>{start(context)},willDisappear:({context})=>stop(context),didReceiveSettings:({context})=>tick(context)});
}
