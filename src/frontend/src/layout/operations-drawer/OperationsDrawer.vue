<script setup lang="ts">
import MaterialIcon from '../../shared/components/material-icon/MaterialIcon.vue';
import { useLanguageStore } from '../../core/i18n/language.store';
import { useExplorerPreferences } from '../../core/preferences/explorer-preferences.store';
const i18n = useLanguageStore(); const preferences = useExplorerPreferences();
</script>
<template>
<section v-if="preferences.operationsOpen" id="operations-drawer" class="operations-drawer" :aria-label="i18n.t('operations')">
<div class="section-heading">
<h2>{{ i18n.t('operations') }}</h2>
<button type="button" class="icon-action" :aria-label="i18n.t('closeOperations')" @click="preferences.operationsOpen = false"><MaterialIcon name="close" /></button>
</div>
<div class="operations-empty">
<MaterialIcon name="history" />
<div>
<strong>{{ i18n.t('noOperations') }}</strong>
<p>{{ i18n.t('operationsUpcoming') }}</p>
</div>
</div>
</section>
</template>
