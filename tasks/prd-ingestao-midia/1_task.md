---
status: done
task_kind: enabling
blocked_by: []
gate: 'grep -qE "^> \*\*Status:\*\* aprovado \(ASCII" docs/design/wireframes-videos.md'
gate_expect: "exit 0: o documento de wireframes da área Vídeos existe e registra a aprovação do ASCII pelo usuário"
---

# 1.0 Wireframe ASCII da área Vídeos aprovado, com nome e posição da área decididos

**Fatia:** EN-02 (ASCII) · **Cobre:** QP-02, RN-M15, Experiência do Usuário do PRD · **Spec:** `techspec.md` § Bloco Frontend, § Habilitadores (EN-02) · **ADR:** —

## Comportamento

Existe `docs/design/wireframes-videos.md`, no mesmo formato de `docs/design/wireframes-acesso-interno.md`
(inventário, fluxo, layout base, wireframe por tela e estado, plano para o Figma), aprovado pelo
usuário. Ele desenha, sobre o AppShell do backoffice já aprovado em CAP-002:

- **Lista** (`/admin/videos`): uma linha por vídeo com título, estado (cor **e** texto), autor, data e
  duração quando pronto; motivo da falha na própria linha; filtro por estado e busca por título;
  estados vazio, carregando, erro e "atualizando" enquanto há vídeo em andamento.
- **Envio:** escolha do arquivo com recusa imediata de formato (fora de MP4/MOV/MKV) e tamanho (acima de
  5 GB); conferência do título; barra de progresso com volume transferido e restante; aviso antes de
  sair da página durante a transferência; falha de rede numa parte e reenvio.
- **Retomada:** aviso *"envio incompleto de aula-3.mp4 — selecione o mesmo arquivo para continuar"*, com
  o prazo; escolha de outro arquivo iniciando envio novo.
- **Estados do vídeo** *recebido*, *em preparação*, *pronto · duração* e *falhou* com os quatro motivos
  do PRD (RF-05), sem código técnico.
- **Edição de título** e erro de título em branco.
- **Sem permissão:** reusa a tela de 403 de CAP-002.
- **Acessibilidade:** onde o progresso é anunciado em intervalos e onde a mudança de estado é
  anunciada; ordem de foco por teclado.

O documento **decide QP-02** (área própria "Vídeos" ou dentro de Autoria; posição no menu) e registra
a decisão. Nenhum texto promete que o vídeo é "protegido contra cópia" (RN-M15): quando precisar falar
de proteção, diz que o vídeo **não fica público**.

A aprovação é registrada no cabeçalho, no formato do documento de CAP-002:
`> **Status:** aprovado (ASCII) em <data> · Figma pendente`.

## Fora do escopo desta task

Desenho no Figma (2.0). Qualquer código no `admin-spa`. Reprodução do vídeo (CAP-007).

## Decisões fechadas

- Fluxo ASCII → Figma → aprovação → código (PRD, Experiência do Usuário; decisão do usuário em 2026-09-26).
- Rota `/videos` sob a base `/admin/`, permissão `midia.enviar` (techspec, Bloco Frontend).
- Lista se atualiza enquanto há vídeo em *recebido*/*em preparação*; sem e-mail (DP-04).
- Todo ator com `midia.enviar` vê e titula todos os vídeos da escola, com o autor (DP-03).
- Textos dos motivos e avisos: os do PRD (RF-03, RF-05, Experiência do Usuário).

## Modificar / Referenciar

- **ref:** `docs/design/wireframes-acesso-interno.md` (formato e AppShell aprovados), `docs/design/Components.md`, `DESIGN.md`
- **ref:** `tasks/prd-ingestao-midia/prd.md` (RF-02 a RF-07, Experiência do Usuário), `api-contract.yaml` 1.1.1 (campos, estados e erros)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado para documento de design; a aprovação é do usuário | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Toda tela e estado listados em Comportamento têm wireframe.
- [ ] QP-02 está decidida no documento.
- [ ] O usuário aprovou o ASCII, e a aprovação está registrada no cabeçalho.
