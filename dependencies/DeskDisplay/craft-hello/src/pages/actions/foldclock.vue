<script setup lang="ts">
import {usePropertyStore} from '@/hooks/property';import {foldDefaults} from '@/fold-clock';
const property=usePropertyStore();const toggles={showSeconds:'显示秒',blink:'冒号闪烁'};
</script><template><section><h3>折光数字时钟</h3><p>弧面折带 · 黑白渐变 · 固定字位</p>
<label v-for="(label,key) in toggles">{{label}}<input type="checkbox" :checked="property.settings[key]??foldDefaults[key]" @change="property.settings[key]=($event.target as HTMLInputElement).checked"></label>
<label>背景颜色<input type="color" :value="property.settings.bgColor??foldDefaults.bgColor" @input="property.settings.bgColor=($event.target as HTMLInputElement).value"></label>
<label>高光颜色<input type="color" :value="property.settings.lineColor??foldDefaults.lineColor" @input="property.settings.lineColor=($event.target as HTMLInputElement).value"></label>
<button @click="Object.assign(property.settings,foldDefaults)">恢复默认</button><p>窄画布自动按时、分、秒分行排列。</p></section></template><style scoped>section{padding:16px;color:#eee;font:13px 'Microsoft YaHei',sans-serif}label{display:flex;justify-content:space-between;gap:12px;margin:16px 0}p{opacity:.65;line-height:1.8}button{padding:7px;background:#352d3a;color:white;border:1px solid #74677d;border-radius:5px}</style>
