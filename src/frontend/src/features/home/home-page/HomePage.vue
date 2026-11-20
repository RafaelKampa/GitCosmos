<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue';
import Button from 'primevue/button';
import { SystemApiService } from '../../../core/system/system-api.service';
import { useLanguageStore } from '../../../core/i18n/language.store';
import { ApiError } from '../../../core/api/api-error';

const i18n = useLanguageStore();
const api = new SystemApiService(() => i18n.language, i18n.t);
const loading = ref(false);
const message = ref('');
const failed = ref(false);
let controller: AbortController | undefined;

async function refresh() {
  controller?.abort();
  const request = new AbortController();
  controller = request;
  loading.value = true;
  failed.value = false;
  try {
    const response = await api.getStatus(request.signal);
    if (!request.signal.aborted) message.value = response.messages.join(' ');
  } catch (error: unknown) {
    if (!request.signal.aborted) {
      failed.value = true;
      message.value = error instanceof ApiError ? error.message : i18n.t('error');
    }
  } finally {
    if (!request.signal.aborted) loading.value = false;
  }
}

onMounted(refresh);
watch(() => i18n.language, refresh);
onUnmounted(() => controller?.abort());
</script>

<template>
  <section class="hero">
    <p class="eyebrow">{{ i18n.t('foundation') }}</p>
    <h1>{{ i18n.t('title') }}</h1>
    <p class="description">{{ i18n.t('description') }}</p>
    <article class="status-card" :aria-busy="loading">
      <h2>{{ i18n.t('statusTitle') }}</h2>
      <p :role="failed ? 'alert' : 'status'">{{ loading ? i18n.t('loading') : message }}</p>
      <Button :label="i18n.t('retry')" :disabled="loading" @click="refresh" />
    </article>
  </section>
</template>
