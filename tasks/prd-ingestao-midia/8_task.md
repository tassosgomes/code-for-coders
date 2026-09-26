---
status: pending
task_kind: vertical
blocked_by: ["7.0"]
gate: 'dotnet test --project src/media/tests/CodeForCoders.Media.IntegrationTests/CodeForCoders.Media.IntegrationTests.csproj -- --filter-class CodeForCoders.Media.IntegrationTests.VideoPreparationFailureTests --minimum-expected-tests 7 && npm --prefix src/admin-spa run test -- video-failure'
gate_expect: 'Pelo menos 9 testes passam: 7 Media com ffmpeg, MinIO e RabbitMQ reais, 2 SPA'
---

# 8.0 Vídeo com problema falha com motivo claro e não fica preso

**Fatia:** V-05 · **Cobre:** RF-04 (duração e ilegível), RF-05, RF-09 (`midia.preparacao-falhou`), RN-M05, RN-M06, DP-02 (limite de 3 h), DP-06, US-03 · **Spec:** `techspec.md` § V-05, § Media worker — preparação (passos 2 e 6) · **ADR:** [ADR-0006](../../docs/adr/0006-preparacao-de-video-e-custodia-de-chave.md)

## Comportamento

- **Falha determinística** (sem nova tentativa, direto a `failed`):
  - sonda falha ou não há faixa de vídeo (ex.: `.mp4` com bytes aleatórios) → `unreadable-file`;
  - codec da faixa de vídeo que o ffmpeg da imagem não decodifica → `unsupported-format`;
  - duração acima de 10.800 s (ex.: vídeo de 3h01 em 144p) → `duration-exceeded`.
- **Falha passageira** (armazenamento indisponível, processo encerrado, lease vencida): o vídeo volta a
  `received`, elegível de novo em +1 min e depois em +5 min; se a condição passa, ele chega a `ready`
  sozinho. Na 3ª tentativa esgotada → `failed` com `preparation-failed`.
- Toda ida a `failed` grava o outbox `midia.preparacao-falhou.v1` com `reason`, no mesmo commit, com
  `eventId` fixo; depois apaga o original e qualquer `hls/` parcial. Nenhum objeto do vídeo sobra no
  bucket, e o diretório de trabalho e o arquivo de chave são apagados.
- **SPA:** a linha mostra, sem código técnico, "arquivo de vídeo ilegível", "formato de vídeo não
  suportado", "duração acima de 3 horas" ou "não foi possível preparar este vídeo — envie novamente"
  (textos do PRD, RF-05), no desenho aprovado; a lista para de consultar quando não há vídeo em andamento.

## Fora do escopo desta task

Reenviar ou reprocessar a partir da tela (reenvio é um envio novo, 4.0). Excluir vídeo (fora do PRD).

## Decisões fechadas

- Três tentativas só para falha passageira; nenhuma para falha determinística: D-03.
- Motivos em inglês kebab-case, iguais na API e no fato: C-15; textos em pt-BR do PRD.
- Arquivos de teste gerados no próprio teste; falha passageira por falha injetada no adaptador de armazenamento.

## Modificar / Referenciar

- **modificar:** `src/admin-spa/src/testing/handlers.ts`
- **ref:** `asyncapi-contract.yaml` 1.0.0 (`publicarPreparacaoFalhou`, enum `reason`); `api-contract.yaml` 1.1.1 (`FailureReason`); Figma aprovado; skills `dotnet` e `react`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| `src/media` | `dotnet format src/media/CodeForCoders.Media.slnx --verify-no-changes` | exit 0 | `ci-dotnet.yml` Lint |
| `src/media` | `dotnet build src/media/CodeForCoders.Media.slnx` | exit 0, sem warnings novos | `ci-dotnet.yml` |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.UnitTests/CodeForCoders.Media.UnitTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/media` | `dotnet test --project src/media/tests/CodeForCoders.Media.ArchitectureTests/CodeForCoders.Media.ArchitectureTests.csproj` | exit 0 | `ci-dotnet.yml` Testes |
| `src/admin-spa` | `npm --prefix src/admin-spa run lint` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run typecheck` | exit 0 | `ci-react-ts.yml` |
| `src/admin-spa` | `npm --prefix src/admin-spa run build` | exit 0 | `ci-react-ts.yml` |

## Pronto quando

- [ ] Gate passa (exit 0) com pelo menos 9 testes.
- [ ] Bytes aleatórios, 3h01 e codec não decodificável chegam a `failed` com o motivo certo na primeira tentativa.
- [ ] Armazenamento que volta depois da 1ª falha → vídeo `ready`; que não volta → `preparation-failed` após 3 tentativas.
- [ ] Fila de teste recebe um `midia.preparacao-falhou.v1` conforme o AsyncAPI; o bucket não guarda nada do vídeo.
- [ ] Smoke no Compose: enviar um `.mp4` ilegível e ver "arquivo de vídeo ilegível" na linha.
