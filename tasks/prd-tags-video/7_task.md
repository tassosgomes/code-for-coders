---
status: done
task_kind: vertical
blocked_by: ["6.0"]
gate: 'npm --prefix src/admin-spa run test -- authoring-video-picker-tags'
gate_expect: "Os testes do admin-spa com o fragmento authoring-video-picker-tags passam (pelo menos 5: filtro, só prontos, E/OU, vazio com limpar e vínculo)"
---

# 7.0 O professor filtra o seletor de vídeo da aula por tags

**Fatia:** V-05 · **Cobre:** RF-05, RF-06 (no seletor); RN-C04, RN-T01; US-04, US-05 · **Spec:** `techspec.md` § V-05 · **ADR:** —

## Comportamento

**Ponta a ponta:** na tela de aula da Autoria, o seletor de vídeo ganha o filtro por tags
(componente de 4.0, modo só sugestão) ao lado da busca por título, com a escolha E/OU nas mesmas
regras de 6.0. Cada vídeo listado mostra suas tags, para distinguir vídeos de títulos parecidos.

- A consulta continua com `status=ready` e acrescenta `tag` e `tagMatch`: **só vídeos prontos**
  aparecem, mesmo que um vídeo em preparação tenha a tag (RN-C04).
- Mudar o filtro volta para a página 1, como a busca por título.
- Lista vazia com filtro → "Nenhum vídeo pronto atende aos filtros", com a ação de limpar busca e
  tags.
- Vincular um vídeo achado pelo filtro segue o fluxo atual; nada de tag é gravado na aula nem no
  curso (RN-T01).
- O filtro do seletor é estado local e não vai para a URL (`techspec.md` § Decisões Técnicas).

Backend: nenhuma mudança. O seletor usa `listVideos` (6.0) e `listVideoTags` (4.0) como estão.

## Fora do escopo desta task

- Qualquer mudança em `learning` ou no vínculo (`updateLesson`).

## Decisões fechadas

- O seletor só oferece vídeos prontos (RN-C04).
- E/OU com **E** padrão, visível só com 2 tags ou mais (DP-03).

## Modificar / Referenciar

- **modificar:** `src/admin-spa/src/features/course-authoring/api/get-ready-videos.ts` (filtros e `tags` no schema); `src/admin-spa/src/features/course-authoring/components/course-video-picker.tsx`; `src/admin-spa/src/testing/authoring-video-handlers.ts` (MSW reproduz `tag`/`tagMatch` e `status=ready`)
- **ref:** `src/admin-spa/src/app/routes/authoring-video-picker.test.tsx` (padrão de teste do seletor); `docs/design/wireframes-autoria-curso.md` (adendo aprovado); `contracts/bff-admin/openapi-midia.yaml` (`listVideos`, `listVideoTags` já promovidos); skill `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run test -- authoring-video-picker` | exit 0 (seletor existente intacto) | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate focalizado passa (exit 0).
- [ ] Filtrar por "docker" lista só vídeos prontos com a tag; um vídeo em preparação com "docker" não aparece.
- [ ] Com duas tags, E e OU dão os resultados esperados.
- [ ] Sem resultado → a mensagem e a ação de limpar os filtros.
- [ ] Vincular um vídeo achado pelo filtro funciona como antes.
- [ ] Em dev, montar uma aula filtrando o seletor por tag.
