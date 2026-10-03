# API Contract — reprodução protegida (BFF do aluno)

> **Gerado a partir de:** [api-contract.yaml](api-contract.yaml), OpenAPI 3.1.0, versão do contrato **1.0.0** (Novo, sem versão anterior)  
> **PRD:** [prd.md](prd.md) v1.0, aprovado em 2026-10-03 (`CAP-007`)  
> **Estado:** Aprovado para implementação em 2026-10-03. Este documento é derivado do YAML, que é a fonte única; não o edite à mão.

Registra o acordo para a implementação deste PRD. PRDs posteriores podem evoluí-lo. Catalogação e mecanismos de atualização do acervo são da plataforma.

## Premissas e Decisões

| Decisão | Escolha | Motivo |
|---|---|---|
| Interface | SPA → `bff-student`, sob `/api/v1` | Mesma base das APIs públicas do aluno (CAP-001). |
| Autenticação | Cookie `student_session`; escritas com `X-CSRF-Token` | ADR-0003 e G18. Os GET de playlist e chave dispensam CSRF: não alteram estado. |
| Superfície única | Playlist e chave passam pelo BFF; segmentos vêm direto da distribuição | G15 (o SPA só fala com o BFF) sem trazer a banda do vídeo para o BFF (C-01). |
| Credencial dos segmentos | Endereço sem credencial na playlist + `segmentAccess.query` vigente | Renovar a sessão troca a credencial sem recarregar a playlist (C-06). |
| Exceção ao gate HTTP | `application/vnd.apple.mpegurl` e `application/octet-stream` em 3 respostas 200 | Playlist HLS e chave AES-128 não são JSON (C-08). Corpo de requisição segue só JSON. |
| Erros | RFC 9457 com `code`, `traceId`; `ACCESS_DENIED` com `reason` e `accessEndedAt` | `ACCESS_DECISION_UNAVAILABLE` (503) nunca se confunde com `ACCESS_DENIED` (403): RN-R02, DP-08. |
| Paginação | Não se aplica | Nenhuma coleção paginável: a lista de aulas é a estrutura de um curso (até 100 módulos e 200 aulas por módulo), um objeto só (RF-06). |
| Datas | ISO 8601 UTC | A data exibida ao aluno (`accessEndedAt`) é convertida para o fuso da escola. |
| Cache | `no-store` em sessão, playlist e chave; `private, no-store` na aula | Nada de vídeo ou chave fica em cache de navegador, BFF ou distribuição (RN-M01, RN-M08). |

## Resumo de Operações

| Método | Path | `operationId` | Descrição | Segurança | Status |
|---|---|---|---|---|---|
| `GET` | `/lessons/{lessonId}` | `getStudentLesson` | Obter a aula e a lista de aulas do curso | StudentSessionCookie | 200, 403, 404, 503, 401, 502, 504 |
| `POST` | `/lessons/{lessonId}/playback-sessions` | `openPlaybackSession` | Abrir uma Sessão de Reprodução | StudentSessionCookie | 201, 404, 409, 422, 503, 401, 403, 502, 504 |
| `POST` | `/playback-sessions/{sessionId}/renewals` | `renewPlaybackSession` | Renovar a Sessão de Reprodução | StudentSessionCookie | 200, 404, 410, 422, 503, 401, 403, 502, 504 |
| `GET` | `/playback-sessions/{sessionId}/playlist` | `getPlaybackPlaylist` | Obter a playlist principal (HLS) | StudentSessionCookie | 200, 404, 410, 401, 502, 504 |
| `GET` | `/playback-sessions/{sessionId}/variants/{quality}` | `getPlaybackVariantPlaylist` | Obter a playlist de uma qualidade (HLS) | StudentSessionCookie | 200, 404, 410, 401, 502, 504 |
| `GET` | `/playback-sessions/{sessionId}/key` | `getPlaybackKey` | Obter a chave de cifra do vídeo | StudentSessionCookie | 200, 404, 410, 401, 502, 504 |
| `POST` | `/playback-sessions/{sessionId}/progress` | `recordPlaybackProgress` | Informar o avanço da reprodução | StudentSessionCookie | 200, 400, 404, 410, 401, 403, 502, 504 |

