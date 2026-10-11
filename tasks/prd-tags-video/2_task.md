---
status: pending
task_kind: enabling
blocked_by: ["1.0"]
gate: 'rg -q "^> \*\*Adendo tags de vídeo:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-videos.md && rg -q "^> \*\*Adendo tags de vídeo:\*\* ASCII e Figma aprovados pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-autoria-curso.md'
gate_expect: "exit 0: o cabeçalho dos dois documentos registra ASCII e Figma do adendo de tags aprovados pelo responsável"
---

# 2.0 Figma do adendo de tags de vídeo aprovado

**Fatia:** EN-01 (Figma) · **Cobre:** RF-01 a RF-06 (desenho) · **Spec:** `techspec.md` § Habilitadores inevitáveis · **ADR:** —

## Comportamento

O adendo aprovado em 1.0 está desenhado no Figma do Design System Code4Coders, sobre as telas já
aprovadas de Vídeos e de Autoria. O desenho reaproveita os componentes e tokens existentes e cobre
todos os estados de 1.0, em desktop e celular. O componente de tags (etiqueta, campo com sugestões
e escolha E/OU) é **um só** e aparece nas quatro superfícies (envio, edição, filtro da tela Vídeos
e filtro do seletor).

Os frames ficam listados na seção do adendo de cada documento, com o link. Depois da aprovação
explícita do responsável, a linha do cabeçalho passa a
`> **Adendo tags de vídeo:** ASCII e Figma aprovados pelo responsável em AAAA-MM-DD (…)`.

## Fora do escopo desta task

- Código de tela → 3.0 a 7.0.
- Componentes novos no Design System além do componente de tags.

## Decisões fechadas

- As do adendo ASCII aprovado em 1.0.
- Nenhuma dependência de UI nova no `admin-spa`: o componente é desenhado sobre os primitivos existentes (`techspec.md`, Bloco Frontend).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-videos.md` e `docs/design/wireframes-autoria-curso.md` (links dos frames e linha de aprovação)
- **ref:** Figma do Design System Code4Coders (telas de Vídeos e Autoria aprovadas); `DESIGN.md`; `docs/design/Components.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Documentação | — | Sem check automatizado para documento de design; o gate estático é a evidência | AGENTS.md |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Todos os estados do adendo têm frame no Figma, com link na seção do adendo.
- [ ] O responsável aprovou o desenho visual, e a aprovação está registrada com a data nos dois documentos.
