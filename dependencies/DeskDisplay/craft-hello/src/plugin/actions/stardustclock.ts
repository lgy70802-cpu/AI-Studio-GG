import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {StardustRenderer,StardustState,timeDigits} from '@/stardust';
export default function(name:string){
 const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name,instances=new Map<string,any>();
 function tick(context:string){const a=plugin.getAction(context),st=instances.get(context);if(!a||!st||st.busy)return;
 const s=a.settings as any,now=performance.now();st.state.update(timeDigits(),now);
 if(st.token!==(s.demoToken||0)){st.token=s.demoToken||0;st.state.demo(now);}
 const p=st.state.progress(now,s),phase=s.drift===false?0:Math.floor(Date.now()/100),key=JSON.stringify(s)+st.state.revision+':'+p+':'+phase;if(key===st.last)return;
 const dim=(v:any,f:number)=>Number.isFinite(Number(v))&&Number(v)>0?Math.max(1,Math.round(Number(v))):f;
 const canvas=st.renderer.draw(st.state.from,st.state.to,p,dim(s.ActionGeometry?.width,1000),dim(s.ActionGeometry?.height,420),phase*100);
 st.busy=true;canvas.toBlob(async blob=>{try{if(!blob||instances.get(context)!==st)return;const buffer=await blob.arrayBuffer();if(instances.get(context)!==st)return;const bytes=new Uint8Array(buffer);let raw='';for(let i=0;i<bytes.length;i+=8192)raw+=String.fromCharCode(...bytes.subarray(i,i+8192));a.setImage('data:image/png;base64,'+btoa(raw));st.last=key;}catch(e){console.error(JSON.stringify({event:'stardust_send_error',error:String(e)}));}finally{st.busy=false;}},'image/png');
 }
 useWatchEvent('action',{ActionID,willAppear({context}){plugin.Unterval(ActionID+context);const a=plugin.getAction(context);instances.set(context,{renderer:new StardustRenderer(),state:new StardustState(),busy:false,last:'',token:(a?.settings as any)?.demoToken||0});tick(context);plugin.Interval(ActionID+context,40,()=>tick(context));},didReceiveSettings({context}){tick(context)},willDisappear({context}){plugin.Unterval(ActionID+context);instances.delete(context)}});
}
