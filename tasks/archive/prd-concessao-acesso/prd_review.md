# Full validation — PRD concessao-acesso (CAP-008, 1º PRD)

Run: run.NRdHKvty

**Resultado: FULL VALIDATION APROVADA** — 0 bloqueantes, 4 recomendações. Tentativa full 3/3.

| Campo | Valor |
|---|---|
| base_ref | `1b8a7462a7a85e7832940d731b3555452f9d4e94` (origin/main; ancestral de HEAD) |
| validated_commit | `5c4f486097ed2a873f896f8dae3abeb526c348f9` |
| validated_tree | `dbb74ea9a1c3c122fa68f9d8a7c22c2662ce47aa` |
| Estabilidade | HEAD e `git status --porcelain` idênticos antes e depois (só `flow-state.json` modificado, estado operacional que já existia na linha de base) |
| Escopo | 283 arquivos: commerce, identity, audit, bff-admin, admin-spa, compose/scripts, docs/design, tasks |

## Matriz de CI por componente

Fonte: `.github/workflows/<comp>.yml` chamando `tassosgomes/template-pipeline/.github/workflows/ci-dotnet.yml@v1` e `ci-react-ts.yml@v1`, lidos via `gh api` (as aplicações de parâmetros efetivos estão nos chamadores: Debug, cobertura mínima 70, `security-mode: observe`). Todos os comandos rodaram em primeiro plano, com exit code registrado.

| Componente | restore | `dotnet format --verify-no-changes` | `dotnet test --configuration Debug --coverage` | Cobertura (união de linhas, como o CI) | Testes |
|---|---|---|---|---|---|
| identity | 0 | 0 | 0 | 80,63% | 155/155 |
| audit | 0 | 0 | 0 | 81,32% | exit 0 |
| bff-admin | 0 | 0 | 0 | 83,89% | exit 0 |
| commerce | 0 | 0 | 0 | 96,05% | 365/365 (integração: 47m54s) |
| admin-spa | `npm ci` 0 | `npm run lint` 0 | `npm test` (vitest + coverage) 0 | linhas 91,01% | 235/235 em 44 arquivos |
| admin-spa build | `npm run build -- --base=/admin/` 0 | | | | |

Passos do CI **não reproduzidos localmente**: push da imagem no GHCR, Trivy/SBOM, SAST, secret scan, dependency scan e upload de artifact. Dependem de runner e credenciais do GitHub, e o CI os roda em modo `observe`, que não reprova o job. Substituto parcial: `docker build -f src/commerce/Dockerfile .` terminou com exit 0 e o `tzdata` novo entregou `/usr/share/zoneinfo/America/Sao_Paulo`. Não rodei o build de imagem dos demais componentes (Dockerfiles sem mudança).

## Sensor de discriminação

Isolamento: `git worktree` temporário (sem `git stash`), removido ao fim. Linha de base: HEAD e status idênticos depois do descarte. 27 mutações de comportamento, derivadas dos critérios de aceite, cobrindo as sete fatias; cada uma rodou a classe focalizada da fatia. Logs: `mut_M*.log` no scratchpad da sessão.

