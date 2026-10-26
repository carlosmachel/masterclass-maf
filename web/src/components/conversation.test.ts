import { describe, expect, it } from 'vitest'
import type { ChatItem, ToolCallView } from '@/agui/useAgent'
import { groupConversation, summarizeWork } from './conversation'

const tool = (id: string, name: string, startedAt = 0, endedAt?: number, result?: string): ToolCallView => ({
  id,
  name,
  args: '{}',
  status: endedAt === undefined ? 'running' : 'done',
  startedAt,
  endedAt,
  result,
})

describe('groupConversation', () => {
  it('joins narrations and queries in a work block and keeps the answer apart', () => {
    const items: ChatItem[] = [
      { kind: 'text', id: 'u1', role: 'user', text: 'Como está a operação?' },
      { kind: 'text', id: 'n1', role: 'assistant', text: 'Vou consultar os freezers.' },
      { kind: 'tool', id: 't1' },
      { kind: 'text', id: 'empty', role: 'assistant', text: '  ' },
      { kind: 'tool', id: 't2' },
      { kind: 'text', id: 'r1', role: 'assistant', text: 'Três problemas.' },
    ]
    const calls = { t1: tool('t1', 'GetFreezerStatus'), t2: tool('t2', 'GetWeeklySales') }

    expect(groupConversation(items, calls)).toEqual([
      { type: 'user', id: 'u1', text: 'Como está a operação?' },
      {
        type: 'work',
        id: 'n1',
        steps: [
          { type: 'narration', id: 'n1', text: 'Vou consultar os freezers.' },
          { type: 'tool', id: 't1' },
          { type: 'tool', id: 't2' },
        ],
      },
      { type: 'answer', id: 'r1', text: 'Três problemas.' },
    ])
  })

  it('keeps actions out of the block and turns a narration without queries into loose text', () => {
    const items: ChatItem[] = [
      { kind: 'text', id: 'u1', role: 'user', text: 'Abre os chamados.' },
      { kind: 'text', id: 'n1', role: 'assistant', text: 'Vou abrir os chamados.' },
      { kind: 'tool', id: 'a1' },
      { kind: 'tool', id: 'a2' },
    ]
    const calls = { a1: tool('a1', 'OpenMaintenanceTicket'), a2: tool('a2', 'CreateEmergencyOrder') }

    expect(groupConversation(items, calls).map((b) => `${b.type}:${b.id}`)).toEqual([
      'user:u1',
      'narration:n1',
      'action:a1',
      'action:a2',
    ])
  })

  it('keeps the conversation with the DC agent (A2A) in sight, outside the block', () => {
    const items: ChatItem[] = [
      { kind: 'tool', id: 't1' },
      { kind: 'tool', id: 'dc' },
      { kind: 'tool', id: 't2' },
    ]
    const calls = {
      t1: tool('t1', 'GetTemperatureHistory'),
      dc: tool('dc', 'RequestDcProposal'),
      t2: tool('t2', 'ListMaintenanceTickets'),
    }
    expect(groupConversation(items, calls).map((b) => `${b.type}:${b.id}`)).toEqual([
      'work:t1',
      'action:dc',
      'work:t2',
    ])
  })

  it('a dashboard event separates the turns', () => {
    const items: ChatItem[] = [
      { kind: 'event', id: 'e1', text: 'Curitiba removida' },
      { kind: 'tool', id: 't1' },
    ]
    expect(groupConversation(items, { t1: tool('t1', 'GetWeeklySales') }).map((b) => b.type)).toEqual([
      'event',
      'work',
    ])
  })
})

describe('summarizeWork', () => {
  it('counts queries, measures start to end and points to the one running', () => {
    const calls = {
      t1: tool('t1', 'GetFreezerStatus', 1000, 3000),
      t2: tool('t2', 'SearchKnowledgeBase', 2000, 9600, '[{"document":"x.md","blocked":true}]'),
      t3: tool('t3', 'PublishAlerts', 9700),
    }
    const summary = summarizeWork(
      [
        { type: 'tool', id: 't1' },
        { type: 'tool', id: 't2' },
        { type: 'tool', id: 't3' },
      ],
      calls,
      (r) => (r?.includes('blocked') ? ['x.md'] : []),
    )
    expect(summary).toEqual({ queries: 3, seconds: 9, inProgress: 'PublishAlerts', blocked: ['x.md'] })
  })
})
