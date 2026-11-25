<script setup lang="ts">
import {usePropertyStore} from '@/hooks/property';
const property=usePropertyStore();
</script>
<template><section><h3>黑白翻页时钟</h3><p>深灰翻牌 · 六位时分秒 · 本地时间</p>
<label>翻页动画<input type="checkbox" :checked="property.settings.animate!==false" @change="property.settings.animate=($event.target as HTMLInputElement).checked"></label>
<label>翻页时长<select :value="property.settings.duration||400" @change="property.settings.duration=Number(($event.target as HTMLSelectElement).value)"><option :value="250">快速 · 250 毫秒</option><option :value="400">标准 · 400 毫秒</option><option :value="800">慢速 · 800 毫秒</option></select></label>
<button @click="property.settings.demoToken=Date.now()">演示翻页</button><p>短暂展示示例数字，然后恢复当前时间。</p>
<label>显示日期<input type="checkbox" :checked="property.settings.showDate!==false" @change="property.settings.showDate=($event.target as HTMLInputElement).checked"></label><label>图像传输<select :value="property.settings.transport||'compatible'" @change="property.settings.transport=($event.target as HTMLSelectElement).value"><option value="compatible">兼容模式</option><option value="binary">二进制 PNG（实验）</option></select></label>
<p>若二进制模式不显示画面，请切回兼容模式。</p></section></template>
<style scoped>section{padding:16px;color:#e9e5d5;font:13px 'Microsoft YaHei',sans-serif}label{display:flex;justify-content:space-between;gap:10px;margin:18px 0}button,select{background:#292b27;color:#fff;border:1px solid #666;padding:7px;border-radius:5px}button{cursor:pointer}p{opacity:.65;line-height:1.8}</style>
