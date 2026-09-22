---
status: done
task_kind: enabling
blocked_by: []
gate: "dotnet build src/notification/tests/CodeForCoders.Notification.IntegrationTests/CodeForCoders.Notification.IntegrationTests.csproj --no-restore -warnaserror"
gate_expect: "build sem erros e sem warnings"
---

# 8.0 Isolar o processamento transacional por namespace

## Comportamento

O serviço deve aceitar um namespace lógico explícito para cada execução de notificação e preservar esse
namespace em todo o caminho de processamento: Registro de Entrega, outbox, consultas e reivindicações
dos workers, filas/exchanges/routing keys e consumidores. Uma execução só pode ler, reivindicar,
publicar ou consumir dados do próprio namespace; nenhuma consulta ou reivindicação pode selecionar
pedidos, registros ou mensagens globalmente.

O isolamento deve permitir que dois hosts de teste processem pedidos simultaneamente sem consumir o
pedido, o evento ou o estado de outro host. A limpeza de uma execução deve atingir somente seu
namespace. O comportamento de aceite, entrega, retry, falha e expurgo permanece igual dentro do
namespace.

## Por que é um habilitador

O bloqueio B1 da revisão full atravessa todas as fatias já concluídas e não pertence a uma única
jornada vertical: o estado compartilhado é acessado pelo caso de uso, outbox, workers e integração
RabbitMQ. Colocar a correção em qualquer task vertical isolada deixaria as demais execuções sem o
contrato de isolamento e manteria a corrida entre hosts. Esta task estabelece a fronteira comum que
desbloqueia a revalidação full das tasks 1.0–7.0.

## Fora do escopo

- alterar regras de aceite, idempotência, retry, falha definitiva ou retenção;
- criar um novo modelo de mensagem ou alterar os contratos de negócio existentes;
- resolver o isolamento por serialização global dos testes ou por reset global concorrente.

## Arquivos a modificar/referenciar

- `src/notification/src/CodeForCoders.Notification.Domain` e `Infra.Data`, para a persistência e as
  consultas delimitadas pelo namespace;
- `src/notification/src/CodeForCoders.Notification.Application`, para propagar a fronteira aos casos
  de uso e workers;
- `src/notification/src/CodeForCoders.Notification.Infra.Messaging` e `Api`, para publicar e consumir
  somente recursos do namespace;
- fixtures e testes de integração de `src/notification/tests`, para executar hosts concorrentes com
  namespaces distintos e verificar ausência de vazamento de estado.

## Pronto quando

- hosts concorrentes com namespaces distintos não observam nem reivindicam pedidos, registros ou
  eventos uns dos outros;
- outbox e routing de cada namespace chegam somente ao consumidor correspondente;
- a limpeza de um namespace não altera dados ou mensagens de outro;
- os comportamentos das tasks 1.0–7.0 continuam cobertos pelos gates existentes;
- o gate de build desta task passa sem warnings.
