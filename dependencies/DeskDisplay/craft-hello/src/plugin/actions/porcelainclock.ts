import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {drawPorcelain,porcelainKey,loadPorcelainDial} from '@/porcelain';
export default function(name:string){const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name,states=new Map<string,any>();
function draw(context:string,force=false){const a=plugin.getAction(context),st=states.get(context);if(!a||!st)return;const now=new Date(),key=porcelainKey(a.settings,now);if(!force&&key===st.key)return;try{drawPorcelain(st.canvas,a.settings,now);a.setImage(st.canvas.toDataURL('image/jpeg',.9));st.key=key;}catch(e){console.error(JSON.stringify({event:'porcelain_render_error',context,error:String(e)}));}}
useWatchEvent('action',{ActionID,willAppear({context}){plugin.Unterval(ActionID+context);states.set(context,{canvas:document.createElement('canvas'),key:''});const instance=states.get(context);loadPorcelainDial().then(()=>{if(states.get(context)===instance)draw(context,true);});draw(context,true);plugin.Interval(ActionID+context,100,()=>draw(context));},didReceiveSettings({context}){draw(context,true)},willDisappear({context}){plugin.Unterval(ActionID+context);states.delete(context)}});
}
