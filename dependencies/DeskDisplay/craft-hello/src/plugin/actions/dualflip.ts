import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {DualFlipRenderer,DualFlipState,clockPairs,loadDualFlipFont} from '@/dual-flip';
export default function(name:string){
 const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name,states=new Map<string,any>();
 async function start(context:string){stop(context);const st={renderer:new DualFlipRenderer(),clock:new DualFlipState(),ready:false,busy:false,last:'',token:undefined};states.set(context,st);try{await loadDualFlipFont();if(states.get(context)!==st)return;st.ready=true;tick(context);plugin.Interval(ActionID+context,33,()=>tick(context));}catch(e){console.error(JSON.stringify({event:'dual_flip_font_error',error:String(e)}));}}
 function stop(context:string){plugin.Unterval(ActionID+context);states.delete(context);}
 function tick(context:string){
  const st=states.get(context),a=plugin.getAction(context);if(!st?.ready||!a||st.busy)return;const s=a.settings as any,now=performance.now();st.clock.update(clockPairs(new Date(),s.hour12===true),now,s);
  if(st.token===undefined)st.token=s.demoToken||0;if(st.token!==(s.demoToken||0)){st.token=s.demoToken||0;st.clock.demo(now,s);}
  const p=st.clock.progress(now,s),dateKey=new Date().toDateString(),key=JSON.stringify(s)+st.clock.revision+':'+p+':'+dateKey;if(st.last===key)return;
  const size=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.round(Number(v)):f,canvas=st.renderer.draw(st.clock.from,st.clock.to,p,size(s.ActionGeometry?.width,1420),size(s.ActionGeometry?.height,420),s.showDate!==false);
  if(s.transport==='binary'){st.busy=true;canvas.toBlob(async blob=>{try{if(blob&&states.get(context)===st){const data=await blob.arrayBuffer();if(states.get(context)===st){a.setImageBlob(data,'image/png');st.last=key;}}}finally{st.busy=false;}},'image/png');}
  else try{a.setImage(canvas.toDataURL('image/jpeg',.92));st.last=key;}catch(e){console.error(JSON.stringify({event:'dual_flip_send_error',error:String(e)}));}
 }
 useWatchEvent('action',{ActionID,willAppear:({context})=>start(context),willDisappear:({context})=>stop(context),didReceiveSettings:({context})=>tick(context)});
}
