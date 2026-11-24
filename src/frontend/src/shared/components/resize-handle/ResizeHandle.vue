<script setup lang="ts">
import { onUnmounted, ref } from 'vue';
const props = withDefaults(defineProps<{ modelValue: number; axis: 'x' | 'y'; min: number; max: number; label: string; percent?: boolean }>(), { percent: false });
const emit = defineEmits<{ 'update:modelValue': [value: number] }>();
const handle = ref<HTMLElement>();
let start = 0; let initial = 0; let scale = 1; let active: number | undefined;
function update(value: number) { emit('update:modelValue', Math.round(Math.min(props.max, Math.max(props.min, value)) * 100) / 100); }
function size() {
  const rect = handle.value!.previousElementSibling!.getBoundingClientRect();
  return props.axis === 'x' ? rect.width : rect.height;
}
function stop() {
  active = undefined;
  window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', stop); window.removeEventListener('pointercancel', stop);
}
function move(event: PointerEvent) {
  if (event.pointerId !== active) return;
  update(initial + ((props.axis === 'x' ? event.clientX : event.clientY) - start) / scale);
}
function begin(event: PointerEvent) {
  if (event.button !== 0) return;
  event.preventDefault(); stop();
  const rect = handle.value!.parentElement!.getBoundingClientRect();
  scale = props.percent ? Math.max(1, props.axis === 'x' ? rect.width : rect.height) / 100 : 1;
  initial = props.modelValue || size() / scale;
  start = props.axis === 'x' ? event.clientX : event.clientY; active = event.pointerId;
  handle.value!.focus();
  window.addEventListener('pointermove', move); window.addEventListener('pointerup', stop); window.addEventListener('pointercancel', stop);
}
function keydown(event: KeyboardEvent) {
  const negative = props.axis === 'x' ? 'ArrowLeft' : 'ArrowUp'; const positive = props.axis === 'x' ? 'ArrowRight' : 'ArrowDown';
  if (![negative, positive, 'Home', 'End'].includes(event.key)) return;
  event.preventDefault();
  const current = props.modelValue || size();
  const step = props.percent ? 2 : 16;
  update(event.key === 'Home' ? props.min : event.key === 'End' ? props.max : current + (event.key === positive ? step : -step));
}
onUnmounted(stop);
</script>
<template>
  <div ref="handle" class="resize-handle" :class="'resize-' + axis" role="separator" tabindex="0" :aria-label="label" :title="label" :aria-orientation="axis === 'x' ? 'vertical' : 'horizontal'" :aria-valuemin="min" :aria-valuemax="max" :aria-valuenow="modelValue || undefined" @pointerdown="begin" @keydown="keydown" />
</template>
<style scoped>
.resize-handle { position: relative; flex-shrink: 0; touch-action: none; user-select: none; background: transparent; z-index: 2; }
.resize-handle::after { content: ''; position: absolute; border-radius: 2px; background: var(--gc-border); }
.resize-x { width: 8px; min-height: 24px; cursor: col-resize; }
.resize-x::after { width: 2px; height: 100%; left: 3px; top: 0; }
.resize-y { height: 8px; min-width: 24px; cursor: row-resize; }
.resize-y::after { height: 2px; width: 100%; top: 3px; left: 0; }
.resize-handle:hover::after, .resize-handle:focus-visible::after { background: var(--gc-violet); }
.resize-handle:focus-visible { outline: 2px solid var(--gc-violet); outline-offset: -2px; }
</style>
