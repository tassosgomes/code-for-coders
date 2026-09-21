# ADR-0001: Monorepo de código

## Status

Accepted

## Identidade e escopo

- Caminho: `docs/adr/0001-monorepo-de-codigo.md` (numeração global do repositório)
- Domínios/componentes afetados: organização do código-fonte, serviços e aplicações da Fase 1, CI/CD e contratos entre serviços
- Origem histórica: `docs/foundation-plan.md`, Etapa 3 — decisão já tomada na fundação do produto; nenhum commit de origem disponível
- Substitui: Nenhuma

## Data

2026-09-21

## Contexto

O produto possui vários domínios e serviços, mas é construído por uma equipe de dois engenheiros com
codificação executada de forma autônoma por agentes. O plano de fundação precisa replicar o golden path
de `identity` para as demais unidades, mantendo os mesmos padrões de build, segurança, observabilidade,
contratos e operação. Espalhar esse trabalho por um repositório para cada serviço duplicaria configuração
e governança justamente no momento em que a plataforma ainda está sendo estabelecida.

O baseline arquitetural já decidiu por microsserviços com serviço internamente modular: serviços agrupam
domínios por afinidade de mudança, e módulos continuam protegidos por contratos e schemas próprios. A
questão deste ADR é a organização do código-fonte, não a fronteira de runtime. Um repositório pode
conter várias unidades de deploy sem transformar essas unidades em um único processo, uma única release
ou um único dono de dados.

## Decisão

O produto usará um único repositório de código, `code-for-coders`, com uma fronteira explícita por
serviço ou aplicação deployável. A raiz poderá concentrar defaults de toolchain, documentação e
guardrails comuns; isso não autoriza compartilhar runtime, banco, credencial ou ciclo de release entre
serviços.

Esta decisão não viola BA01. BA01 define serviço como unidade de deploy, escala e falha; não define
um repositório Git como essa unidade. A fronteira operacional continuará sendo verificável em cada
serviço:

- cada serviço continua sendo unidade de deploy, escala e falha;
- cada serviço tem imagem própria, pipeline próprio, banco e credencial próprios para sua persistência,
  e rollback próprio;
- nenhum serviço usa tabela ou credencial de outro serviço;
- serviços sem persistência própria, como uma SPA, não recebem um banco artificial, mas continuam tendo
  imagem, pipeline, escala, falha e rollback independentes;
- contratos compartilhados são consumidos como pacote versionado, e não como referência direta entre
  solutions;
- um deploy, rollback, incidente ou decisão de escala de um serviço não exige publicar os demais.

Assim, o monorepo reduz o custo de coordenação do código sem alterar a topologia, a propriedade dos
dados ou a independência operacional estabelecidas no baseline.

## Alternativas Consideradas

### Alternativa 1: Polyrepo

- **Descrição:** um repositório separado para cada serviço ou aplicação deployável.
- **Prós:** isolamento natural de histórico e permissões; releases independentes por construção; clones menores.
- **Contras:** duplicação de workflows, defaults, documentação e guardrails; mudanças transversais exigem coordenação entre repositórios; contratos e atualizações de plataforma podem divergir; manter muitos repositórios aumenta o custo de revisão para uma equipe pequena.
- **Por que rejeitada:** a independência de deploy já será garantida por pipeline, imagem e rollback por serviço. O polyrepo adicionaria custo de coordenação e drift sem contribuir para a fronteira de runtime necessária agora.

### Alternativa 2: Monorepo sem fronteiras

- **Descrição:** um único repositório em que serviços compartilham projetos internos, tabelas, credenciais ou um pipeline e release coordenados.
- **Prós:** menor atrito inicial para reutilizar código; alterações locais podem atravessar qualquer componente sem cerimônia; uma única execução de CI parece simples.
- **Contras:** cria acoplamento de build e deploy; permite banco compartilhado e ownership ambíguo; torna falhas e rollbacks coordenados; facilita que o monorepo vire um monólito distribuído; viola BA01 e os guardrails de propriedade de dados.
- **Por que rejeitada:** a localização física do código não pode apagar as fronteiras de serviço. O monorepo adotado terá limites verificáveis e pipelines seletivos por unidade.

## Consequências

### Positivas

- Um único lugar para aplicar e revisar defaults de toolchain, segurança, observabilidade e documentação.
- O golden path de `identity` pode ser replicado para as demais unidades com mudanças rastreáveis na mesma história.
- Alterações coordenadas em workflows, contratos e guardrails podem ser revisadas atomicamente.
- A equipe mantém descoberta, busca e revisão centralizadas sem abrir mão da operação independente.

### Negativas

- O repositório e a CI crescerão; workflows precisarão de filtros de caminho e gates específicos para não executar ou publicar unidades não afetadas.
- Guardrails de fronteira precisam ser mantidos continuamente; a proximidade física pode induzir referências diretas, compartilhamento de schema ou deploy conjunto.
- Uma mudança em defaults da raiz pode afetar vários serviços e exige validação de compatibilidade antes de ser adotada.
- Permissões de acesso ao repositório não isolam automaticamente o código de cada serviço; segregação adicional, se necessária, deverá ser decidida separadamente.

### Riscos

- **Um workflow deixar de detectar uma alteração relevante:** usar filtros de caminho por serviço, testes de contrato e self-test da plataforma; qualquer exceção de escopo deve ser explícita no workflow.
- **Código compartilhado virar acoplamento entre serviços:** manter contratos em pacote NuGet versionado, proibir referências entre solutions e revisar dependências de arquitetura.
- **Banco ou credencial compartilhados criarem ownership ambíguo:** provisionar banco e credencial por serviço, manter migration com o dono da tabela e bloquear acesso cruzado.
- **A independência operacional regredir para release coordenada:** publicar imagem e digest por serviço, operar pipeline próprio e provar rollback pelo digest anterior antes da produção.
- **O custo do monorepo superar o benefício com o crescimento:** acompanhar tempo de CI, tamanho das mudanças e frequência de conflitos; só considerar divisão quando houver evidência operacional de que os guardrails não são suficientes.

## Notas de Implementação

- Cada unidade deployável deve manter seu contexto de build, Dockerfile, nome de imagem, workflow e
  filtros de caminho identificáveis. Arquivos comuns na raiz são insumos de build e políticas, não uma
  unidade de execução compartilhada.
- O pacote `Contracts` deve ser publicado e consumido com versionamento; projetos de solutions
  diferentes não devem ser referenciados diretamente.
- Cada serviço de backend é dono de seu banco, credencial, schema e migrations. Dependências de
  plataforma compartilhadas, como o broker e o coletor OTLP, não transferem ownership de dados nem
  tornam o deploy conjunto.
- A extração futura de um módulo para um serviço próprio deve mover projetos e preservar a fronteira
  de domínio, sem exigir redesenho por causa da escolha do repositório.

## Referências

- [Plano de Fundação — Etapa 3](../foundation-plan.md#etapa-3--registrar-e-fechar-o-fluxo)
- [Baseline Arquitetural — decisão de microsserviços com serviço internamente modular](../../context/architecture-baseline.md#decisão-microsserviços-com-serviço-internamente-modular)
- [Baseline Arquitetural — dependências de plataforma exigidas](../../context/architecture-baseline.md#dependências-de-plataforma-que-este-estilo-exige)
- [Baseline Arquitetural — decisões registradas, incluindo BA01 e BA08](../../context/architecture-baseline.md#decisões-registradas)
- [Vision — restrições globais e equipe](../../vision.md#5-restrições-globais-global-constraints)
