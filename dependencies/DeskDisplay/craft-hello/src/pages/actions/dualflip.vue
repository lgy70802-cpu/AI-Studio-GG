<script setup lang="ts">import {usePropertyStore} from '@/hooks/property';const property=usePropertyStore();</script>
<template><section><h3>双联翻页时钟</h3><p>宽幅小时牌与分钟牌，右侧显示英文日期。</p>
<label>翻页动画<input type="checkbox" :checked="property.settings.animate!==false" @change="property.settings.animate=($event.target as HTMLInputElement).checked"></label>
<label>翻页时长<select :value="property.settings.duration||450" @change="property.settings.duration=Number(($event.target as HTMLSelectElement).value)"><option :value="260">快速 · 260 毫秒</option><option :value="450">标准 · 450 毫秒</option><option :value="800">慢速 · 800 毫秒</option></select></label>
<button @click="property.settings.demoToken=Date.now()">演示翻页</button>
<label>12 小时制<input type="checkbox" :checked="property.settings.hour12===true" @change="property.settings.hour12=($event.target as HTMLInputElement).checked"></label>
<label>显示日期<input type="checkbox" :checked="property.settings.showDate!==false" @change="property.settings.showDate=($event.target as HTMLInputElement).checked"></label>
<label>图像传输<select :value="property.settings.transport||'compatible'" @change="property.settings.transport=($event.target as HTMLSelectElement).value"><option value="compatible">兼容模式</option><option value="binary">二进制 PNG（实验）</option></select></label><p>若二进制模式无画面，请切回兼容模式。</p></section></template>
<style scoped>section{padding:16px;color:#eee;font:13px 'Microsoft YaHei',sans-serif}label{display:flex;justify-content:space-between;align-items:center;gap:12px;margin:17px 0}button,select{background:#262626;color:#fff;border:1px solid #666;padding:7px;border-radius:5px}button{cursor:pointer}p{opacity:.65;line-height:1.7}</style>
