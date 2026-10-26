#!/usr/bin/env python3
"""
Generates the synthetic dataset for the MAF masterclass: "Rede Ipê Supermercados" (a fictitious company).
Identifiers (tables, columns, files) are in English; the data itself (stores, products, documents) is in Portuguese.

Outputs (in --out):
  sql/seed.sql           data (clears the tables, then INSERTs in batches of 1000). The schema and the views
                         come from the EF Core migrations in src/Ipe.Agent/Data/Migrations (apply the migrations, then the seed)
  csv/*.csv              the same data as CSV
  docs/*.md              documents for the Foundry IQ knowledge base (Blob)
  GABARITO.md            the planted anomalies and what the agent should "discover"

Usage:
  python generate_data.py --out ./masterclass-data                  # ends yesterday
  python generate_data.py --out ./masterclass-data --end 2026-11-20 # pins the last day

Run it again the day before the event so that "this week" really is this week.
"""
import argparse, csv, math, os, random
from datetime import date, datetime, timedelta

SEED = 42

STORES = [
    # id, name, city, state, region, manager, area_m2
    (1, "Ipê Paulista", "São Paulo", "SP", "Sudeste", "Marina Duarte", 2400),
    (2, "Ipê Moema", "São Paulo", "SP", "Sudeste", "Rafael Lins", 1800),
    (3, "Ipê Copacabana", "Rio de Janeiro", "RJ", "Sudeste", "Juliana Castro", 1600),
    (4, "Ipê Batel", "Curitiba", "PR", "Sul", "Thiago Moreira", 2100),
    (5, "Ipê Savassi", "Belo Horizonte", "MG", "Sudeste", "Camila Rocha", 1900),
    (6, "Ipê Moinhos", "Porto Alegre", "RS", "Sul", "Eduardo Prates", 1700),
    (7, "Ipê Cambuí", "Campinas", "SP", "Sudeste", "Patrícia Nogueira", 2000),
    (8, "Ipê Asa Sul", "Brasília", "DF", "Centro-Oeste", "Lucas Ferraz", 2200),
    (9, "Ipê Pituba", "Salvador", "BA", "Nordeste", "Aline Santana", 1500),
    (10, "Ipê Aldeota", "Fortaleza", "CE", "Nordeste", "Bruno Araújo", 1600),
    (11, "Ipê Trindade", "Florianópolis", "SC", "Sul", "Fernanda Kuhn", 1400),
    (12, "Ipê Praia do Canto", "Vitória", "ES", "Sudeste", "Gustavo Pimenta", 1300),
    (13, "Ipê Boa Viagem", "Recife", "PE", "Nordeste", "Renata Cavalcanti", 1800),
    (14, "Ipê Setor Bueno", "Goiânia", "GO", "Centro-Oeste", "Diego Martins", 1700),
    (15, "Ipê Umarizal", "Belém", "PA", "Norte", "Larissa Monteiro", 1200),
    (16, "Ipê Adrianópolis", "Manaus", "AM", "Norte", "Henrique Barros", 1400),
    (17, "Ipê Pinheiros", "São Paulo", "SP", "Sudeste", "Beatriz Okada", 1500),
    (18, "Ipê Icaraí", "Niterói", "RJ", "Sudeste", "Vinícius Teles", 1300),
    (19, "Ipê Centro", "Ribeirão Preto", "SP", "Sudeste", "Carolina Freitas", 1600),
    (20, "Ipê Ponta Verde", "Maceió", "AL", "Nordeste", "Mateus Lyra", 1100),
]

CATEGORIES = {
    # category: (average daily revenue per 1000 m2, average unit ticket)
    "Congelados": (4200, 18.0),
    "Laticínios": (5200, 9.5),
    "Bebidas": (7800, 8.0),
    "Hortifruti": (6100, 6.5),
    "Mercearia": (11500, 11.0),
    "Padaria": (3600, 5.5),
}

