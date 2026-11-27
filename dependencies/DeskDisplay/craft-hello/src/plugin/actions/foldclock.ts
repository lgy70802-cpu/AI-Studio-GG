import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {drawFoldClock,foldKey,loadFoldFont} from '@/fold-clock';
export default function(name:string){
 const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name,instances=new Map<string,{canvas:HTMLCanvasElement,key:string,ready:boolean}>();
 function draw(context:string,force=false){const a=plugin.getAction(context),st=instances.get(context);if(!a||!st||!st.ready)return;const now=new Date(),key=foldKey(a.settings,now);if(!force&&key===st.key)return;try{drawFoldClock(st.canvas,a.settings,now);a.setImage(st.canvas.toDataURL('image/png'));st.key=key;}catch(e){console.error(JSON.stringify({event:'fold_clock_render_error',context,error:String(e)}));}}
 useWatchEvent('action',{ActionID,async willAppear({context}){plugin.Unterval(ActionID+context);instances.set(context,{canvas:document.createElement('canvas'),key:'',ready:false});const current=instances.get(context);try{await loadFoldFont();}catch(error){console.error(JSON.stringify({event:"fold_font_error",error:String(error)}));return;}if(instances.get(context)!==current)return;current!.ready=true;draw(context,true);plugin.Interval(ActionID+context,100,()=>draw(context));},didReceiveSettings({context}){draw(context,true)},willDisappear({context}){plugin.Unterval(ActionID+context);instances.delete(context)}});
}
