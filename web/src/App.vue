<script setup lang="ts">
import { computed, reactive, ref, useTemplateRef, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import { useAgent } from '@/agui/useAgent'
import type { Interrupt } from '@/agui/types'
import MarkdownText from '@/components/MarkdownText.vue'
import ApprovalCard from '@/components/ApprovalCard.vue'
import ToolCallChip from '@/components/ToolCallChip.vue'
import AgentWork from '@/components/AgentWork.vue'
import { groupConversation } from '@/components/conversation'
import { useStickToBottom } from '@/components/useStickToBottom'
import DashboardPanel from '@/dashboard/DashboardPanel.vue'
import { initialState, type DashboardState } from '@/dashboard/types'

// Chat on the left, dashboard on the right. The dashboard is the AG-UI state: the server sends a STATE_DELTA
// every time a data tool finishes (src/Ipe.Agent/Dashboard/DashboardMapping.cs).
const agent = useAgent('/api/agent', initialState())
const input = ref('')

// Debug: opens the panel of every tool call. A browser preference (there may be no storage).
const debug = ref(readDebug())
watch(debug, (v) => {
  try {
    localStorage.setItem('ipe.debug', v ? '1' : '0')
  } catch {
    /* no storage: it only applies to this tab */
  }
})
function readDebug() {
  try {
    return localStorage.getItem('ipe.debug') === '1'
  } catch {
    return false
  }
}

// Demo controls: model (Foundry or local) and Prompt Shields. They live in the AG-UI state, which goes with every run.
const MODELS = [
  { label: 'Foundry · gpt-5-mini', value: 'foundry' },
  { label: 'Local · qwen3 8B', value: 'local' },
]
const model = computed({
  get: () => agent.state.value.model,
  set: (v) => v && (agent.state.value = { ...agent.state.value, model: v }),
})
const promptShields = computed({
  get: () => agent.state.value.security.promptShields,
  set: (v) => (agent.state.value = { ...agent.state.value, security: { promptShields: v } }),
})

// Question, agent work (collapsed), actions and answer: see components/conversation.ts.
const blocks = computed(() => groupConversation(agent.items.value, agent.toolCalls.value))

// The stage questions (data/GABARITO.md), for the empty screen.
const SUGGESTIONS = [
  'Como está a operação das lojas esta semana?',
  'Por que Curitiba vendeu tanta bebida?',
  'Esse problema de Campinas já era conhecido?',
]

// Scrolls to the last message while the agent writes.
useStickToBottom(useTemplateRef<HTMLElement>('conversation'), useTemplateRef<HTMLElement>('content'))

// One decision per approval request; when all of them are decided, the resume goes in a single request.
const decisions = reactive<Record<string, boolean>>({})
watch(agent.interrupts, () => Object.keys(decisions).forEach((k) => delete decisions[k]))

function decide(interrupt: Interrupt, approved: boolean) {
  // The on-call workflow repeats the request (agent + workflow port) with the same toolCallId: one click covers both.
  for (const i of agent.interrupts.value)
    if (i.id === interrupt.id || (i.toolCallId && i.toolCallId === interrupt.toolCallId)) decisions[i.id] = approved
  const pending = agent.interrupts.value.filter((i) => !(i.id in decisions))
  if (pending.length === 0) {
    agent.resolveInterrupts(
      agent.interrupts.value.map((i) => ({ interrupt: i, approved: decisions[i.id]! })),
    )
  }
}

// Approval requests stay in a fixed tray above the input field until the resume. The on-call flow
// repeats the request (agent + workflow port) with the same toolCallId: a single card.
const requests = computed(() => {
  const seen = new Set<string>()
  return agent.interrupts.value.filter((i) => {
    if (!i.toolCallId || !agent.toolCalls.value[i.toolCallId] || seen.has(i.toolCallId)) return false
    seen.add(i.toolCallId)
    return true
  })
})

function awaiting(toolCallId: string) {
  return agent.interrupts.value.some((i) => i.toolCallId === toolCallId && !(i.id in decisions))
}

// Store name for the card: the request's storeId or the freezer's store (FRZ-07-2 → store 7).
function actionStore(toolCallId: string) {
  try {
    const args = JSON.parse(agent.toolCalls.value[toolCallId]?.args || '{}')
    const storeId = args.storeId ?? Number(/^FRZ-(\d+)-/.exec(args.freezerId ?? '')?.[1])
    return storeNames.value[storeId]
  } catch {
    return undefined
  }
}

// Every question (or filter change) starts with an empty progress panel.
function clearProgress() {
  agent.state.value = { ...agent.state.value, progress: {} }
}

// Citation chip on the alert card: asks the agent about the document.
function cite(document: string, alertTitle: string) {
  if (agent.running.value) return
  clearProgress()
  agent.send(`O que o documento ${document} diz sobre "${alertTitle}"?`)
}

// Shared state, client side: the user changes the dashboard filters, the state changes and a run starts
// on its own. The server reads the filters from RunAgentInput.state (DashboardFilters) and the agent reanalyzes.
const removedNames = reactive<Record<number, string>>({})
const storeNames = computed(() => {
  const names: Record<number, string> = { ...removedNames }
  for (const a of agent.state.value.alerts) names[a.storeId] = a.store
  return names
})

// The dashboard filters what it already has right away (DashboardPanel); the reanalysis redoes KPIs and alerts.
function changeFilters(filters: DashboardState['filters'], note: string, reason: string) {
  agent.state.value = { ...agent.state.value, filters, progress: {} }
  agent.sendEvent(
    `[Dashboard] ${reason} Filtros agora: região ${filters.region ?? 'todas'}, lojas excluídas ` +
      `${filters.excludedStoreIds.length ? filters.excludedStoreIds.join(', ') : 'nenhuma'}.`,
    note,
  )
}

function removeStore(storeId: number, store: string) {
  if (agent.running.value) return
  removedNames[storeId] = store
  const f = agent.state.value.filters
  changeFilters(
    { ...f, excludedStoreIds: [...f.excludedStoreIds, storeId] },
    `${store} removida do painel · reanalisando`,
    `O usuário removeu a loja ${storeId} (${store}) do painel.`,
  )
}

function restoreStore(storeId: number) {
  if (agent.running.value) return
  const f = agent.state.value.filters
  const store = storeNames.value[storeId] ?? `Loja ${storeId}`
  changeFilters(
    { ...f, excludedStoreIds: f.excludedStoreIds.filter((id) => id !== storeId) },
    `${store} de volta ao painel · reanalisando`,
    `O usuário trouxe a loja ${storeId} (${store}) de volta ao painel.`,
  )
}

function changeRegion(region: string | null) {
  if (agent.running.value) return
  changeFilters(
    { ...agent.state.value.filters, region },
    `Região: ${region ?? 'todas'} · reanalisando`,
    `O usuário mudou a região do painel para ${region ?? 'todas'}.`,
  )
}

// On-call trigger: a sensor alarm (simulated here by a button) fires the workflow, with nobody asking anything.
// In production, the same POST would come from IoT Hub / Event Grid.
function fireAlarm() {
  if (agent.running.value) return
  clearProgress()
  agent.sendEvent(
    'Sensor FRZ-07-2 (Ipê Cambuí, Campinas): temperatura acima de -15 °C há mais de 2 horas.',
    'Alarme IoT · FRZ-07-2 acima de −15 °C · plantão acionado',
    '/api/oncall',
  )
}

function submit(question = input.value) {
  if (!question.trim() || agent.running.value) return
  clearProgress()
  agent.send(question)
  input.value = ''
}
</script>

<template>
  <main class="page">
    <header class="topbar">
      <h1 class="topbar__brand">
        <span class="topbar__logo" aria-hidden="true"><i class="pi pi-sun" /></span>
        Rede Ipê <span class="topbar__product">Centro de Operações</span>
      </h1>

      <!-- Scene trigger (step 6): a sensor fires the on-call flow, with nobody asking anything. -->
      <Button
        class="topbar__alarm"
        label="Alarme IoT"
        icon="pi pi-bolt"
        severity="warn"
        size="small"
        text
        :disabled="agent.running.value"
        @click="fireAlarm"
      />

      <!-- Stage controls: they change the run (AG-UI state), not the product. -->
      <div class="stage-controls">
        <SelectButton
          v-model="model"
          :options="MODELS"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          :disabled="agent.running.value"
          size="small"
        />
        <label
          class="stage-controls__shields"
          :class="{ 'stage-controls__shields--off': !promptShields }"
          title="Prompt Shields nos documentos da base de conhecimento"
        >
          <i class="pi" :class="promptShields ? 'pi-shield' : 'pi-exclamation-triangle'" />
          Shields
          <ToggleSwitch v-model="promptShields" :disabled="agent.running.value" />
        </label>
        <button
          type="button"
          class="stage-controls__debug"
          :class="{ 'stage-controls__debug--on': debug }"
          :aria-pressed="debug"
          title="Debug: abre os argumentos e resultados de todas as tools"
          @click="debug = !debug"
        >
          <i class="pi pi-code" />
        </button>
      </div>
    </header>

    <div class="stage">
      <aside class="chat">
        <section ref="conversation" class="conversation">
          <div ref="content" class="conversation__content">
            <div v-if="blocks.length === 0" class="welcome">
              <p class="welcome__title">Pergunte sobre a operação das lojas.</p>
              <p class="welcome__text">
                O agente consulta freezers, vendas, estoque e a base de conhecimento, e o painel se
                monta ao lado.
              </p>
              <button
                v-for="suggestion in SUGGESTIONS"
                :key="suggestion"
                type="button"
                class="welcome__suggestion"
                @click="submit(suggestion)"
              >
                <i class="pi pi-arrow-right" />
                {{ suggestion }}
              </button>
            </div>

            <template v-for="block in blocks" :key="block.id">
              <p v-if="block.type === 'user'" class="msg msg--user">{{ block.text }}</p>
              <MarkdownText v-else-if="block.type === 'answer'" :text="block.text" />
              <MarkdownText v-else-if="block.type === 'narration'" :text="block.text" narration />
              <p v-else-if="block.type === 'event'" class="event">
                <i class="pi pi-sliders-h" />
                {{ block.text }}
              </p>
              <AgentWork
                v-else-if="block.type === 'work'"
                :steps="block.steps"
                :tool-calls="agent.toolCalls.value"
                :debug="debug"
              />
              <ToolCallChip
                v-else-if="agent.toolCalls.value[block.id]"
                :call="agent.toolCalls.value[block.id]!"
                :awaiting-approval="awaiting(block.id)"
                :debug="debug"
              />
            </template>

            <Message v-if="agent.error.value" severity="error">{{ agent.error.value }}</Message>
          </div>
        </section>

        <section v-if="requests.length" class="tray" aria-label="Pedidos de aprovação">
          <p class="tray__title">
            <i class="pi pi-shield" />
            {{ requests.length === 1 ? 'Uma ação precisa' : `${requests.length} ações precisam` }} da
            sua aprovação
          </p>
          <div class="tray__list">
            <ApprovalCard
              v-for="request in requests"
              :key="request.id"
              :call="agent.toolCalls.value[request.toolCallId!]!"
              :store-name="actionStore(request.toolCallId!)"
              :decision="decisions[request.id]"
              @decide="(approve) => decide(request, approve)"
            />
          </div>
        </section>

        <form class="composer" @submit.prevent="submit()">
          <InputText
            v-model="input"
            class="composer__field"
            placeholder="Pergunte ao agente…"
          />
          <Button
            type="submit"
            icon="pi pi-arrow-up"
            aria-label="Enviar"
            :loading="agent.running.value"
          />
        </form>
      </aside>
      <section class="dashboard-pane">
        <DashboardPanel
          :state="agent.state.value"
          :working="agent.running.value"
          :names="storeNames"
          @cite="cite"
          @remove-store="removeStore"
          @restore-store="restoreStore"
          @region="changeRegion"
        />
      </section>
    </div>
  </main>
</template>

<style scoped>
.page {
  display: flex;
  flex-direction: column;
  gap: 20px;
  height: 100vh;
  padding: 16px 28px 20px;
}

.stage {
  flex: 1;
  display: grid;
  grid-template-columns: minmax(400px, 34%) 1fr;
  gap: 32px;
  min-height: 0;
}

/* Top bar: brand on the left; scene trigger and stage controls on the right, discreet. */
.topbar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding-bottom: 14px;
  border-bottom: 3px solid var(--p-text-color);
}

