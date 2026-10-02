# ADR-0010: Autenticação de serviço de `commerce` em Identity, para confirmar a conta de aluno ao conceder acesso

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0010-autenticacao-de-servico-commerce-em-identity.md`
- Domínios/componentes afetados: `commerce` (módulo Matrícula e Direito de Acesso, como chamador), `identity` (como destino), `bff-admin` (escopo novo no emissor que já existe)
- Origem histórica: `tasks/prd-concessao-acesso`, CAP-008 (decisões C-02 e C-03 do conjunto de contratos)
- Substitui: Nenhuma. Estende a [ADR-0004](0004-autenticacao-de-servico-bff-identity.md) e a [ADR-0005](0005-sessao-e-servico-do-backoffice.md), que decidiram a autenticação de serviço de **BFFs** em Identity e previram "mais de um emissor, cada um com chaves e escopos próprios"

## Data

2026-10-01

## Contexto

Matrícula só concede acesso a **conta de aluno** (RN-D09 do domain doc de Matrícula). Quem é a conta e de que tipo ela é pertence a Identity, e a regra "só conta de aluno recebe concessão" precisa valer no serviço dono da concessão, não só na tela. A cortesia (primeiro PRD de `CAP-008`) é concedida pelo financeiro no backoffice: o `bff-admin` localiza a conta pelo e-mail em Identity e envia a `commerce` o identificador da conta. Entre a localização e a confirmação passa tempo; a conta pode ter sido desativada, e um BFF comprometido ou com defeito poderia enviar o identificador de uma conta de ator interno.

Até aqui, só BFFs chamam Identity, e Identity valida a asserção de serviço de cada emissor configurado (`ServiceAssertionOptions.Issuers`, com chaves públicas por `kid`, escopos permitidos e tenants permitidos). Um serviço de domínio nunca chamou Identity. A ADR-0005 já fixou que as duas superfícies não compartilham credencial e que cada emissor tem escopos próprios. Também vale o que o baseline decidiu para o caminho quente: serviços de domínio validam o JWT do ator localmente por JWKS e **não** consultam Identity para autorizar (ADR-0005, alternativa 2); esta decisão não muda isso, porque trata de uma pergunta de **dado** ("esta conta é de aluno e está ativa?"), feita uma vez por concessão, fora do caminho de reprodução.

## Decisão

1. **`commerce` passa a ser emissor de asserção de serviço em Identity**, no mecanismo da ADR-0004: JWS RS256, `iss` e `sub` = `commerce`, `aud` = o identificador interno de Identity já usado pelos BFFs, `scope`, `tenantId` da escola, `iat`/`nbf`/`exp` com vida de até 60 segundos e `jti` único por chamada, consumido uma vez em Identity. O par de chaves é **exclusivo de `commerce`** e distinto dos de `bff-student` e `bff-admin`.
2. **Escopo único para o chamador novo: `student-account:confirm`.** Identity registra o emissor `commerce` com esse escopo e a lista de `tenantId` que ele pode representar. `commerce` não obtém escopo de sessão, de convite, de papel nem de referência de auditoria.
3. **A operação devolve só `eligible`.** Verdadeiro apenas para conta **de aluno**, **ativa** e do tenant da asserção. Inexistente, de ator interno, desativada e de outro tenant são indistintos. Não devolve e-mail, nome nem outro dado da conta.
4. **Falha fechada.** `commerce` concede somente depois de `eligible: true`. Sem resposta, resposta inválida ou tempo esgotado, nada é gravado e o chamador recebe erro (503 em `commerce`, 502 no BFF). Não há valor padrão "elegível".
5. **A localização pelo e-mail continua sendo do `bff-admin`**, com o escopo novo `student-account:lookup` no emissor `bff-admin` (ADR-0005) e a sessão interna vigente com a permissão `cortesia.conceder` validada por Identity. O e-mail nunca vai para `commerce`.
6. **Tempo-limite curto e sem repetição automática na confirmação.** A repetição é do operador, pela mesma `Idempotency-Key` da cortesia, que não duplica a concessão.

## Alternativas Consideradas

### Alternativa 1: Confiar no `bff-admin` e não confirmar em `commerce`

- **Descrição:** `commerce` aceita o identificador enviado pelo BFF.
- **Prós:** nenhuma chave nem salto síncrono novos.
- **Contras:** a regra RN-D09 deixa de ser do serviço dono; um defeito ou comprometimento do BFF concede acesso a conta que não é de aluno; a conta desativada entre a localização e a confirmação passa.
- **Por que rejeitada:** repete o raciocínio da ADR-0005 (o serviço dono decide, sem confiar no BFF).

### Alternativa 2: `commerce` mantém uma projeção das contas, alimentada pelos fatos de Identity

- **Descrição:** `identidade.conta-criada`, `conta-confirmada` e `conta-desativada` alimentam uma tabela em `commerce`.
- **Prós:** nenhuma chamada síncrona a Identity.
- **Contras:** exige carga inicial das contas existentes (reenvio de Identity); deixa uma janela em que uma conta recém-desativada ainda recebe; cria uma réplica de dado de Identity em outro serviço.
- **Por que rejeitada:** o PRD exige recusar a conta desativada entre a localização e a confirmação, e a concessão é rara, de baixo volume. O custo de um salto síncrono pontual é menor que o de uma réplica.

### Alternativa 3: Reusar a credencial do `bff-admin` em `commerce`

- **Descrição:** `commerce` assina com a chave do `bff-admin`.
- **Prós:** nenhuma chave nova.
- **Contras:** o vazamento de uma compromete as duas superfícies; impossível restringir escopos por chamador.
- **Por que rejeitada:** contraria a alternativa 3 da ADR-0005.

### Alternativa 4: mTLS entre serviços

- **Descrição:** autenticação de transporte com certificado de workload.
- **Por que rejeitada nesta etapa:** a fundação não documenta emissão e renovação de certificados de workload (mesma constatação das ADR-0004 e ADR-0009).

## Consequências

### Positivas

- RN-D09 vale no serviço dono da concessão, com a fonte única da identidade.
- Identity não aprende nada de Matrícula além de um escopo: a pergunta é de dado, e a resposta é um booleano.
- Nenhum dado pessoal sai de Identity para `commerce`.
- O mecanismo, o verificador e a configuração de emissores já existem em Identity; o trabalho é configuração, um escopo novo e um assinador em `commerce`.

### Negativas

- Um par de chaves e uma configuração de emissor a mais; mais um segredo a distribuir e rotacionar (`scripts/generate-local-env.sh`, Compose e secret manager).
- Um salto síncrono `commerce` → Identity por concessão: a cortesia depende da disponibilidade de Identity.
- Um assinador de asserção em `commerce`, cópia do padrão do `bff-admin`; assinadores duplicados divergem sem teste de conformidade.

### Riscos

- **Identity indisponível impede conceder cortesia.** Aceito: é rara, manual e repetível; a falha é fechada e visível (`STUDENT_ACCOUNT_CHECK_UNAVAILABLE`).
- **Reuso de asserção interceptada:** TLS interno, vida de até 60 segundos, `jti` consumido uma vez, nenhuma asserção em log, span ou métrica.
- **Rotação falha:** sobreposição de chaves públicas, teste com `kid` antigo e novo.
- **Enumeração de contas por `eligible`:** a rota só aceita a asserção de `commerce`, que já conhece o identificador da conta; não recebe e-mail.

## Notas de Implementação

- A chave privada de `commerce` e a configuração do emissor em Identity vêm do secret manager do ambiente (ADR-0002) e são validadas na partida; o emissor `commerce` **não** é criado em ambiente que não tenha o par de chaves.
- Referência de comportamento do verificador: `src/identity/src/CodeForCoders.Identity.Api/Security/ServiceAssertionVerifier.cs`.
- O OpenAPI interno de Identity descreve a asserção, os escopos e os erros (`tasks/prd-concessao-acesso/internal-api-contract-identity.yaml`).

## Referências

- [ADR-0001](0001-monorepo-de-codigo.md), [ADR-0002](0002-plataforma-de-runtime-coolify.md), [ADR-0004](0004-autenticacao-de-servico-bff-identity.md), [ADR-0005](0005-sessao-e-servico-do-backoffice.md), [ADR-0009](0009-autenticacao-de-servico-bff-aluno-em-servicos-de-dominio.md).
- [Baseline arquitetural](../../context/architecture-baseline.md) — G07, G10, G15, G23; BA05.
- Origem histórica: `tasks/prd-concessao-acesso/techspec.md` e `tasks/prd-concessao-acesso/contracts.md` (C-02, C-03).
