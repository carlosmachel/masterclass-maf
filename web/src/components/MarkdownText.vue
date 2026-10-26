<script setup lang="ts">
import { computed } from 'vue'
import { marked } from 'marked'
import DOMPurify from 'dompurify'

// The agent's answer in Markdown. Renders on every streaming delta (partial Markdown is tolerated)
// and goes through DOMPurify: the text comes from the model, so it's treated as untrusted.
const props = defineProps<{ text: string; narration?: boolean }>()

const html = computed(() =>
  DOMPurify.sanitize(marked.parse(props.text, { async: false, gfm: true, breaks: true })),
)
</script>

<template>
  <!-- eslint-disable-next-line vue/no-v-html -- HTML sanitized by DOMPurify -->
  <div class="md" :class="{ 'md--narration': narration }" v-html="html" />
</template>

<style scoped>
.md {
  max-width: 100%;
  font-family: var(--font-text);
  font-size: 17px;
  line-height: 1.65;
  overflow-wrap: anywhere;
}

/* Text between tools ("Vou consultar..."): discreet, as in the Claude chat. */
.md--narration {
  font-size: 15px;
  line-height: 1.5;
  color: var(--p-text-muted-color);
}

.md :deep(> :first-child) {
  margin-top: 0;
}

.md :deep(> :last-child) {
  margin-bottom: 0;
}

.md :deep(p) {
  margin: 0 0 0.75em;
}

.md :deep(h1),
.md :deep(h2),
.md :deep(h3),
.md :deep(h4) {
  margin: 1.1em 0 0.4em;
  font-family: var(--font-ui);
  font-weight: 600;
  line-height: 1.3;
}

.md :deep(h1) {
  font-size: 1.35em;
}

.md :deep(h2) {
  font-size: 1.2em;
}

.md :deep(h3),
.md :deep(h4) {
  font-size: 1.05em;
}

.md :deep(ul),
.md :deep(ol) {
  margin: 0 0 0.75em;
  padding-left: 1.4em;
}

.md :deep(li) {
  margin: 0.25em 0;
}

.md :deep(li::marker) {
  color: var(--p-text-muted-color);
}

.md :deep(strong) {
  font-weight: 650;
}

.md :deep(code) {
  padding: 0.1em 0.35em;
  font-family: var(--font-mono);
  font-size: 0.85em;
  background: var(--p-content-hover-background);
  border-radius: 0;
}

.md :deep(pre) {
  padding: 10px 12px;
  overflow-x: auto;
  background: var(--p-surface-900);
  border-radius: 0;
}

.md :deep(pre code) {
  padding: 0;
  background: none;
}

.md :deep(table) {
  margin: 0 0 0.75em;
  font-family: var(--font-ui);
  font-size: 0.85em;
  border-collapse: collapse;
}

.md :deep(th),
.md :deep(td) {
  padding: 6px 10px;
  text-align: left;
  border-bottom: 1px solid var(--p-content-border-color);
}

.md :deep(blockquote) {
  margin: 0 0 0.75em;
  padding-left: 12px;
  color: var(--p-text-muted-color);
  border-left: 3px solid var(--p-content-border-color);
}

/* Document citations come in italics: (_ata-....md_). */
.md :deep(em) {
  font-size: 0.9em;
  color: var(--p-text-muted-color);
}

.md :deep(a) {
  color: var(--p-primary-color);
}
</style>
