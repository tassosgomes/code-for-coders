---
status: pending
task_kind: enabling
blocked_by: ["3.0", "4.0", "5.0", "6.0", "7.0", "8.0", "9.0", "10.0", "11.0", "12.0", "13.0"]
gate: 'npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml contracts/bff-student/openapi-aula.yaml contracts/media/openapi-webhook.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error'
gate_expect: "exit 0: os quatro contratos promovidos passam no Spectral com a exceção vigente"
---

# 14.0 Contratos de transcrição consolidados em contracts/

**Fatia:** Encerramento do PRD · **Cobre:** Consolidação dos contratos de RF-02, RF-03, RF-04, RF-07, RF-08 · **Spec:** `techspec.md#contratos-e-fronteiras` · **ADR:** 0020

## Comportamento

Ao fim do PRD, os recortes que as tasks 3.0 a 11.0 acrescentaram a `contracts/` são consolidados na
regra 2 do `contracts/README.md`: subir `info.version` (`bff-admin` vídeos para 1.3.0, `media` interno
para 1.3.0, `bff-student` aula para 1.2.0, `media` webhook 1.0.0 novo), acrescentar a linha de origem
em `info.description` de cada um, registrar `text/vtt` na exceção C-08 de `contracts/.spectral-exception.yaml`,
atualizar a tabela do README com os quatro arquivos (incluindo a linha de
`contracts/media/openapi-webhook.yaml`) e conferir que o promovido coincide com o implementado.

**Por que não cabe numa fatia:** é a consolidação do acordo do PRD, sem comportamento observável próprio;
a regra do repositório a exige no fim, e nenhuma fatia pode fechá-la antes das outras.

**Desbloqueia** o encerramento do PRD e o arquivamento de `tasks/prd-transcricao-legenda-aula/`.

## Fora do escopo desta task

- Qualquer mudança de comportamento ou de contrato além do que as tasks anteriores implementaram.
- Arquivar a pasta do PRD: passo seguinte do fluxo, depois do encerramento.

## Decisões fechadas

- Promoção aditiva por task já feita; aqui só a consolidação (`contracts/README.md`, regra 2).
- O contrato do serviço de transcrição não é copiado para `contracts/`.
- Nada em `tasks/archive/` é editado.

## Modificar / Referenciar

- **modificar:** `contracts/bff-admin/openapi-midia.yaml`; `contracts/media/openapi-internal.yaml`; `contracts/bff-student/openapi-aula.yaml`; `contracts/media/openapi-webhook.yaml`; `contracts/.spectral-exception.yaml`; `contracts/README.md`
- **ref:** `tasks/prd-transcricao-legenda-aula/contracts.md` (versões de entrada e saída); `api-contract.yaml`, `internal-api-contract-media.yaml`, `api-contract-student.yaml`, `webhook-contract-media.yaml`; `contracts/README.md`

## Verificações do projeto

| Componente | Comando | Resultado esperado | Fonte |
|---|---|---|---|
| Contratos | `npx --yes @stoplight/spectral-cli lint contracts/bff-admin/openapi-midia.yaml contracts/media/openapi-internal.yaml contracts/bff-student/openapi-aula.yaml contracts/media/openapi-webhook.yaml --ruleset contracts/.spectral-exception.yaml --fail-severity=error` | exit 0 | `contracts/README.md` |
| Contratos | `rg -q "^  version: 1.3.0" contracts/media/openapi-internal.yaml` | exit 0 (versão consolidada) | `contracts/README.md` regra 2 |

## Pronto quando

- [ ] Gate passa (exit 0).
- [ ] As versões e as linhas de origem estão consolidadas e a tabela do README lista os quatro arquivos.
- [ ] A exceção `text/vtt` está registrada e o Spectral não aponta erro.
