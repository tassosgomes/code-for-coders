# Revisão da task 1.0 — Wireframes ASCII da tela da aula aprovados

Run: run.j4NSdp8n
Modo: focused · Tentativa: 1/3 · Data: 2026-10-03
Resultado: **VALIDAÇÃO APROVADA** (0 bloqueantes, 3 recomendações)

## Escopo revisado

- `tasks/prd-reproducao-protegida/1_task.md` (frontmatter, `status: validating`).
- `docs/design/wireframes-aula.md` (novo, não rastreado, 411 linhas), HEAD `e3d2ed468054f904c3cf6cc41e9bec56d191fa2e`.
- `tasks/prd-reproducao-protegida/flow-state.json`: registro da aprovação do responsável (2026-10-03).
- Cruzamento com `prd.md` (Experiência do Usuário, RF-04 a RF-06, DP-03, DP-05, DP-10, RN-M15), `api-contract.md` (títulos dos estados) e `wireframes-cortesias.md` (formato do cabeçalho de aprovação).
- Nenhum arquivo de código no diff; a task não toca `src/`.

## Gate

| Comando | Exit | Esperado |
|---|---|---|
| `rg -q "^> \*\*Status:\*\* (ASCII aprovado\|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-aula.md` | **0** | exit 0 (cabeçalho registra a aprovação do ASCII) |

Linha 3 do documento: `> **Status:** ASCII aprovado pelo responsável em 2026-10-03`.
A primeira execução dentro de um comando composto devolveu exit 1 por causa do wrapper `rtk`
do ambiente. Repeti com `rtk proxy rg ...` e com `bash -c 'rg ...'`, e ambos deram exit 0, então
o veredito vale o do binário real.

Verificações do projeto: `docs/design` não tem check automatizado (a aprovação é do responsável), conforme a task.
Não há suíte comportamental para um documento de design; a limitação é declarada.

## Cobertura contra a task

| Item exigido | Onde |
|---|---|
| Estrutura: título, player, aviso fixo, lista na ordem do currículo com aula atual em texto; lista ao lado no desktop e abaixo no celular | G1, G2, A1.a, A1.b |
| Desktop e celular de 390 px | A1.a, A1.b, A1.p (mobile dos estados), seção 3.1 |
| Controles próprios: tocar/pausar, linha do tempo, volume, velocidade 0,5x–2x, tela cheia; ordem de foco, nome acessível, foco visível | G3, G4, A1.a, bloco "Player", seção 5 |
| Tela cheia com a marca visível | G6, A1.c |
| Marca d'água: zonas fixas fora da barra, troca sem repetir a anterior (30 s), legível, visível pausada | G7, G8, bloco "Marca d'água", A1.e |
| Estados com mensagem exata | A1.d (carregando), A1.e (pausado), A1.f (sem acesso, sem título), A1.g (encerrado com data), A1.h (indisponível + *Tentar de novo*), A1.i (aula não disponível, uma mensagem para os quatro casos), A1.j (vídeo indisponível), A1.k (sem e-mail), A1.l (sem suporte), A1.m e A1.n (interrompida), A1.o (renovação invisível) |
| Entrada e retorno; rota direta negada | seção 2.1, 2.2, A1.q, A1.r |
| Aviso de uso pessoal e ausência de "protegido contra cópia" | G9, seção 7 item 8 |
| Acessibilidade | seção 5 (teclado, região de status, `aria-hidden` na marca, lista com "Aula atual" em texto, contraste) |

Mensagens conferidas literalmente contra a tabela de estados do PRD (linhas 185–193), o critério de
"sem e-mail" (linha 352) e os `title` do `api-contract.md`: todas coincidem.

Verificação de linguagem: `rg -i "sessão|token|CDN|decisão|protegido contra|garant"` no documento não
acha nenhuma ocorrência em texto de tela. Nenhuma mensagem exibida usa os termos proibidos.

Escopo respeitado: nada de Figma (seção 8 fica "reservada"), nenhum código, nada de progresso,
percentual ou "continuar de onde parou" (G13).

Aprovação: o `flow-state.json` registra a aprovação explícita do responsável na conversa do
orquestrador em 2026-10-03, com a linha de status gravada depois dela. O cabeçalho segue o formato
de `wireframes-cortesias.md`.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueiam)

1. `docs/design/wireframes-aula.md:319-320` (A1.m): a frase "O que foi assistido até ali continua
   valendo" sugere registro de progresso, que a task e o G13 deixam fora (DP-06, CAP-017). Convém
   retirá-la ou reescrevê-la no ajuste do Figma (task 2.0), para o desenho não prometer algo que
   esta fatia não entrega.
2. `docs/design/wireframes-aula.md:265, 272, 317`: A1.f e A1.g dizem "sem lista de aulas", mas A1.m
   oferece *Ver outras aulas*. Vale decidir no Figma qual é o destino dessa ação (volta ao curso ou
   à lista), para a ação não ficar sem destino em A1.g.
3. `docs/design/wireframes-aula.md:4, 409-411`: a seção 8 e a linha *Figma* ficam "a definir". Isso
   está correto agora; lembre de registrar "ASCII e Figma aprovados" na task 2.0.

## Integridade

HEAD `e3d2ed468054f904c3cf6cc41e9bec56d191fa2e` antes e depois. Esta revisão não alterou código, status,
tasks nem commits; só gravou este relatório e o resultado estruturado.
