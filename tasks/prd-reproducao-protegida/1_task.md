---
status: done
task_kind: enabling
blocked_by: []
gate: 'rg -q "^> \*\*Status:\*\* (ASCII aprovado|ASCII e Figma aprovados) pelo responsável em [0-9]{4}-[0-9]{2}-[0-9]{2}" docs/design/wireframes-aula.md'
gate_expect: "exit 0: o cabeçalho do documento de wireframes da aula registra a aprovação do ASCII pelo responsável"
---

# 1.0 Wireframes ASCII da tela da aula aprovados

**Fatia:** EN-01 (ASCII) · **Cobre:** Experiência do Usuário do PRD; RF-01, RF-04, RF-05, RF-06 (desenho); DP-03, DP-04, DP-05, DP-10 · **Spec:** `techspec.md#habilitadores-inevitaveis` · **ADR:** —

## Comportamento

Um documento de wireframes em `docs/design/wireframes-aula.md` desenha, para o SPA do aluno (design system do aluno), em
**desktop e celular de 390 px**, a **tela da aula** (`/aulas/:lessonId`) e todos os seus estados:

- **Estrutura da tela:** título da aula; área do player; aviso fixo de uso pessoal sob o player (DP-05); lista de módulos e
  aulas na ordem do currículo, com a aula atual destacada e indicada em texto (ao lado no desktop, abaixo do player no
  celular).
- **Player:** controles próprios (tocar e pausar, linha do tempo, volume, **velocidade** de 0,5x a 2x, tela cheia), com ordem
  de foco por teclado, nome acessível e foco visível. Mostra o **layout da tela cheia**, onde a marca d'água continua visível.
- **Marca d'água:** as zonas fixas em que o e-mail do aluno pode aparecer, que **não tocam a barra de controles**, a regra de
  trocar de zona sem repetir a anterior, e o aspecto legível sem atrapalhar o vídeo, também com o vídeo pausado.
- **Estados, cada um com a mensagem em português simples, sem termo técnico** (sem "sessão", "decisão", "token", "CDN"):
  carregando; reproduzindo; **sem acesso** ("Você não tem acesso a este curso", sem título de curso nem de aula); **acesso
  encerrado** ("Seu acesso a este curso terminou em dd/mm/aaaa"); **indisponibilidade da decisão** ("Não foi possível
  confirmar seu acesso agora", com a ação *Tentar de novo*, distinta de "sem acesso"); **aula não disponível** (a mesma
  mensagem para inexistente, de outra escola, de curso não publicado e removida da versão vigente); **vídeo indisponível**
  ("Esta aula está indisponível no momento"); **não foi possível iniciar a aula** (sem e-mail na sessão); **navegador sem
  suporte ao player**; **reprodução interrompida** por direito encerrado ou por indisponibilidade até o fim da validade
  (com *Tentar de novo*); a renovação em curso, que **não aparece** para o aluno.
- **Entrada e retorno:** o aluno sem sessão de login é levado ao login e volta à mesma aula; a rota direta de uma aula negada.
- Texto do aviso de uso pessoal e confirmação de que **nenhum texto da tela usa "protegido contra cópia"** ou equivalente.
- Acessibilidade: controles por teclado, mensagem de estado anunciada em região de status, marca d'água fora da ordem de
  leitura de leitor de tela, lista navegável como lista com a aula atual em texto, contraste da marca suficiente.

O cabeçalho do documento registra `> **Status:** ASCII aprovado pelo responsável em <AAAA-MM-DD>` — escrito **só depois**
da aprovação explícita do responsável. Pedido de ajuste volta ao ASCII antes do registro.

## Fora do escopo desta task

Figma (2.0) e qualquer código. Pré-visualização do professor, material complementar, "meus cursos", percentual de progresso e retomar de onde parou.

## Decisões fechadas

- Fluxo de design: wireframe ASCII → Figma → aprovação → código de tela (PRD, Experiência do Usuário).
- A tela da aula traz a lista de aulas do curso (DP-04) e o aviso de uso pessoal (DP-05); a velocidade entra e a escolha
  manual de qualidade não (DP-10); a marca d'água troca de posição ao menos a cada 30 s (DP-03).
- A aula sempre começa do início (DP-06); a tela não mostra progresso, percentual nem "continuar de onde parou".

## Modificar / Referenciar

Arquivos a criar não são listados: a estrutura vem das skills `dotnet` e `react`.

- **ref:** `prd.md` (Experiência do Usuário, RF-01 a RF-07, RN-R03, RN-R04, DP-03, DP-05, DP-10); `techspec.md#bloco-frontend` e `#v-02`; `api-contract.md` (estados e códigos de erro); `docs/design/wireframes-cortesias.md` (formato do documento e do registro de aprovação); `docs/design/wireframes-catalogo-vitrine.md` e `wireframes-conta-aluno.md` (design system do aluno); `docs/design/Components.md`; `DESIGN.md`
- **modificar:** `docs/design/` (documento novo `wireframes-aula.md`, que é a saída desta task)

## Verificações do projeto

| `docs/design` | — | Sem check automatizado; a aprovação é do responsável | Fluxo de design do SPA do aluno (CAP-001, CAP-003) |

## Pronto quando

- [x] Gate passa (exit 0).
- [x] O documento cobre a tela, o player, a tela cheia, a marca d'água e todos os estados acima, em desktop e celular de 390 px.
- [x] O responsável aprovou o ASCII explicitamente.