PRODUCTS = [
    # sku, name, category, price, refrigerated
    ("CG-001", "Pizza congelada calabresa 460g", "Congelados", 24.90, 1),
    ("CG-002", "Lasanha bolonhesa 600g", "Congelados", 21.50, 1),
    ("CG-003", "Sorvete creme 1,5L", "Congelados", 29.90, 1),
    ("CG-004", "Hambúrguer bovino cx 672g", "Congelados", 27.40, 1),
    ("CG-005", "Batata palito pré-frita 1kg", "Congelados", 19.90, 1),
    ("CG-006", "Peito de frango congelado 1kg", "Congelados", 22.90, 1),
    ("CG-007", "Pão de queijo 1kg", "Congelados", 26.50, 1),
    ("LT-001", "Leite integral 1L", "Laticínios", 5.49, 1),
    ("LT-002", "Iogurte natural 170g", "Laticínios", 3.29, 1),
    ("LT-003", "Queijo muçarela fatiado 150g", "Laticínios", 9.90, 1),
    ("LT-004", "Manteiga com sal 200g", "Laticínios", 12.90, 1),
    ("LT-005", "Requeijão cremoso 200g", "Laticínios", 8.49, 1),
    ("LT-006", "Creme de leite 200g", "Laticínios", 3.99, 0),
    ("BB-001", "Água mineral 1,5L", "Bebidas", 2.99, 0),
    ("BB-002", "Refrigerante cola 2L", "Bebidas", 9.49, 0),
    ("BB-003", "Cerveja lata 350ml", "Bebidas", 3.99, 0),
    ("BB-004", "Suco de uva integral 1L", "Bebidas", 14.90, 0),
    ("BB-005", "Energético 250ml", "Bebidas", 8.99, 0),
    ("BB-006", "Café torrado 500g", "Bebidas", 19.90, 0),
    ("BB-007", "Chá gelado limão 1,5L", "Bebidas", 7.49, 0),
    ("HF-001", "Banana prata kg", "Hortifruti", 5.99, 0),
    ("HF-002", "Tomate italiano kg", "Hortifruti", 8.49, 0),
    ("HF-003", "Alface crespa un", "Hortifruti", 3.49, 1),
    ("HF-004", "Batata inglesa kg", "Hortifruti", 4.99, 0),
    ("HF-005", "Maçã gala kg", "Hortifruti", 9.90, 0),
    ("HF-006", "Cebola kg", "Hortifruti", 4.49, 0),
    ("MC-001", "Arroz branco 5kg", "Mercearia", 27.90, 0),
    ("MC-002", "Feijão carioca 1kg", "Mercearia", 8.49, 0),
    ("MC-003", "Azeite extra virgem 500ml", "Mercearia", 34.90, 0),
    ("MC-004", "Macarrão espaguete 500g", "Mercearia", 4.99, 0),
    ("MC-005", "Açúcar refinado 1kg", "Mercearia", 4.79, 0),
    ("MC-006", "Óleo de soja 900ml", "Mercearia", 7.99, 0),
    ("MC-007", "Molho de tomate 340g", "Mercearia", 3.49, 0),
    ("MC-008", "Biscoito recheado 140g", "Mercearia", 3.29, 0),
    ("PD-001", "Pão francês kg", "Padaria", 14.90, 0),
    ("PD-002", "Pão de forma integral", "Padaria", 9.90, 0),
    ("PD-003", "Bolo de cenoura un", "Padaria", 18.90, 0),
    ("PD-004", "Croissant un", "Padaria", 6.50, 0),
]

# ---------------------------------------------------------------- anomalies
# 1) Campinas (store 7): FRZ-07-2 degrading over the last 10 days (compressor),
#    a problem already raised in the committee minutes and in a "stopgap" closed ticket.
# 2) Recife (store 13): FRZ-13-1 went offline for ~36h 5 days ago and came back warm;
#    Frozen food sales dropped hard over the last 5 days.
# 3) Curitiba (store 4): beverage promotion last week -> Beverage sales
#    +70% and beverage stock below the reorder point.
ANOM_CAMPINAS = (7, "FRZ-07-2")
ANOM_RECIFE = (13, "FRZ-13-1")
ANOM_CURITIBA = 4


def sql_val(v):
    if v is None:
        return "NULL"
    if isinstance(v, bool):
        return "1" if v else "0"
    if isinstance(v, (int,)):
        return str(v)
    if isinstance(v, float):
        return repr(round(v, 4))
    if isinstance(v, datetime):
        return "'" + v.strftime("%Y-%m-%dT%H:%M:%S") + "'"
    if isinstance(v, date):
        return "'" + v.isoformat() + "'"
    return "N'" + str(v).replace("'", "''") + "'"