## Operações Detalhadas

### `GET /lessons/{lessonId}` — Obter a aula e a lista de aulas do curso

Devolve a aula pedida e o curso **na versão vigente**, para a tela da aula (RF-01, RF-06). Ordem das verificações: (1) a aula existe na versão vigente de um curso publicado da escola do aluno; (2) Matrícula responde que o aluno pode acessar o curso agora (`decideAccessInternal`, cache de até 30 s, falha fechada: BA07, G11). Nada do conteúdo é devolvido antes da decisão positiva (RN-R07). Todas as aulas estão abertas a quem tem direito; não há liberação progressiva (RN-R06, G20).

`operationId`: `getStudentLesson`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `lessonId` | path | sim | Identidade estável da aula (RN-C07). Opaca para quem a recebe. |

#### Response 200

A aula e o curso na versão vigente.

Exemplo `aula`:
```json
{
  "lesson": {
    "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
    "moduleId": "c1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
    "title": "Injeção de dependência na prática",
    "position": 2
  },
  "course": {
    "courseId": "9a7e5c31-2b4d-4f68-8e01-6d3c5a7b9f24",
    "title": "APIs com .NET do zero ao deploy",
    "versionNumber": 3,
    "modules": [
      {
        "moduleId": "c1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
        "title": "Fundamentos",
        "position": 1,
        "lessons": [
          {
            "lessonId": "5c2e8f4d-7b60-4a13-9a13-3f6a1c527d84",
            "title": "Visão geral do curso",
            "position": 1
          },
          {
            "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
            "title": "Injeção de dependência na prática",
            "position": 2
          }
        ]
      },
      {
        "moduleId": "d2e3f4a5-b6c7-4d8e-9fa0-1b2c3d4e5f60",
        "title": "Persistência",
        "position": 2,
        "lessons": [
          {
            "lessonId": "7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d",
            "title": "Migrations com Entity Framework",
            "position": 1
          }
        ]
      }
    ]
  }
}
```

#### Response 403

Matrícula respondeu que o aluno não pode acessar o curso agora. `reason` distingue nunca ter tido de ter vencido; nada do conteúdo do curso é revelado.

Exemplo `semAcesso`:
```json
{
  "type": "about:blank",
  "title": "Você não tem acesso a este curso.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "no-grant"
}
```

Exemplo `vencido`:
```json
{
  "type": "about:blank",
  "title": "Seu acesso a este curso terminou.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "grant-ended",
  "accessEndedAt": "2026-10-04T03:00:00Z"
}
```

#### Response 404

Aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente. A resposta **não distingue** o motivo (RN-R07).

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Esta aula não está disponível.",
  "status": 404,
  "code": "LESSON_NOT_AVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 503

Matrícula não respondeu a tempo ou respondeu com erro. **Não é negação** (RN-R02, DP-08): nenhuma sessão abre nem se estende. O cliente oferece *Tentar de novo*.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Não foi possível confirmar seu acesso agora.",
  "status": 503,
  "code": "ACCESS_DECISION_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---

### `POST /lessons/{lessonId}/playback-sessions` — Abrir uma Sessão de Reprodução

Abre a Sessão de Reprodução do aluno corrente para a aula (RF-01, RF-02). Ordem obrigatória (RN-M10): (1) a aula alegada está entre as Referências de Uso de um vídeo (404 `LESSON_NOT_AVAILABLE`, **antes** de consultar Matrícula); (2) o vídeo está pronto (409 `MEDIA_NOT_READY`); (3) Matrícula responde que o aluno pode acessar o curso agora (403 `ACCESS_DENIED`; sem resposta, 503 `ACCESS_DECISION_UNAVAILABLE`, falha fechada); (4) o e-mail do aluno está disponível para a marca d'água (422 `WATERMARK_UNAVAILABLE`). Cada chamada abre uma sessão nova; não é idempotente e não há limite de sessões simultâneas do mesmo aluno (RN-M13, BA16, G24). A sessão vale 5 minutos (DP-01).

