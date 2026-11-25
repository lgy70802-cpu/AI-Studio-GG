<script setup lang="ts">
import { usePropertyStore } from '@/hooks/property';
import { defaults, themes } from '@/settings';
import {timeFonts} from '@/fonts';
const property=usePropertyStore();
const toggles={showSeconds:'显示秒',showDate:'显示日期',showWeekday:'显示星期',hour12:'12 小时制',transparent:'透明背景'};
function checked(key:string){return property.settings[key]??defaults[key];}
function toggle(key:string,event:Event){property.settings[key]=(event.target as HTMLInputElement).checked;}
function theme(key:string){const t=themes[key];Object.assign(property.settings,{bgColor:t.bgColor,textColor:t.textColor});}
function reset(){Object.assign(property.settings,defaults);}
</script>
<template><section class="settings"><h3>你好，D92</h3><p>修改后自动保存 · 窄画布自动竖排</p>
<label>显示文字<input :value="property.settings.text??defaults.text" @input="property.settings.text=($event.target as HTMLInputElement).value" maxlength="60"/></label>
<label>时间字体<select :value="property.settings.timeFont??defaults.timeFont" @change="property.settings.timeFont=($event.target as HTMLSelectElement).value"><option v-for="(font,key) in timeFonts" :value="key">{{font.label}}</option></select></label>
<div class="themes"><button v-for="(t,key) in themes" @click="theme(key)">{{t.name}}</button></div>
<label v-for="(title,key) in toggles">{{title}}<input type="checkbox" :checked="checked(key)" @change="toggle(key,$event)"/></label>
<label>背景颜色<input type="color" :disabled="checked('transparent')" :value="property.settings.bgColor??defaults.bgColor" @input="property.settings.bgColor=($event.target as HTMLInputElement).value"/></label>
<label>文字颜色<input type="color" :value="property.settings.textColor??defaults.textColor" @input="property.settings.textColor=($event.target as HTMLInputElement).value"/></label>
<button @click="reset">恢复默认样式与内容</button><p>恢复默认保留组件的位置和尺寸。</p></section></template>
<style scoped>
.settings{padding:16px;color:#dce8ed;font:13px 'Microsoft YaHei',sans-serif}h3{color:#72e4cc}p{opacity:.65;line-height:1.8}label{display:flex;align-items:center;justify-content:space-between;gap:12px;margin:16px 0}input,button,select{min-width:0;max-width:65%;background:#18242c;color:white;border:1px solid #40505b;border-radius:5px;padding:6px}input[type=color]{width:60px;height:32px;padding:2px}input[type=checkbox]{accent-color:#72e4cc}button{cursor:pointer}.themes{display:flex;gap:6px}input:disabled{opacity:.4}
</style>
