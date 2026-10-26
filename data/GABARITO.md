# Gabarito da demo — o que o agente deve descobrir

Período dos dados: **2026-07-11 a 2026-10-08** · 45 freezers · 97,164 leituras · 10,800 linhas de vendas.
As views calculam "agora" a partir do dado mais recente, então a demo funciona em qualquer data.
Mesmo assim, gere os dados de novo na véspera do evento.

## 1. Campinas: freezer degradando (história principal)
- **Dado (SQL):** o FRZ-07-2 aquece de forma gradual nos últimos 10 dias, de cerca de -19,5 °C para cerca de -13 °C, com picos à tarde nos últimos 3 dias. Em `vw_freezer_status` ele aparece como **CRITICO**.
- **Contexto (Foundry IQ):** a ata do Comitê já citava o ruído no compressor e registra que a troca, de R$ 18.400, foi **adiada para o 4º trimestre**. O chamado de ~58 dias atrás foi fechado com "ajuste paliativo".
- **Ação esperada:** antecipar a troca do compressor (o valor passa de R$ 15 mil, então precisa de aprovação do Comitê), transferir os produtos e abrir chamado de alta prioridade. **Momento de aprovação humana.**

## 2. Recife: freezer offline e queda de vendas
- **Dado:** o FRZ-13-1 ficou **sem leituras** de 03/10 17:00 a 05/10 05:00 (36 horas) e voltou acima de -8 °C. Hoje ele está na faixa normal, mas aparece como **ATENCAO** em `vw_freezer_status` por causa da lacuna (`longest_gap_7d_hours`). As vendas de Congelados da loja 13 caíram cerca de 45% nos últimos 5 dias (cerca de 30% na comparação semanal de `vw_weekly_sales`), e o estoque de congelados está quase zerado (produtos descartados). Há um chamado **Aberto**.
- **Contexto:** pelo manual, mais de 6 horas sem leitura deve ser tratado como crítico. A ata já citava instabilidade de energia em Recife e a ação de instalar nobreak.
- **Ação esperada:** cobrar o nobreak e pedir reposição emergencial ao CD Nordeste.

## 3. Curitiba: pico de vendas e risco de ruptura
- **Dado:** as vendas de Bebidas da loja 4 subiram cerca de 70% na última semana, e todos os itens de bebidas estão abaixo do ponto de reposição (`vw_critical_stock`).
- **Contexto:** é o piloto do "Festival de Bebidas" (calendário de promoções). A política exigia estoque reforçado de 2,5 vezes o ponto de reposição, e isso não aconteceu.
- **Ação esperada:** pedido emergencial ao CD Sul. Como o piloto deu certo, vale estender a campanha.

## Perguntas de palco sugeridas
1. "Como está a operação das lojas esta semana?" (o dashboard se monta com as 3 anomalias)
2. "Por que Curitiba vendeu tanta bebida?" (cruza o SQL com o calendário de promoções)
3. "Esse problema de Campinas já era conhecido?" (encontra a ata e a decisão de adiar)
4. "Abre os chamados e os pedidos necessários." (o agente pede aprovação no front)
