import {usePluginStore,useWatchEvent} from '@/hooks/plugin';
import {drawRibbonClock,ribbonKey} from '@/ribbon-clock';
export default function(name:string){
 const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name,instances=new Map<string,{canvas:HTMLCanvasElement,key:string}>();
 function draw(context:string,force=false){const a=plugin.getAction(context),st=instances.get(context);if(!a||!st)return;const now=new Date(),key=ribbonKey(a.settings,now);if(!force&&key===st.key)return;try{drawRibbonClock(st.canvas,a.settings,now);a.setImage(st.canvas.toDataURL('image/png'));st.key=key;}catch(e){console.error(JSON.stringify({event:'ribbon_clock_render_error',context,error:String(e)}));}}
 useWatchEvent('action',{ActionID,willAppear({context}){plugin.Unterval(ActionID+context);instances.set(context,{canvas:document.createElement('canvas'),key:''});draw(context,true);plugin.Interval(ActionID+context,100,()=>draw(context));},didReceiveSettings({context}){draw(context,true)},willDisappear({context}){plugin.Unterval(ActionID+context);instances.delete(context)}});
}
