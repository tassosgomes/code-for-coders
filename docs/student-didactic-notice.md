# Aviso de ambiente didático

O `student-spa` exibe um aviso na vitrine, nos detalhes de curso e antes dos formulários
públicos de acesso, cadastro, confirmação e recuperação de senha. A faixa pública acompanha
o cabeçalho durante a rolagem. O aviso não pode ser dispensado pelo visitante.

A variável `DIDACTIC_NOTICE_ENABLED` controla todas essas exibições:

| Valor | Resultado |
| --- | --- |
| `true` | Exibe o aviso (desenvolvimento e demonstrações) |
| `false` | Remove o aviso (produção) |
| Ausente ou vazia | Exibe o aviso por padrão |

Somente as strings `true` e `false` são aceitas; outro valor não vazio causa erro de
configuração no boot do SPA. O aviso orienta o uso de dados fictícios; não altera a
persistência ou o processamento dos dados enviados.

## Configuração no deploy

Defina a variável no ambiente do stack Komodo ou no `.env` local ao host:

```dotenv
DIDACTIC_NOTICE_ENABLED=false
```

O Compose local e o Compose web do Komodo repassam esse valor ao container `student-spa`.
Os overrides remoto e estável herdam a configuração. O ambiente estável continua exibindo
o aviso até receber `false` explicitamente; estar no ambiente estável não significa que
a aplicação deixou de ser didática.

Reaplique o stack no Komodo ou recrie somente o SPA usando os mesmos arquivos Compose
do deploy. Exemplo do stack web de desenvolvimento:

```bash
docker compose --env-file .env -f docker-compose.komodo.web.yml up -d --no-deps --force-recreate student-spa
```

Depois, recarregue a página. Não é necessário recompilar a imagem, nem alterar o Cloudflare
Tunnel. Um simples `docker compose restart` não aplica mudanças nas variáveis do container.

O entrypoint gera `runtime-env.js` com o valor da variável; `src/config/env.ts` valida e
converte o valor para booleano. O arquivo de runtime é servido com `Cache-Control: no-store`.
Em desenvolvimento via Vite, o aviso também fica ativo por padrão. Para testar a remoção
nesse modo, forneça `DIDACTIC_NOTICE_ENABLED: 'false'` em `window.RUNTIME_ENV` antes de
carregar o bundle; a variável de shell sozinha não configura o runtime do Vite.

## Verificação

- Com `true`, confira a vitrine, os detalhes de curso e os formulários públicos por acesso direto.
- Confira rolagem, navegação, celular e temas claro e escuro.
- Com `false`, confira que o aviso desaparece de todos esses layouts sem deixar espaço reservado.
- Rode `npm run lint`, `npm run build` e `npm test` em `src/student-spa`.
