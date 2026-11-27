import { usePluginStore,useWatchEvent } from '@/hooks/plugin';
import { drawClock } from '@/render';
import {ensureTimeFont} from '@/fonts';
import { frameKey } from '@/settings';
export default function(name:string){
 const plugin=usePluginStore(),ActionID=window.argv[3].plugin.uuid+'.'+name;
 const instances=new Map<string,{canvas:HTMLCanvasElement,key:string}>();
 const pending=new Set<string>();
 const draw=async(context:string,force=false)=>{
  const a=plugin.getAction(context),state=instances.get(context);if(!a||!state||pending.has(context))return;
  const now=new Date(),key=JSON.stringify(a.settings)+frameKey(a.settings,now);if(!force&&state.key===key)return;
  pending.add(context);
  try{await ensureTimeFont((a.settings as any).timeFont);if(instances.get(context)!==state)return;drawClock(state.canvas,a.settings,now);a.setImage(state.canvas.toDataURL('image/png'));state.key=key;}
  catch(error){console.error(JSON.stringify({event:'clock_render_failed',context,error:String(error)}));}finally{pending.delete(context);}
 };
 useWatchEvent('action',{ActionID,
  willAppear({context}){plugin.Unterval(ActionID+context);instances.set(context,{canvas:document.createElement('canvas'),key:''});draw(context,true);plugin.Interval(ActionID+context,200,()=>draw(context));},
  didReceiveSettings({context}){draw(context,true);},
  willDisappear({context}){plugin.Unterval(ActionID+context);instances.delete(context);}
 });
}
