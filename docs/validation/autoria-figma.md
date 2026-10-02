# Alinhamento das telas de Autoria ao Figma

Validação de 02/10/2026 para a TASK-7, na branch `feature/figma-screen-alignment`.

Referência: [Screens — Autoria, Code4Coders Design System](https://www.figma.com/design/kKNfxTqSFT5IfQHcoTh5gn?node-id=155-7451).

## Implementação

A lista de cursos, o editor, os diálogos, os seletores, o histórico, a versão publicada e o início do professor foram alinhados com os frames do Figma. Foram ajustados a hierarquia do conteúdo, os cartões, os espaçamentos, a tipografia, os campos, os badges e a paginação.

O editor apresenta nível e pré-requisito antes do currículo, agrupa as ações de publicação com as abas e mantém o descarte abaixo dos módulos. O tema escuro usa os tokens do Figma e pode ser alternado na barra superior. Em mobile, a tabela vira cartões, os campos de nível ficam empilhados e os seletores abrem como painéis inferiores.

Os componentes existentes, CSS e ícones de `lucide-react` foram reutilizados. A navegação das abas aceita setas, Home e End. Os diálogos recebem foco ao abrir, mantêm Tab dentro deles e restauram o foco ao fechar. A cópia da referência usa o ID completo e oferece o texto para cópia manual quando o navegador bloqueia a área de transferência.

## Comparação visual

As capturas abaixo foram inspecionadas junto às referências do Figma. Os dados, as datas, as versões e a quantidade de itens variam conforme a resposta da API; os exemplos são fixtures de validação.

| Tela / estado | Frame de referência | Evidência |
| --- | --- | --- |
| Cursos da escola, desktop | `158:88` | [Captura](autoria-figma/lista-desktop.png) |
| Cursos da escola, mobile | `161:21938` | [Captura](autoria-figma/lista-mobile.png) |
| Escola sem cursos | `160:366` | [Captura](autoria-figma/lista-vazia.png) |
| Falha ao carregar cursos | `160:596` | [Captura](autoria-figma/lista-erro.png) |
| Novo curso | `196:14359` | [Captura](autoria-figma/criar-curso.png) |
| Editor, desktop Light | `193:22487` | [Captura](autoria-figma/editor-desktop.png) |
| Editor, desktop Dark | `193:24792` | [Captura](autoria-figma/editor-dark.png) |
| Editor, mobile | `197:13879` | [Captura](autoria-figma/editor-mobile.png) |
| Edição de dados, mobile | família A2/A3 | [Captura](autoria-figma/editar-mobile.png) |
| Publicação | família A7 | [Captura](autoria-figma/publicacao.png) |
| Descarte | `161:19606` | [Captura](autoria-figma/descartar.png) |
| Seletor de vídeo, desktop | `160:18397` | [Captura](autoria-figma/seletor-video.png) |
| Seletor de vídeo, mobile | `161:22272` | [Captura](autoria-figma/seletor-video-mobile.png) |
| Cursos recomendados | `194:12148` | [Captura](autoria-figma/seletor-cursos.png) |
| Histórico | `161:20336` | [Captura](autoria-figma/historico.png) |
| Versão publicada | `196:14175` | [Captura](autoria-figma/versao-publicada.png) |
| Início do professor | `161:21667` | [Captura](autoria-figma/inicio-professor.png) |

## Verificações no navegador

Playwright com Chromium e SPA executada em `desenv-server` (`192.168.0.5:5181`). As respostas da API foram interceptadas com fixtures para tornar os estados previsíveis. A validação visual não dependeu de alterações em dados reais nem de infraestrutura local.

- Desktop: 1440 × 900; mobile: 390 × 844.
- Editor sem rolagem horizontal nas larguras 320, 390, 768 e 1024 px, também conferido em mobile Dark.
- Painel de vídeo em mobile com largura de 390 px; diálogo de edição com 358 px, preservando margens de 16 px.
- Fundo Dark confirmado como `#242033`; botão primário em hover mantém `#a78bfa` e texto `#0d0b14`.
- Abertura e fechamento por Escape, entrada e restauração de foco e troca das abas por teclado.
- Busca e seleção de vídeos, abertura do seletor de recomendações e consulta de versão histórica.
- Abertura de criação a partir da lista vazia, limpeza do filtro sem resultados e recuperação após erro 502 simulado.
- Falha da área de transferência anuncia o ID completo para cópia manual; sucesso e falha também cobertos por testes automatizados.

O ambiente visual forneceu `runtime-env.js` pela interceptação do navegador. Como o Vite remoto foi acessado por HTTP, também recebeu um substituto de `crypto.randomUUID` apenas no navegador de teste. Essas adaptações pertencem ao ambiente de validação; a aplicação conserva suas APIs de navegador existentes. Após preparar esse ambiente, os percursos normais não geraram erros JavaScript. A falha 502 usada no teste de recuperação gera o erro de rede esperado.

## Gates

Executados no servidor de testes, em `/tmp/code-for-coders-figma/src/admin-spa`, com Node 24 e npm 11:

```sh
npm run lint
npm run build
npm test -- --maxWorkers=2
```

O build verifica os tipos com `tsc --noEmit`. Permanecem os avisos existentes de `runtime-env.js` externo ao bundle e tamanho do chunk principal, além do aviso de `HydrateFallback` nos testes do roteador. O diff também foi verificado com `git diff --check`.

Resultado final sobre a `main`: lint aprovado, build aprovado e **209 testes aprovados em 39 arquivos**. Os quatro novos testes cobrem teclado nas abas, sucesso e falha na cópia da referência e foco/Tab/Escape dos diálogos. Os testes existentes foram atualizados para os textos e a apresentação aprovados no Figma.

A rodada inicial teve 235 testes aprovados em 44 arquivos sobre a base de trabalho. Antes de abrir a PR, o commit desta entrega foi reaplicado sobre a `main` (`f0db614`), retirando da branch os commits de Cortesias ainda não integrados. Os gates foram repetidos nessa base e o navegador confirmou edição, fechamento com Escape, histórico por teclado e cartões do início do professor.

Esta rodada cobre apresentação e comportamento da SPA com APIs simuladas; não constitui validação integrada dos serviços de backend.