.topbar__brand {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0;
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 800;
  letter-spacing: -0.02em;
}

.topbar__logo {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  color: var(--p-surface-950);
  background: var(--p-primary-color);
  border-radius: 0;
}

.topbar__logo i {
  font-size: 16px;
}

.topbar__product {
  font-family: var(--font-mono);
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--p-text-muted-color);
}

.topbar__alarm {
  margin-left: auto;
}

.stage-controls {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 3px 4px 3px 3px;
  background: var(--p-surface-900);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
}

.stage-controls :deep(.p-selectbutton) {
  border-radius: 0;
}

.stage-controls__shields {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 8px 0 12px;
  font-size: 14px;
  color: var(--p-text-muted-color);
  border-left: 1px solid var(--p-content-border-color);
  cursor: pointer;
}

.stage-controls__shields--off {
  color: var(--p-red-300);
}

.stage-controls__debug {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  font: inherit;
  color: var(--p-text-muted-color);
  background: transparent;
  border: 0;
  border-radius: 0;
  cursor: pointer;
}

.stage-controls__debug:hover {
  color: var(--p-text-color);
  background: var(--p-content-hover-background);
}

.stage-controls__debug--on {
  color: var(--p-primary-color);
  background: color-mix(in srgb, var(--p-primary-color) 14%, transparent);
}