`operationId`: `openPlaybackSession`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `lessonId` | path | sim | Identidade estável da aula (RN-C07). Opaca para quem a recebe. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão do aluno (`csrfToken` de `getCurrentStudentSession`). |

#### Response 201

Sessão aberta.

Exemplo `sessao`:
```json
{
  "sessionId": "e4b8c6a2-1d3f-4957-b0a8-6c2d4e8f1a73",
  "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
  "expiresAt": "2026-10-05T14:05:00Z",
  "renewAfter": "2026-10-05T14:03:30Z",
  "watermark": {
    "text": "marina.alves@example.com",
    "repositionSeconds": 30
  },
  "progress": {
    "intervalSeconds": 30,
    "minGapSeconds": 10
  },
  "segmentAccess": {
    "query": "sa=MjAyNi0xMC0wNVQxNDowNTowMFo.k3Jq9xT2vL0pQw",
    "expiresAt": "2026-10-05T14:05:00Z"
  }
}
```

#### Response 404

Aula inexistente, de outra escola, de curso não publicado ou removida da versão vigente. A resposta **não distingue** o motivo (RN-R07).

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Esta aula não está disponível.",
  "status": 404,
  "code": "LESSON_NOT_AVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 409

O vídeo da aula ainda não está pronto ou falhou (RN-M05, RN-M09). Mensagem ao aluno: "Esta aula está indisponível no momento"; nenhum detalhe técnico.

