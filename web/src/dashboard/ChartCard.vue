<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useTemplateRef } from 'vue'
import VueApexCharts from 'vue3-apexcharts'
import type { ApexOptions } from 'apexcharts'
import type { Chart } from './types'

const props = defineProps<{ chart: Chart }>()

// ApexCharts only re-measures on window resize. Here the card changes width when another chart joins
// the same grid row; the ResizeObserver passes the real width to the component, which redraws.
const card = useTemplateRef<HTMLElement>('card')
const width = ref<number>()
let observer: ResizeObserver | undefined
onMounted(() => {
  observer = new ResizeObserver(([entry]) => {
    const w = Math.floor(entry!.contentRect.width)
    if (w > 0 && w !== width.value) width.value = w
  })
  if (card.value) observer.observe(card.value)
})
onBeforeUnmount(() => observer?.disconnect())

const RED = '#f87171'
const GREEN = '#4ade80'
const AMBER = '#fbbf24'
const TEXT = '#c3cdef'

const series = computed(() =>
  props.chart.type === 'line'
    ? props.chart.series.map((s) => ({
        name: s.name,
        data: s.points.map((p) => ({ x: new Date(p.x).getTime(), y: p.y })),
      }))
    : props.chart.series.map((s) => ({
        name: s.name,
        data: s.points.map((p) => ({ x: p.x, y: p.y })),
      })),
)

const common: ApexOptions = {
  chart: {
    background: 'transparent',
    foreColor: TEXT,
    fontFamily: "'Atkinson Hyperlegible Next', 'Helvetica Neue', Arial, sans-serif",
    toolbar: { show: false },
    zoom: { enabled: false },
    animations: { enabled: true, speed: 500 },
  },
  theme: { mode: 'dark' },
  grid: { borderColor: 'rgba(185, 214, 255, 0.22)', strokeDashArray: 3 },
  tooltip: { theme: 'dark' },
  legend: { labels: { colors: TEXT }, fontSize: '13px' },
}

const lineOptions = computed<ApexOptions>(() => {
  const { band, threshold } = props.chart
  return {
    ...common,
    chart: { ...common.chart, type: 'line' },
    colors: [AMBER, RED],
    stroke: { width: [3, 1.5], curve: 'smooth', dashArray: [0, 4] },
    xaxis: {
      type: 'datetime',
      labels: { datetimeUTC: false, format: 'dd/MM' },
    },
    yaxis: {
      // "-14°" in the label instead of a rotated title; || 0 avoids "-0".
      labels: { formatter: (v: number) => `${Math.round(v) || 0}°` },
      min: (min: number) => Math.min(min, band?.min ?? min) - 1,
      max: (max: number) => Math.max(max, threshold ?? max) + 1,
    },
    tooltip: {
      theme: 'dark',
      x: { format: 'dd/MM HH:mm' },
      y: { formatter: (v: number) => `${v.toFixed(1)} °C` },
    },
    annotations: {
      yaxis: [
        ...(band
          ? [
              {
                y: band.min,
                y2: band.max,
                fillColor: GREEN,
                opacity: 0.12,
                borderColor: 'transparent',
                label: {
                  text: 'faixa ideal',
                  position: 'left',
                  textAnchor: 'start',
                  style: { color: '#052e16', background: GREEN },
                },
              },
            ]
          : []),
        ...(threshold != null
          ? [
              {
                y: threshold,
                borderColor: RED,
                strokeDashArray: 6,
                label: {
                  text: `${threshold} °C · crítico`,
                  style: { color: '#450a0a', background: RED },
                },
              },
            ]
          : []),
      ],
    },
  }
})

const barOptions = computed<ApexOptions>(() => ({
  ...common,
  chart: { ...common.chart, type: 'bar' },
  legend: { show: false },
  plotOptions: {
    bar: {
      horizontal: true,
      borderRadius: 4,
      barHeight: '55%',
      // A change above 15% is a warning both ways: a rise without stock is a stockout risk.
      // Color only for severity, as in the KPIs and alerts; the direction comes from the sign in the label.
      colors: { ranges: [{ from: -1000, to: 1000, color: AMBER }] },
    },
  },
  dataLabels: {
    enabled: true,
    formatter: (v: number) => `${v > 0 ? '+' : ''}${v.toFixed(1).replace('.', ',')}%`,
    style: { colors: ['#211450'] },
  },
  xaxis: { labels: { formatter: (v: string) => `${v}%` } },
  yaxis: { labels: { maxWidth: 260, style: { fontSize: '13px' } } },
}))
</script>

<template>
  <article ref="card" class="chart">
    <h3 class="chart__title">{{ chart.title }}</h3>
    <VueApexCharts
      v-if="width && chart.type === 'line'"
      :width="width"
      type="line"
      height="260"
      :options="lineOptions"
      :series="series"
    />
    <VueApexCharts
      v-else-if="width"
      :width="width"
      type="bar"
      :height="Math.max(140, 56 * (chart.series[0]?.points.length ?? 1) + 60)"
      :options="barOptions"
      :series="series"
    />
  </article>
</template>

<style scoped>
.chart {
  min-width: 0; /* grid item: without this the chart overflows the column */
  overflow: hidden;
  padding: 14px 16px 6px;
  background: var(--p-content-background);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
}

.chart__title {
  margin: 0 0 4px;
  font-size: 15px;
  font-weight: 600;
}
</style>
