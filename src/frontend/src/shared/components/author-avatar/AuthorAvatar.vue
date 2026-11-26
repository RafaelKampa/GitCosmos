<script setup lang="ts">
import { computed, ref, watch } from 'vue';

const props = defineProps<{ name: string; email: string }>();
const source = ref('');
const initials = computed(() => props.name.trim().split(/\s+/u).filter(Boolean).slice(0, 2).map(part => Array.from(part)[0]).join('').toLocaleUpperCase() || '?');
watch(() => props.email, async (email, _, onCleanup) => {
  let active = true;
  onCleanup(() => { active = false; });
  source.value = '';
  const normalized = email.trim().toLowerCase();
  if (!normalized) return;
  try {
    const digest = await globalThis.crypto.subtle.digest('SHA-256', new TextEncoder().encode(normalized));
    const hash = Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, '0')).join('');
    if (active) source.value = `https://www.gravatar.com/avatar/${hash}?s=48&d=404&r=g`;
  } catch { /* Initials remain available offline or without Web Crypto. */ }
}, { immediate: true });
</script>

<template>
  <span class="author-avatar" aria-hidden="true">
    <img v-if="source" :src="source" alt="" width="24" height="24" loading="lazy" decoding="async" referrerpolicy="no-referrer" @error="source = ''" />
    <span v-else>{{ initials }}</span>
  </span>
</template>

<style scoped>
.author-avatar { display: inline-flex; width: 24px; height: 24px; flex: 0 0 24px; border-radius: 50%; overflow: hidden; align-items: center; justify-content: center; background: var(--gc-info-bg); color: var(--gc-info); font-size: .65rem; font-weight: 600; }
img { display: block; width: 100%; height: 100%; object-fit: cover; }
</style>
