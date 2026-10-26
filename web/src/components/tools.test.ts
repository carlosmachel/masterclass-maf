import { describe, expect, it } from 'vitest'
import { argsSummary, blockedDocuments, duration, prettyJson, toolLabel } from './tools'

describe('tools', () => {
  it('friendly label and fallback for an unknown tool', () => {
    expect(toolLabel('GetTemperatureHistory').name).toBe('Histórico de temperatura')
    expect(toolLabel('new_tool')).toEqual({ name: 'new_tool', icon: 'pi pi-cog' })
  })

  it('summarizes the arguments that identify the call', () => {
    expect(argsSummary('{"freezerId":"FRZ-07-2","days":14}')).toBe('FRZ-07-2')
    expect(argsSummary('{"storeId":4,"category":"Bebidas","justification":"..."}')).toBe('loja 4 · Bebidas')
    expect(argsSummary('{"excludedStoreIds":[4]}')).toBe('sem loja 4')
    expect(argsSummary('{"request":"150 caixas de Congelados para a loja 7"}')).toBe(
      '“150 caixas de Congelados para a loja 7”',
    )
    expect(argsSummary('{"freezerId":"FRZ')).toBe('') // still arriving
  })

  it('duration in ms or s with a decimal comma', () => {
    expect(duration(0, 250)).toBe('250 ms')
    expect(duration(0, 1940)).toBe('1,9 s')
    expect(duration(0)).toBe('')
  })

  it('formats JSON and keeps plain text', () => {
    expect(prettyJson('{"a":1}')).toBe('{\n  "a": 1\n}')
    expect(prettyJson('Tool call invocation rejected.')).toBe('Tool call invocation rejected.')
  })
})

describe('blockedDocuments', () => {
  it('lists the quarantined documents, without repeats', () => {
    const result = JSON.stringify([
      { document: 'calendario-promocoes.md', text: 'ok' },
      { document: 'comunicado.md', text: '[bloqueado]', blocked: true },
      { document: 'comunicado.md', text: '[bloqueado]', blocked: true },
    ])
    expect(blockedDocuments(result)).toEqual(['comunicado.md'])
  })

  it('ignores a result that is not a list of passages', () => {
    expect(blockedDocuments(undefined)).toEqual([])
    expect(blockedDocuments('Tool call invocation rejected.')).toEqual([])
    expect(blockedDocuments('{"blocked":true}')).toEqual([])
  })
})