Exemplo `naoPronto`:
```json
{
  "type": "about:blank",
  "title": "Esta aula está indisponível no momento.",
  "status": 409,
  "code": "MEDIA_NOT_READY",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 422

A sessão não pode trazer o e-mail do aluno para a marca d'água: **sem marca, sem reprodução** (RN-R03).

Exemplo `semMarca`:
```json
{
  "type": "about:blank",
  "title": "Não foi possível iniciar a aula.",
  "status": 422,
  "code": "WATERMARK_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 503

Matrícula não respondeu a tempo ou respondeu com erro. **Não é negação** (RN-R02, DP-08): nenhuma sessão abre nem se estende. O cliente oferece *Tentar de novo*.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Não foi possível confirmar seu acesso agora.",
  "status": 503,
  "code": "ACCESS_DECISION_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 403

`CSRF_INVALID` (prova ausente ou inválida) ou `ACCESS_DENIED` (Matrícula respondeu que o aluno não pode acessar o curso agora; `reason` e, quando vencido, `accessEndedAt`). Os dois não se confundem: o cliente trata o `code`.

Exemplo `csrf`:
```json
{
  "type": "about:blank",
  "title": "Prova CSRF ausente ou inválida.",
  "status": 403,
  "code": "CSRF_INVALID",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

Exemplo `semAcesso`:
```json
{
  "type": "about:blank",
  "title": "Você não tem acesso a este curso.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "no-grant"
}
```

Exemplo `vencido`:
```json
{
  "type": "about:blank",
  "title": "Seu acesso a este curso terminou.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "grant-ended",
  "accessEndedAt": "2026-10-04T03:00:00Z"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---

### `POST /playback-sessions/{sessionId}/renewals` — Renovar a Sessão de Reprodução

Estende a reprodução em andamento **repetindo a decisão de acesso** (RN-M11, RF-02): a validade volta a 5 minutos e a `segmentAccess` é substituída. O player chama a partir de `renewAfter` e repete a tentativa enquanto a resposta for 503, até `expiresAt`. Decisão negada (403) encerra a reprodução; decisão indisponível até o fim da validade também (RN-R02): nenhuma sessão se estende por padrão. Sessão já vencida não renova (410): o player abre outra. Idempotente por natureza: repetir a chamada só repete a decisão e devolve a validade vigente.

`operationId`: `renewPlaybackSession`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `sessionId` | path | sim | Identificador da Sessão de Reprodução. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão do aluno (`csrfToken` de `getCurrentStudentSession`). |

#### Response 200

Sessão renovada: mesmo `sessionId`, nova validade e nova `segmentAccess`.

Exemplo `renovada`:
```json
{
  "sessionId": "e4b8c6a2-1d3f-4957-b0a8-6c2d4e8f1a73",
  "lessonId": "3f6a1c52-7d84-4b0e-9a13-5c2e8f4d7b60",
  "expiresAt": "2026-10-05T14:10:00Z",
  "renewAfter": "2026-10-05T14:08:30Z",
  "watermark": {
    "text": "marina.alves@example.com",
    "repositionSeconds": 30
  },
  "progress": {
    "intervalSeconds": 30,
    "minGapSeconds": 10
  },
  "segmentAccess": {
    "query": "sa=MjAyNi0xMC0wNVQxNDoxMDowMFo.p8Wd2mQ7sN4xZa",
    "expiresAt": "2026-10-05T14:10:00Z"
  }
}
```

#### Response 404

Sessão desconhecida, de outro aluno ou de outra escola; não distingue o motivo.

Exemplo `naoEncontrada`:
```json
{
  "type": "about:blank",
  "title": "Sessão de reprodução não encontrada.",
  "status": 404,
  "code": "PLAYBACK_SESSION_NOT_FOUND",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 410

A validade da sessão terminou sem renovação. O player abre uma sessão nova (`openPlaybackSession`); a posição de reprodução fica com o próprio player.

Exemplo `expirada`:
```json
{
  "type": "about:blank",
  "title": "A sessão de reprodução terminou.",
  "status": 410,
  "code": "PLAYBACK_SESSION_EXPIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 422

A sessão não pode trazer o e-mail do aluno (RN-R03): a reprodução não se estende.

Exemplo `semMarca`:
```json
{
  "type": "about:blank",
  "title": "Não foi possível iniciar a aula.",
  "status": 422,
  "code": "WATERMARK_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 503

Matrícula não respondeu a tempo ou respondeu com erro. **Não é negação** (RN-R02, DP-08): nenhuma sessão abre nem se estende. O cliente oferece *Tentar de novo*.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Não foi possível confirmar seu acesso agora.",
  "status": 503,
  "code": "ACCESS_DECISION_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 403

`CSRF_INVALID` (prova ausente ou inválida) ou `ACCESS_DENIED` (Matrícula respondeu que o aluno não pode acessar o curso agora; `reason` e, quando vencido, `accessEndedAt`). Os dois não se confundem: o cliente trata o `code`.

Exemplo `csrf`:
```json
{
  "type": "about:blank",
  "title": "Prova CSRF ausente ou inválida.",
  "status": 403,
  "code": "CSRF_INVALID",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

Exemplo `semAcesso`:
```json
{
  "type": "about:blank",
  "title": "Você não tem acesso a este curso.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "no-grant"
}
```

Exemplo `vencido`:
```json
{
  "type": "about:blank",
  "title": "Seu acesso a este curso terminou.",
  "status": 403,
  "code": "ACCESS_DENIED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01",
  "reason": "grant-ended",
  "accessEndedAt": "2026-10-04T03:00:00Z"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---

### `GET /playback-sessions/{sessionId}/playlist` — Obter a playlist principal (HLS)

Playlist HLS principal da sessão, listando as qualidades que o vídeo tem (RF-03, RF-05). Os endereços das variantes são **relativos** (`variants/{quality}`), de modo que o BFF repassa o corpo sem reescrevê-lo. Exceção ao gate HTTP: o corpo é `application/vnd.apple.mpegurl`, não JSON (C-08). Recusada sem sessão válida, depois do fim da validade, ou para sessão de outro aluno (RN-M01).

`operationId`: `getPlaybackPlaylist`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `sessionId` | path | sim | Identificador da Sessão de Reprodução. |

#### Response 200

Playlist principal.

Exemplo (`application/vnd.apple.mpegurl`):
```
#EXTM3U
#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080
variants/1080p
#EXT-X-STREAM-INF:BANDWIDTH=2800000,RESOLUTION=1280x720
variants/720p
#EXT-X-STREAM-INF:BANDWIDTH=1000000,RESOLUTION=854x480
variants/480p
```

#### Response 404

Sessão desconhecida, de outro aluno ou de outra escola; não distingue o motivo.

Exemplo `naoEncontrada`:
```json
{
  "type": "about:blank",
  "title": "Sessão de reprodução não encontrada.",
  "status": 404,
  "code": "PLAYBACK_SESSION_NOT_FOUND",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 410

A validade da sessão terminou sem renovação. O player abre uma sessão nova (`openPlaybackSession`); a posição de reprodução fica com o próprio player.

Exemplo `expirada`:
```json
{
  "type": "about:blank",
  "title": "A sessão de reprodução terminou.",
  "status": 410,
  "code": "PLAYBACK_SESSION_EXPIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---

### `GET /playback-sessions/{sessionId}/variants/{quality}` — Obter a playlist de uma qualidade (HLS)

Playlist HLS de uma qualidade. Os segmentos aparecem como endereços **absolutos da distribuição, sem credencial**; o player acrescenta `segmentAccess.query` a cada pedido de segmento (C-06). A URI da chave é **relativa** (`../key`) e aponta para o recurso `key` desta sessão, nunca para um endereço estável do armazenamento (ADR-0006, item 4). Nenhum endereço carrega o e-mail do aluno (G23). Exceção ao gate HTTP (C-08): `application/vnd.apple.mpegurl`.

`operationId`: `getPlaybackVariantPlaylist`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `sessionId` | path | sim | Identificador da Sessão de Reprodução. |
| `quality` | path | sim | Qualidade da variante. |

#### Response 200

Playlist da qualidade.

Exemplo (`application/vnd.apple.mpegurl`):
```
#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:6
#EXT-X-PLAYLIST-TYPE:VOD
#EXT-X-KEY:METHOD=AES-128,URI="../key"
#EXTINF:6.000,
https://media.example.com/v/0b1c2d3e/720p/seg-00001.ts
#EXTINF:6.000,
https://media.example.com/v/0b1c2d3e/720p/seg-00002.ts
#EXT-X-ENDLIST
```

#### Response 404

Sessão desconhecida, de outro aluno ou de outra escola; não distingue o motivo.

Exemplo `naoEncontrada`:
```json
{
  "type": "about:blank",
  "title": "Sessão de reprodução não encontrada.",
  "status": 404,
  "code": "PLAYBACK_SESSION_NOT_FOUND",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 410

A validade da sessão terminou sem renovação. O player abre uma sessão nova (`openPlaybackSession`); a posição de reprodução fica com o próprio player.

Exemplo `expirada`:
```json
{
  "type": "about:blank",
  "title": "A sessão de reprodução terminou.",
  "status": 410,
  "code": "PLAYBACK_SESSION_EXPIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---

### `GET /playback-sessions/{sessionId}/key` — Obter a chave de cifra do vídeo

Entrega a chave AES-128 **somente** para uma Sessão de Reprodução válida **daquele** vídeo, pelo próprio serviço de Mídia e nunca pela distribuição nem por endereço estável (RN-M08, RF-03). Resposta com `Cache-Control: no-store`. A chave nunca aparece em log, span, mensagem ou outra resposta (ADR-0006). Exceção ao gate HTTP (C-08): `application/octet-stream`.

`operationId`: `getPlaybackKey`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `sessionId` | path | sim | Identificador da Sessão de Reprodução. |

#### Response 200

Chave de cifra AES-128 do vídeo: 16 bytes.

Corpo: `application/octet-stream`, binary.

#### Response 404

Sessão desconhecida, de outro aluno ou de outra escola; não distingue o motivo.

Exemplo `naoEncontrada`:
```json
{
  "type": "about:blank",
  "title": "Sessão de reprodução não encontrada.",
  "status": 404,
  "code": "PLAYBACK_SESSION_NOT_FOUND",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 410

A validade da sessão terminou sem renovação. O player abre uma sessão nova (`openPlaybackSession`); a posição de reprodução fica com o próprio player.

Exemplo `expirada`:
```json
{
  "type": "about:blank",
  "title": "A sessão de reprodução terminou.",
  "status": 410,
  "code": "PLAYBACK_SESSION_EXPIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---

### `POST /playback-sessions/{sessionId}/progress` — Informar o avanço da reprodução

O player informa onde a reprodução está (RF-07): a cada `progress.intervalSeconds` de reprodução contínua e ao pausar, sair e chegar ao fim. Media aceita enquanto a sessão for do aluno, **mesmo depois do fim da validade** (a decisão negada não apaga o que foi assistido: RF-07), e publica o fato `midia.reproducao-avancou.v1` (`asyncapi-contract.yaml`). Idempotente por (`sessionId`, `sequence`); menos de `progress.minGapSeconds` desde o avanço anterior aceito da mesma sessão não gera fato (RN-R05). A saída da aula é enviada com `fetch` em modo `keepalive`, porque a escrita exige o cabeçalho `X-CSRF-Token`, que `sendBeacon` não permite.

`operationId`: `recordPlaybackProgress`

| Parâmetro | Em | Obrigatório | Descrição |
|---|---|---|---|
| `sessionId` | path | sim | Identificador da Sessão de Reprodução. |
| `X-CSRF-Token` | header | sim | Prova CSRF vinculada à sessão do aluno (`csrfToken` de `getCurrentStudentSession`). |

**Request body**

Exemplo `heartbeat`:
```json
{
  "sequence": 4,
  "positionSeconds": 132,
  "reason": "heartbeat"
}
```

Exemplo `pausa`:
```json
{
  "sequence": 5,
  "positionSeconds": 252,
  "reason": "paused"
}
```

#### Response 200

Avanço tratado; `recorded` diz se virou fato.

Exemplo `aceito`:
```json
{
  "recorded": true
}
```

#### Response 400

JSON malformado, parâmetro ou campo inválido.

Exemplo `invalido`:
```json
{
  "type": "about:blank",
  "title": "Requisição inválida.",
  "status": 400,
  "code": "VALIDATION_ERROR",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 404

Sessão desconhecida, de outro aluno ou de outra escola; não distingue o motivo.

Exemplo `naoEncontrada`:
```json
{
  "type": "about:blank",
  "title": "Sessão de reprodução não encontrada.",
  "status": 404,
  "code": "PLAYBACK_SESSION_NOT_FOUND",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 410

O avanço chegou depois da janela de aceitação da sessão (valor da TechSpec). O que foi informado antes continua valendo.

Exemplo `expirada`:
```json
{
  "type": "about:blank",
  "title": "A sessão de reprodução terminou.",
  "status": 410,
  "code": "PLAYBACK_SESSION_EXPIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 401

Sessão do aluno ausente, expirada ou revogada.

Exemplo `sessaoAusente`:
```json
{
  "type": "about:blank",
  "title": "Sessão ausente, expirada ou revogada.",
  "status": 401,
  "code": "SESSION_REQUIRED",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 403

Prova CSRF ausente ou inválida.

Exemplo `csrf`:
```json
{
  "type": "about:blank",
  "title": "Prova CSRF ausente ou inválida.",
  "status": 403,
  "code": "CSRF_INVALID",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 502

`learning`, `media` ou `identity` não respondeu ou falhou: `LEARNING_UNAVAILABLE`, `MEDIA_UNAVAILABLE` ou `IDENTITY_UNAVAILABLE`. Nada foi alterado de forma visível ao cliente.

Exemplo `indisponivel`:
```json
{
  "type": "about:blank",
  "title": "Serviço temporariamente indisponível.",
  "status": 502,
  "code": "UPSTREAM_UNAVAILABLE",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

#### Response 504

O serviço não respondeu no tempo limite.

Exemplo `tempo`:
```json
{
  "type": "about:blank",
  "title": "O serviço demorou a responder.",
  "status": 504,
  "code": "UPSTREAM_TIMEOUT",
  "traceId": "00-9f2b5d6c4e7a8b90123456789abcdef0-0123456789abcdef-01"
}
```

---
