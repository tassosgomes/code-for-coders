---
status: done
task_kind: enabling
blocked_by: ["1.0"]
gate: 'grep -qE "^> \*\*Status:\*\* aprovado \(ASCII e Figma\)" docs/design/wireframes-videos.md'
gate_expect: "exit 0: o documento de wireframes registra a aprovação do Figma pelo usuário, além do ASCII"
---

# 2.0 Telas da área Vídeos desenhadas no Figma e aprovadas

**Fatia:** EN-02 (Figma) · **Cobre:** Experiência do Usuário do PRD · **Spec:** `techspec.md` § Habilitadores (EN-02) · **ADR:** —

## Comportamento

Cada tela e estado do wireframe aprovado em 1.0 existe no arquivo Figma do Design System Code4Coders,
em páginas "Fluxo — Vídeos" e "Screens — Vídeos", usando os componentes e variáveis do DS do backoffice
(nada de valor fixo onde houver token). O fluxo mostra as passagens lista → envio → progresso → lista
com *recebido* → *em preparação* → *pronto*/*falhou*, e a retomada. O usuário revisa e aprova.

`docs/design/wireframes-videos.md` ganha a seção com os node-ids de cada tela (como a seção 8 do
documento de CAP-002) e o cabeçalho passa a `> **Status:** aprovado (ASCII e Figma) em <data> ·
implementação não iniciada`. Componente novo proposto (por exemplo, linha de vídeo com estado, barra de
progresso de envio) fica identificado para migrar à página de Components.

Se o desenho revelar mudança de comportamento em relação ao ASCII aprovado, o ASCII é atualizado e
reaprovado antes do registro.

## Fora do escopo desta task

Código no `admin-spa`. Code Connect.

## Decisões fechadas

- Wireframe aprovado em 1.0 é a fonte; o Figma não reabre QP-02.
- Estado com cor **e** texto, nunca só cor (PRD, Experiência do Usuário).

## Modificar / Referenciar

- **modificar:** `docs/design/wireframes-videos.md` (node-ids e status)
- **ref:** skills `figma:figma-use` e `figma:figma-generate-design`; páginas "Screens — Acesso interno" do mesmo arquivo Figma (AppShell aprovado)

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `docs/design` | — | Sem check automatizado; a aprovação é do usuário | Fluxo de design do backoffice (CAP-002) |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] Toda tela e estado do ASCII tem frame no Figma, com node-id registrado no documento.
- [ ] O usuário aprovou o Figma.