/* Chat */
.chat {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
}

.dashboard-pane {
  min-height: 0;
  overflow-y: auto;
  padding-right: 6px;
}

.conversation {
  flex: 1;
  overflow-y: auto;
  padding-right: 6px;
}

.conversation__content {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 14px;
}

.welcome {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
  margin-top: 8vh;
}

.welcome__title {
  margin: 0;
  font-family: var(--font-display);
  font-size: 40px;
  font-weight: 800;
  letter-spacing: -0.025em;
  line-height: 1.05;
}

.welcome__text {
  margin: 0 0 12px;
  font-size: 15px;
  color: var(--p-text-muted-color);
}

.welcome__suggestion {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  padding: 8px 14px;
  font: inherit;
  font-size: 15px;
  color: var(--p-text-color);
  text-align: left;
  background: transparent;
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
  cursor: pointer;
  transition:
    background 0.15s,
    border-color 0.15s;
}

.welcome__suggestion i {
  font-size: 12px;
  color: var(--p-primary-color);
}

.welcome__suggestion:hover {
  background: var(--p-content-hover-background);
  border-color: var(--p-primary-color);
}

.msg {
  margin: 0;
  max-width: 100%;
  white-space: pre-wrap;
}

.msg--user {
  align-self: flex-end;
  max-width: 85%;
  padding: 10px 16px;
  font-weight: 600;
  background: var(--p-surface-900);
  border-left: 3px solid var(--p-primary-color);
  border-radius: 0;
}

.event {
  display: flex;
  align-items: center;
  gap: 8px;
  align-self: center;
  margin: 4px 0;
  padding: 4px 12px;
  font-size: 14px;
  color: var(--p-amber-300);
  border: 1px dashed var(--p-amber-700);
  border-radius: 0;
}

/* Approval tray: fixed above the input, scrolls on its own if there are many requests. */
.tray {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px;
  background: color-mix(in srgb, var(--p-amber-400) 5%, var(--p-surface-950));
  border: 1px solid color-mix(in srgb, var(--p-amber-400) 55%, transparent);
  border-radius: 0;
  animation: rise 0.3s ease;
}

@keyframes rise {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
}

.tray__title {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 4px;
  font-size: 14px;
  font-weight: 600;
  color: var(--p-amber-400);
}

.tray__list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 42vh;
  overflow-y: auto;
}

.composer {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 6px 6px 4px;
  background: var(--p-surface-900);
  border: 1px solid var(--p-content-border-color);
  border-radius: 0;
  transition: border-color 0.15s;
}

.composer:focus-within {
  border-color: var(--p-primary-color);
}

.composer__field {
  flex: 1;
  background: transparent;
  border: 0;
  box-shadow: none;
}
</style>
