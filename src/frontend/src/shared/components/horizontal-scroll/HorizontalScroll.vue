<script setup lang="ts">
import MaterialIcon from '../material-icon/MaterialIcon.vue';
import { nextTick, onMounted, onUnmounted, ref, useId, watch } from 'vue';
import { useLanguageStore } from '../../../core/i18n/language.store';

const props = defineProps<{ contentKey: string }>();
const i18n = useLanguageStore();
const id = useId();
const viewport = ref<HTMLElement>(); const content = ref<HTMLElement>();
const left = ref(false); const right = ref(false);
let observer: ResizeObserver | undefined;
function measure() {
  const element = viewport.value;
  if (!element) return;
  left.value = element.scrollLeft > 1;
  right.value = element.scrollLeft + element.clientWidth < element.scrollWidth - 1;
}
function move(direction: number) {
  const element = viewport.value!;
  element.scrollBy({ left: direction * Math.max(80, element.clientWidth * .8), behavior: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
}
watch(() => props.contentKey, async () => { await nextTick(); measure(); });
onMounted(() => {
  measure(); window.addEventListener('resize', measure);
  if (typeof ResizeObserver !== 'undefined') {
    observer = new ResizeObserver(measure);
    observer.observe(viewport.value!); observer.observe(content.value!);
  }
});
onUnmounted(() => { observer?.disconnect(); window.removeEventListener('resize', measure); });
</script>

<template>
  <span class="horizontal-scroll">
    <span :id="id" ref="viewport" class="scroll-viewport" @scroll="measure"><span ref="content" class="scroll-content"><slot /></span></span>
    <button v-if="left" type="button" class="scroll-arrow scroll-left" :aria-controls="id" :aria-label="i18n.t('historyScrollLeft')" :title="i18n.t('historyScrollLeft')" @click.stop="move(-1)"><MaterialIcon name="chevron_left" /></button>
    <button v-if="right" type="button" class="scroll-arrow scroll-right" :aria-controls="id" :aria-label="i18n.t('historyScrollRight')" :title="i18n.t('historyScrollRight')" @click.stop="move(1)"><MaterialIcon name="chevron_right" /></button>
  </span>
</template>

<style scoped>
.horizontal-scroll { display: block; position: relative; min-width: 0; }
.scroll-viewport { display: block; overflow-x: auto; scrollbar-width: none; }
.scroll-viewport::-webkit-scrollbar { display: none; }
.scroll-content { display: block; width: max-content; min-width: 100%; white-space: nowrap; }
.scroll-arrow { position: absolute; top: 0; bottom: 0; width: 23px; padding: 0; border: 1px solid var(--gc-border); border-radius: .2rem; background: var(--gc-surface-raised); color: var(--gc-text); cursor: pointer; font-size: 1rem; line-height: 1; }
.scroll-left { left: 0; }
.scroll-right { right: 0; }
</style>
