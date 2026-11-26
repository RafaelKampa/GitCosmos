import { defineStore } from 'pinia';
import { ref, watch } from 'vue';
const defaults = { changesWidth: 30, changesHeight: 0, unstaged: 50, historyGraph: 0, historyDetails: 0, commitTreeWidth: 30 };
type PanelSizes = typeof defaults;
const bounds: Record<keyof PanelSizes, [number, number]> = { changesWidth: [20, 70], changesHeight: [200, 2400], unstaged: [15, 85], historyGraph: [120, 2000], historyDetails: [160, 2400], commitTreeWidth: [15, 70] };
function saved(): PanelSizes {
  const sizes = { ...defaults };
  try {
    const value = JSON.parse(localStorage.getItem('gitcosmos.panelSizes') || '{}');
    for (const key of Object.keys(defaults) as (keyof PanelSizes)[]) {
      const candidate = value?.[key]; const [min, max] = bounds[key];
      if (typeof candidate === 'number' && Number.isFinite(candidate) && candidate >= min && candidate <= max) sizes[key] = candidate;
    }
  } catch { /* Keep usable defaults if storage is unavailable or malformed. */ }
  return sizes;
}
export const usePanelLayout = defineStore('panel-layout', () => {
  const sizes = ref(saved());
  watch(sizes, value => { try { localStorage.setItem('gitcosmos.panelSizes', JSON.stringify(value)); } catch { /* Resizing remains available without storage. */ } }, { deep: true });
  return { sizes };
});