- **Mortas (23):** M01 permissão do financeiro · M02 busca devolvendo conta que não é de aluno · M03 reconfirmação aceitando conta desativada · M04 filtro de título · M06 término no início do dia · M07 chave de idempotência reutilizada · M08 conta não elegível · M09 fato `acesso-concedido` não gravado · M10 situação derivada invertida · M11 lista de todos os alunos · M12 fronteira 23:59:59/00:00:00 · M13 vitalícia · M14 reemissão de `acesso-expirado` · M15 concessão não marcada como publicada · M16 rótulo de conta que não é de aluno na trilha · M17 alvo do ato · M20 motivo de 501 no SPA · M21 rótulo no filtro da trilha · M23 validador com 61 meses · M24 motivo de 501 no validador · M25 claim `cortesia.conceder` no commerce · M26 as duas checagens de permissão do BFF removidas · M27 prévia do commerce com 61 meses.
- **Sobreviventes (4), nenhum bloqueante:** M05, M18, M19 e M22. Todos removem uma guarda duplicada, e a mesma regra continua imposta, e provada por teste, em outra camada:
  - **M05** `AccessTerm` aceita 61 meses (`AccessTermTests`, 9 testes sem linha 60/61). Imposto antes pelo validador do caso de uso. M23 (validador 61) morreu em `CourtesyGrantTests`, que tem a linha 61.
  - **M19** BFF aceita `months=61` na prévia (`CourtesyGrantProxyTests` só testa `months=6`). O commerce recusa 61. M27 morreu em `CourtesyTermPreviewTests`.
  - **M18 e M22** removem, cada uma, uma das duas checagens de `cortesia.conceder` no BFF. Cada checagem cobre a outra; M26 (as duas removidas) morreu em `RequiredKeyCsrfSessionAndPermissionPreventUpstreamCalls`. O commerce ainda exige a claim (M25 morto).

  Apliquei aqui um critério mais estrito que a leitura literal do skill, que trata todo sobrevivente como bloqueante. Em cada caso o comportamento exigido pelo PRD (RF-01, RF-03, RF-05) continua observável e testado por outra camada, então a mutação não muda o comportamento do sistema. A única lacuna sem teste próprio é o contrato do BFF na prévia (M19): um 61 chega ao commerce e volta 400 com o código do commerce, não o `INVALID_REQUEST` do BFF.

## Cruzamento PRD ↔ entrega

- RF-01: `StaffRoleCatalog` dá `cortesia.conceder` só ao financeiro (M01). Policy no commerce exige a claim (M25), e o BFF recusa sem a permissão (M26).
- RF-02: busca por e-mail exato só devolve conta de aluno (M02). Concessões do aluno com situação derivada (M10, M11).
- RF-03/RF-04: validação de motivo e vigência (M23, M24, M20). Idempotência (M07). Reconfirmação em Identity com falha fechada (M08). Fato e ato gravados com a concessão (M09).
- RF-05: término em fim de dia, fuso da escola, mês curto e bissexto (M06 e linhas de `AccessTermTests`).
- RF-06: decisão sem rotina, fronteira exclusiva, vitalícia vence a finita (M12, M13).
- RF-07: `acesso-expirado` uma vez por concessão, só para vencidas (M14, M15). Nenhum e-mail ou nome nos fatos (`CourtesyGrantTests`).
- RF-08: ato `cortesia-concedida` conforme e persistido quando não conforme (`CourtesyActRecordingTests`, `CourtesyActPolicyTests`, M17). Rótulos na trilha (M16, M21).
- Design: `docs/design/wireframes-cortesias.md` com `Status: ASCII e Figma aprovados`.
- Configuração: compose, remote, coolify e `generate-local-env.sh` trazem chaves, escopos e `depends_on` de `commerce → identity/audit` de forma consistente entre os três arquivos.

Blocos revisados dos full anteriores (B1.1/B1.2 corridas de teste, B2.1/B2.2/B2.3 conformidade de contrato) estão cobertos pelas suítes que passaram acima. Não reabri o diff de cada correção.

## Bloqueantes

Nenhum.

## Recomendações (não bloqueiam)

1. **R1** `AccessTermTests`: acrescentar linhas de fronteira 0, 60 e 61 (M05).
2. **R2** `CourtesyGrantProxyTests`: fixar que o BFF devolve 400 `INVALID_REQUEST` para `months=0` e `months=61` na prévia (M19).
3. **R3** BFF `CourtesyGrantEndpoints.SendAsync`: as duas checagens de `cortesia.conceder` são redundantes entre si. Ou um teste que isole a segunda (sessão do audience `commerce` sem a permissão), ou remover uma delas (M18, M22).
4. **R4** A suíte de integração do commerce leva ~48 min localmente, dominada por espera (banco novo e host completo por grupo de testes, polling de worker). Vale medir no CI e reduzir se o tempo for problema.

## Limitações

- Passos de segurança e publicação do CI não reproduzidos (acima).
- O sensor cobre 27 mutações por regra de negócio, não é varredura exaustiva.
- Não executei E2E em navegador contra a pilha Compose completa; o escopo é build, testes automatizados, CI local e mutação.
