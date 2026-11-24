<script setup lang="ts">
defineProps<{ name: string }>();
</script>

<template>
  <span class="material-icon" aria-hidden="true" :data-icon="name"></span>
</template>

<style scoped>
.material-icon { display: inline-block; width: 1em; height: 1em; flex-shrink: 0; font-family: 'Material Icons'; font-weight: normal; font-style: normal; font-size: 1.2em; line-height: 1; letter-spacing: normal; text-transform: none; white-space: nowrap; overflow-wrap: normal; direction: ltr; vertical-align: -.15em; font-feature-settings: 'liga'; -webkit-font-smoothing: antialiased; }
.material-icon::before { content: attr(data-icon); }
</style>
