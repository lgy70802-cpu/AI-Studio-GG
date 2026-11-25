<script setup lang="ts">
  import { usePropertyStore } from '@/hooks/property';
  import { defaults, symbols } from '@/stocks';
  const p = usePropertyStore();
  const error = computed(() => {
    try {
      symbols(p.settings.symbols ?? defaults.symbols);
      if (p.settings.focus && symbols(p.settings.focus).length !== 1) throw Error('聚焦代码只能填写一只');
      return '';
    } catch (e) {
      return (e as Error).message;
    }
  });
</script>
<template>
  <section>
    <h2>A 股行情</h2>
    <p>设置自动保存。行情可能延迟。</p>
    <label
      >显示模式<select :value="p.settings.mode ?? 'list'" @change="p.settings.mode = ($event.target as HTMLSelectElement).value">
        <option value="list">自选列表</option>
        <option value="market">大盘总览</option>
        <option value="detail">个股详情</option>
        <option value="rotate">三页轮播</option>
      </select></label
    >
    <label
      >自选代码（最多 20 个，支持六位沪深股票代码）<textarea
        :value="p.settings.symbols ?? defaults.symbols"
        @change="p.settings.symbols = ($event.target as HTMLTextAreaElement).value"
      />
    </label>
    <p class="error">{{ error }}</p>
    <label
      >聚焦股票（留空使用第一只自选股）<input
        :value="p.settings.focus ?? ''"
        placeholder="600519 或 sh600519"
        @change="p.settings.focus = ($event.target as HTMLInputElement).value.trim().toLowerCase()"
    /></label>
    <label
      >列表排序<select :value="p.settings.sort ?? 'manual'" @change="p.settings.sort = ($event.target as HTMLSelectElement).value">
        <option value="manual">输入顺序</option>
        <option value="gain">涨幅优先</option>
        <option value="loss">跌幅优先</option>
      </select></label
    >
    <label
      >列表字号<select :value="p.settings.fontScale ?? 1" @change="p.settings.fontScale = Number(($event.target as HTMLSelectElement).value)">
        <option :value="0.85">较小</option>
        <option :value="1">标准</option>
        <option :value="1.15">较大</option>
      </select></label
    >
    <label
      >行情更新间隔<select v-model="p.settings.interval">
        <option :value="10">10 秒</option>
        <option :value="30">30 秒</option>
        <option :value="60">60 秒</option>
        <option :value="300">5 分钟</option>
      </select></label
    >
    <label
      >翻页 / 轮播间隔<select v-model="p.settings.pageSeconds">
        <option :value="12">12 秒</option>
        <option :value="20">20 秒</option>
        <option :value="30">30 秒</option>
      </select></label
    >
    <label><input type="checkbox" v-model="p.settings.greenUp" /> 使用绿涨红跌</label>
    <h3>轮播页面</h3>
    <p>固定顺序：各自选列表页 → 大盘总览 → 个股详情。全部取消时回退到自选列表。</p>
    <label
      v-for="[key, title] in [
        ['rotateList', '自选列表'],
        ['rotateMarket', '大盘总览'],
        ['rotateDetail', '个股详情']
      ]"
      ><input type="checkbox" :checked="p.settings[key] !== false" @change="p.settings[key] = ($event.target as HTMLInputElement).checked" />{{ title }}</label
    >
    <label
      v-for="[key, title] in [
        ['listSeconds', '每个列表页'],
        ['marketSeconds', '大盘页'],
        ['detailSeconds', '详情页']
      ]"
      >{{ title }}停留秒数<input
        type="number"
        min="5"
        max="120"
        :value="p.settings[key] ?? p.settings.pageSeconds ?? 12"
        @change="p.settings[key] = Math.max(5, Math.min(120, Number(($event.target as HTMLInputElement).value) || 12))"
    /></label>
    <h3>聚焦股票价格提醒</h3>
    <label><input type="checkbox" v-model="p.settings.alertEnabled" /> 开启屏内提醒</label>
    <label>上穿价格<input type="number" min="0" step="0.01" v-model.number="p.settings.alertAbove" placeholder="留空关闭上穿规则" /></label>
    <label>下穿价格<input type="number" min="0" step="0.01" v-model.number="p.settings.alertBelow" placeholder="留空关闭下穿规则" /></label>
    <p>
      提醒针对聚焦股票。首次读取建立基准，随后跨价才提示；超过 5
      分钟的旧行情不触发。每条规则每天一次，重启插件后重新建立基准。轮询可能错过两次采样之间的跨价，不提供声音或系统通知。
    </p>
    <p>分时最多每分钟更新一次，覆盖所有自选股。断网保留上次行情并标记异常。</p>
  </section>
</template>
<style scoped>
  section {
    padding: 20px;
    color: #eef3fb;
    font:
      14px 'ManropeTabular',
      'Microsoft YaHei',
      sans-serif;
  }
  h2,
  h3 {
    margin-bottom: 16px;
  }
  p {
    color: #91a0b5;
    line-height: 1.8;
    margin: 12px 0;
  }
  label {
    display: block;
    margin: 18px 0;
  }
  textarea,
  select,
  input:not([type='checkbox']) {
    box-sizing: border-box;
    display: block;
    margin-top: 8px;
    width: 100%;
    background: #141d29;
    color: white;
    border: 1px solid #40506a;
    border-radius: 6px;
    padding: 10px;
  }
  textarea {
    height: 90px;
  }
  .error {
    color: #f0ba55;
  }
</style>