def write_inserts(f, table, cols, rows, batch=1000):
    for i in range(0, len(rows), batch):
        chunk = rows[i:i + batch]
        f.write(f"INSERT INTO dbo.{table} ({', '.join(cols)}) VALUES\n")
        f.write(",\n".join("(" + ", ".join(sql_val(v) for v in r) + ")" for r in chunk))
        f.write(";\n")
    f.write("GO\n\n")


def write_csv(path, cols, rows):
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(cols)
        for r in rows:
            w.writerow([v.isoformat(sep=" ") if isinstance(v, datetime) else v for v in r])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="./masterclass-data")
    ap.add_argument("--end", help="last day of the data (YYYY-MM-DD). Default: yesterday")
    ap.add_argument("--days", type=int, default=90)
    a = ap.parse_args()

    rnd = random.Random(SEED)
    end = date.fromisoformat(a.end) if a.end else date.today() - timedelta(days=1)
    start = end - timedelta(days=a.days - 1)
    days = [start + timedelta(days=i) for i in range(a.days)]
    out = a.out
    for d in ("sql", "csv", "docs"):
        os.makedirs(os.path.join(out, d), exist_ok=True)

    # ------------------------------------------------------------ stores
    store_rows = []
    for (i, name, city, state, region, manager, area) in STORES:
        opened = date(2012 + (i * 7) % 12, 1 + (i * 5) % 12, 1 + (i * 3) % 27)
        store_rows.append((i, name, city, state, region, manager, area, opened))
    store_cols = ["store_id", "name", "city", "state", "region", "manager", "area_m2", "opened_on"]

    # ------------------------------------------------------------ products
    product_rows = [(k + 1, *p) for k, p in enumerate(PRODUCTS)]
    product_cols = ["product_id", "sku", "name", "category", "price", "refrigerated"]

    # ------------------------------------------------------------ daily sales per category
    sales_rows = []
    store_factor = {s[0]: rnd.uniform(0.85, 1.15) for s in STORES}
    for (sid, _n, _c, _st, _r, _m, area) in STORES:
        for d in days:
            dow = d.weekday()
            weekend = {4: 1.12, 5: 1.35, 6: 1.20}.get(dow, 1.0)
            trend = 1 + 0.0006 * (d - start).days
            for cat, (base, ticket) in CATEGORIES.items():
                revenue = base * area / 1000 * store_factor[sid] * weekend * trend * rnd.gauss(1, 0.07)
                days_to_end = (end - d).days
                if sid == ANOM_RECIFE[0] and cat == "Congelados" and days_to_end <= 4:
                    revenue *= rnd.uniform(0.45, 0.6)  # sharp drop
                if sid == ANOM_CURITIBA and cat == "Bebidas" and days_to_end <= 6:
                    revenue *= rnd.uniform(1.6, 1.8)  # promotion
                units = int(revenue / (ticket * rnd.uniform(0.95, 1.05)))
                sales_rows.append((d, sid, cat, round(revenue, 2), units))
    sales_cols = ["sale_date", "store_id", "category", "revenue", "units"]

    # ------------------------------------------------------------ freezers
    freezer_rows = []
    models = ["Fricon HCED-503", "Metalfrio DA550", "Gelopar GHBS-510"]
    for (sid, *_rest) in STORES:
        n = 3 if sid in (1, 4, 7, 8, 13) else 2
        for k in range(1, n + 1):
            fid = f"FRZ-{sid:02d}-{k}"
            installed = date(2019 + (sid + k) % 6, 1 + (sid * k) % 12, 10)
            if fid == ANOM_CAMPINAS[1]:
                installed = date(2018, 3, 12)
            freezer_rows.append((fid, sid, models[(sid + k) % 3], installed, -22.0, -18.0))
    freezer_cols = ["freezer_id", "store_id", "model", "installed_on", "temp_min_c", "temp_max_c"]

    # ------------------------------------------------------------ hourly readings
    reading_rows = []
    end_dt = datetime.combine(end, datetime.min.time()) + timedelta(hours=23)
    start_dt = datetime.combine(start, datetime.min.time())
    hours = int((end_dt - start_dt).total_seconds() // 3600) + 1
    recife_off_start = end_dt - timedelta(days=5, hours=6)
    recife_off_end = recife_off_start + timedelta(hours=36)
    for (fid, sid, *_r) in freezer_rows:
        base = rnd.uniform(-20.3, -19.2)
        for h in range(hours):
            ts = start_dt + timedelta(hours=h)
            t = base + rnd.gauss(0, 0.45)
            if 8 <= ts.hour <= 21 and rnd.random() < 0.08:
                t += rnd.uniform(0.8, 2.0)  # door opening
            hours_to_end = (end_dt - ts).total_seconds() / 3600
            if fid == ANOM_CAMPINAS[1] and hours_to_end <= 240:
                prog = 1 - hours_to_end / 240  # 0 -> 1 over the last 10 days
                t += 6.5 * prog ** 1.4
                if hours_to_end <= 72 and 13 <= ts.hour <= 17:
                    t += rnd.uniform(1.0, 2.5)  # afternoon peaks
            if fid == ANOM_RECIFE[1]:
                if recife_off_start <= ts < recife_off_end:
                    continue  # no readings: equipment offline
                pos = (ts - recife_off_end).total_seconds() / 3600
                if 0 <= pos < 8:
                    t = -7.5 + pos * 1.6 + rnd.gauss(0, 0.3)  # came back warm, recovering
            reading_rows.append((fid, ts, round(t, 2)))
    reading_cols = ["freezer_id", "read_at", "temperature_c"]

    # ------------------------------------------------------------ stock (current snapshot)
    stock_rows = []
    for (sid, *_r) in STORES:
        for (pid, sku, _nm, cat, _p, _rf) in product_rows:
            reorder = rnd.choice([40, 60, 80, 120])
            qty = int(reorder * rnd.uniform(1.3, 3.2))
            if sid == ANOM_CURITIBA and cat == "Bebidas":
                qty = int(reorder * rnd.uniform(0.1, 0.45))
            if sid == ANOM_RECIFE[0] and cat == "Congelados":
                qty = int(reorder * rnd.uniform(0.0, 0.3))  # product discarded
            last = end - timedelta(days=rnd.randint(1, 9))
            stock_rows.append((sid, pid, qty, reorder, last))
    stock_cols = ["store_id", "product_id", "quantity", "reorder_point", "last_restock"]

    # ------------------------------------------------------------ maintenance tickets
    tickets = []
    tid = 1001
    types = ["Ruído anormal", "Porta não veda", "Iluminação interna", "Gelo acumulado", "Revisão preventiva"]
    for _ in range(22):
        f = rnd.choice(freezer_rows)
        if f[0] in (ANOM_CAMPINAS[1], ANOM_RECIFE[1]):
            continue
        opened = start + timedelta(days=rnd.randint(0, a.days - 15))
        closed = opened + timedelta(days=rnd.randint(1, 6))
        tickets.append([tid, f[0], f[1], rnd.choice(types), "Fechado", opened, closed, "Resolvido em visita técnica."])
        tid += 1
    opened_camp = end - timedelta(days=58)
    tickets.append([tid, ANOM_CAMPINAS[1], 7, "Ruído anormal", "Fechado", opened_camp, opened_camp + timedelta(days=3),
                    "Ruído no compressor. Feito ajuste paliativo. Técnico recomenda troca do compressor "
                    "(equipamento de 2018). Troca aguardando aprovação de orçamento."]); tid += 1
    tickets.append([tid, ANOM_RECIFE[1], 13, "Equipamento desligado", "Aberto",
                    recife_off_start.date(), None,
                    "Queda de energia no circuito do freezer. Disjuntor rearmado pela loja; "
                    "aguardando avaliação de perda de produtos."]); tid += 1
    tickets.sort(key=lambda r: r[5])
    ticket_rows = [tuple(r) for r in tickets]
    ticket_cols = ["ticket_id", "freezer_id", "store_id", "type", "status", "opened_on", "closed_on", "description"]

    tables = [
        ("stores", store_cols, store_rows),
        ("products", product_cols, product_rows),
        ("daily_sales", sales_cols, sales_rows),
        ("freezers", freezer_cols, freezer_rows),
        ("freezer_readings", reading_cols, reading_rows),
        ("stock", stock_cols, stock_rows),
        ("maintenance_tickets", ticket_cols, ticket_rows),
    ]

    # ------------------------------------------------------------ SQL
    with open(os.path.join(out, "sql", "seed.sql"), "w", encoding="utf-8") as f:
        f.write("SET NOCOUNT ON;\nGO\n\n")
        # Children first (foreign keys). executed_actions is not touched here.
        for t, _, _ in reversed(tables):
            f.write(f"DELETE FROM dbo.{t};\n")
        f.write("GO\n\n")
        for t, c, r in tables:
            write_inserts(f, t, c, r)
    for t, c, r in tables:
        write_csv(os.path.join(out, "csv", f"{t}.csv"), c, r)

    # ------------------------------------------------------------ documents for Foundry IQ (content in Portuguese)
    minutes_date = end - timedelta(days=55)
    promo_start = end - timedelta(days=6)
    docs = {
        "manual-refrigeracao.md": MANUAL,
        f"ata-comite-operacoes-{minutes_date.isoformat()}.md": ATA.format(data=minutes_date.strftime("%d/%m/%Y")),
        "politica-reposicao.md": POLITICA,
        "calendario-promocoes.md": PROMO.format(
            ini=promo_start.strftime("%d/%m/%Y"), fim=(promo_start + timedelta(days=9)).strftime("%d/%m/%Y")),
    }
    for name, txt in docs.items():
        with open(os.path.join(out, "docs", name), "w", encoding="utf-8") as f:
            f.write(txt)

    with open(os.path.join(out, "GABARITO.md"), "w", encoding="utf-8") as f:
        f.write(GABARITO.format(ini=start, fim=end, off=recife_off_start.strftime("%d/%m %H:%M"),
                                off_fim=recife_off_end.strftime("%d/%m %H:%M"),
                                n_leit=len(reading_rows), n_vend=len(sales_rows),
                                n_frz=len(freezer_rows)))

    print(f"Period: {start} to {end}")
    for t, _c, r in tables:
        print(f"  {t:22s} {len(r):>7,d} rows")


MANUAL = """# Manual de Operação — Refrigeração e Congelados
**Rede Ipê Supermercados** · Diretoria de Operações · Revisão 4

> Documento fictício criado para demonstração.

## 1. Faixas de temperatura
| Equipamento | Faixa ideal | Atenção | Crítico |
|---|---|---|---|
| Freezer de congelados | -22 °C a -18 °C | acima de -18 °C | acima de -15 °C por 2 horas ou mais |
| Balcão de laticínios | 0 °C a 5 °C | acima de 5 °C | acima de 7 °C por 2 horas ou mais |

## 2. O que fazer em cada situação
- **Atenção:** o gerente da loja verifica a vedação das portas e a carga do equipamento em até 4 horas.
- **Crítico:** abrir chamado de manutenção com prioridade alta e transferir os produtos para outro freezer da mesma loja.
- **Equipamento sem leitura por mais de 6 horas:** tratar como crítico até que a loja confirme a temperatura manualmente.
- Após qualquer evento crítico, a qualidade avalia o lote antes de devolvê-lo à venda.

## 3. Vida útil dos equipamentos
- Freezers com mais de 7 anos de uso entram no plano de substituição.
- Dois chamados de "ruído anormal" no mesmo equipamento em 90 dias indicam falha de compressor provável.

## 4. Responsáveis
- Abertura de chamado: gerente da loja.
- Aprovação de troca de equipamento acima de R$ 15.000: Comitê de Operações.
"""

ATA = """# Ata — Comitê de Operações
**Data:** {data} · **Participantes:** Diretoria de Operações, Manutenção, Regionais Sudeste/Sul/Nordeste

> Documento fictício criado para demonstração.

## Pauta 1 — Resultado do trimestre
Vendas dentro da meta em 17 de 20 lojas. Congelados seguem como a categoria de maior margem.

## Pauta 2 — Refrigeração
- **Ipê Cambuí (Campinas), freezer FRZ-07-2:** chamado de ruído no compressor tratado com ajuste paliativo. O equipamento é de 2018. A manutenção recomenda a troca do compressor (orçamento de R$ 18.400).
- **Decisão:** a troca fica **adiada para o 4º trimestre**. Até lá, a loja deve monitorar a temperatura diariamente e escalar imediatamente se o equipamento passar da faixa de atenção.
- **Ipê Boa Viagem (Recife):** a regional relatou instabilidade de energia no circuito de refrigeração. **Ação:** avaliar a instalação de nobreak/estabilizador. Responsável: Manutenção Nordeste.

## Pauta 3 — Promoções
Aprovada a campanha "Festival de Bebidas" como piloto na Ipê Batel (Curitiba). Se o resultado for bom, ela será estendida à região Sul.

## Encaminhamentos
1. Manutenção: enviar o cronograma de trocas do 4º trimestre.
2. Regionais: reforçar o checklist diário de temperatura.
3. Comercial: garantir estoque extra de bebidas antes do piloto em Curitiba.
"""

POLITICA = """# Política de Reposição de Estoque
**Rede Ipê Supermercados** · Diretoria Comercial

> Documento fictício criado para demonstração.

- Todo item tem um **ponto de reposição** por loja. Abaixo dele, o pedido ao centro de distribuição deve sair em até 24 horas.
- **Item abaixo de 25% do ponto de reposição** é risco de ruptura: comunicar o gerente regional no mesmo dia.
- **Durante promoções**, a loja recebe estoque reforçado de 2,5 vezes o ponto de reposição na véspera do início. A responsabilidade é do Comercial.
- Os pedidos emergenciais saem do CD da região: Sudeste (Cajamar/SP), Sul (São José dos Pinhais/PR), Nordeste (Cabo de Santo Agostinho/PE), Centro-Oeste e Norte (Anápolis/GO).
"""

PROMO = """# Calendário de Promoções — trimestre atual
**Rede Ipê Supermercados** · Marketing

> Documento fictício criado para demonstração.

| Campanha | Lojas | Período | Categorias |
|---|---|---|---|
| Festival de Bebidas (piloto) | Ipê Batel (Curitiba) | {ini} a {fim} | Bebidas |
| Semana do Hortifruti | Todas | a definir | Hortifruti |
| Black Friday Ipê | Todas | novembro | Todas |

Observação: o piloto em Curitiba prevê um aumento de 50% a 80% nas vendas de bebidas. O estoque reforçado é responsabilidade do Comercial (ver Política de Reposição).
"""

GABARITO = """# Gabarito da demo — o que o agente deve descobrir

Período dos dados: **{ini} a {fim}** · {n_frz} freezers · {n_leit:,} leituras · {n_vend:,} linhas de vendas.
As views calculam "agora" a partir do dado mais recente, então a demo funciona em qualquer data.
Mesmo assim, gere os dados de novo na véspera do evento.

## 1. Campinas: freezer degradando (história principal)
- **Dado (SQL):** o FRZ-07-2 aquece de forma gradual nos últimos 10 dias, de cerca de -19,5 °C para cerca de -13 °C, com picos à tarde nos últimos 3 dias. Em `vw_freezer_status` ele aparece como **CRITICO**.
- **Contexto (Foundry IQ):** a ata do Comitê já citava o ruído no compressor e registra que a troca, de R$ 18.400, foi **adiada para o 4º trimestre**. O chamado de ~58 dias atrás foi fechado com "ajuste paliativo".
- **Ação esperada:** antecipar a troca do compressor (o valor passa de R$ 15 mil, então precisa de aprovação do Comitê), transferir os produtos e abrir chamado de alta prioridade. **Momento de aprovação humana.**

## 2. Recife: freezer offline e queda de vendas
- **Dado:** o FRZ-13-1 ficou **sem leituras** de {off} a {off_fim} (36 horas) e voltou acima de -8 °C. Hoje ele está na faixa normal, mas aparece como **ATENCAO** em `vw_freezer_status` por causa da lacuna (`longest_gap_7d_hours`). As vendas de Congelados da loja 13 caíram cerca de 45% nos últimos 5 dias (cerca de 30% na comparação semanal de `vw_weekly_sales`), e o estoque de congelados está quase zerado (produtos descartados). Há um chamado **Aberto**.
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
"""

if __name__ == "__main__":
    main()
