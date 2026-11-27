import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {ReferenceRenderer,ReferenceState,timeDigits,loadReference} from '@/reference-clock';
export default function(name:string){
 const starts=new Map<string,object>();const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name,instances=new Map<string,any>();
 function tick(context:string){const a=plugin.getAction(context),st=instances.get(context);if(!a||!st||st.busy)return;
 const s=a.settings as any,now=performance.now();st.state.update(timeDigits(),now);
 if(st.token!==(s.demoToken||0)){st.token=s.demoToken||0;st.state.demo(now);}
 const p=st.state.progress(now,s),phase=s.drift===false?0:Math.floor(Date.now()%1000/500),key=JSON.stringify(s)+st.state.revision+':'+p+':'+phase;if(key===st.last)return;
 const dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const canvas=st.renderer.draw(st.state.from,st.state.to,p,dim(s.ActionGeometry?.width,1000),dim(s.ActionGeometry?.height,420),phase*500);
 try{a.setImage(canvas.toDataURL('image/jpeg',.92));st.last=key;}catch(error){console.error(JSON.stringify({event:'reference_send_error',context,error:String(error)}));}
 }
 useWatchEvent('action',{ActionID,async willAppear({context}){const ticket={};starts.set(context,ticket);try{await loadReference();}catch(error){console.error(JSON.stringify({event:"reference_load_error",error:String(error)}));return;}if(starts.get(context)!==ticket)return;plugin.Unterval(ActionID+context);const a=plugin.getAction(context);instances.set(context,{renderer:new ReferenceRenderer(),state:new ReferenceState(),busy:false,last:'',token:(a?.settings as any)?.demoToken||0});tick(context);plugin.Interval(ActionID+context,40,()=>tick(context));},didReceiveSettings({context}){tick(context)},willDisappear({context}){starts.delete(context);plugin.Unterval(ActionID+context);instances.delete(context)}});
}
