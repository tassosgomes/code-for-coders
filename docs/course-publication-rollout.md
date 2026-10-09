# Publicação de cursos — implantação e conferência

A versão acordada é Audit 1.2.0 e Media 1.1.0. Instalar esses consumidores antes de liberar a autoria
no BFF. `COURSE_AUTHORING_ENABLED` continua false por padrão nos dois Compose.

1. No broker de cada ambiente, configurar `max_message_size = 67108864` e conferir com
   `rabbitmqctl environment`. O currículo C-07 contém até 100 módulos × 200 aulas e títulos de
   200 caracteres: o escape de JSON pode ultrapassar os 16 MiB padrão. O Compose local define
   o teto de 64 MiB; nos ambientes remotos, RabbitMQ pertence ao projeto de infraestrutura
   separado de cada host, e a configuração deve ser aplicada nele antes da liberação. O teste
   `CoursePublicationTests` mede e entrega
   o payload no limite por RabbitMQ real com esse mesmo teto.
2. Gerar/aplicar migrations somente pelo EF. Nesta entrega, aplicar `AddCoursePublication` e `AddContentPublicationOutbox` em
   Learning e `AddCourseReferences` em Media. Para histórico e descarte, aplicar também
   `AddPublishedContentFingerprint` em Learning antes de atualizar a API. Cursos já publicados
   recuperam a impressão do snapshot vigente na primeira edição após a atualização.
   Audit não teve mudança de modelo. Seguir o step
   de migration de `docs/student-registration-local-development.md` ou
   `scripts/remote-infra.sh migrate` no ambiente remoto; APIs não migram no boot.
3. Iniciar Audit e o papel worker de Media. Conferir os bindings
   `audit.events` → `audit.acts` / `auditoria.ato-praticado.v1` e
   `learning.events` → `media.course-publications` / `conteudo.versao-publicada.v1`.
   As filas têm DLQ quorum e limite de entrega. Filas e bases de smoke devem estar isoladas de
   outra stack que use o mesmo broker. O Compose ordena os receptores antes de Learning.
4. Iniciar Learning e reconciliar sua visão de vídeos conforme
   `docs/course-video-projection-rollout.md`. Só então ligar `COURSE_AUTHORING_ENABLED` e
   iniciar BFF/SPA. A disponibilidade da área não significa que a propagação assíncrona já terminou.
5. Como professor, abrir `/admin/autoria`, criar curso, módulo e aula com vídeo pronto. Conferir
   pendências focáveis antes de completar o currículo. Confirmar publicação com revisão exibida
   e nota opcional. A resposta 201 mostra versão, autor e momento; repetir a chave reproduz
   resposta e Location. A nota não é motivo e não sai de Learning.
6. Como administrador, abrir `/admin/auditoria`, filtrar tipo “Versão publicada”, abrir o registro
   conforme e seguir “Ver atos deste curso”. Título vem de Learning, sem `autoria.ler`; indisponibilidade
   de resolução conserva `curso` e ID. Conferir em Media apenas referências de IDs da versão vigente.
7. Monitorar `media.course_references.consumed`, `.lag` (segundos) e `.dead_lettered` via OTLP,
   atraso do outbox de Learning e filas `media.course-publications.dlq` / `audit.acts.dlq`.
   Readiness de Media acusa ausência de consumidor ou DLQ com mensagens. Recuperação preserva
   tenant, eventId e versionNumber; duplicata/versão anterior não muda a projeção.
8. Publicar um currículo com duas aulas, remover uma no rascunho e confirmar uma nova publicação.
   O histórico deve listar v2 vigente e v1 anterior com autor, momento e nota. Abrir e atualizar
   `/admin/autoria/{courseId}/versoes/1` deve manter o retrato original, com as duas aulas e sem
   controles de edição. Media deve manter apenas as referências da v2, inclusive após reentrega
   do fato v1; Auditoria deve ter os dois atos conformes.
9. Editar e retornar ao conteúdo anterior deve retirar “alterações não publicadas”, mesmo com
   revisão maior. Editar novamente e confirmar descarte deve restaurar a vigente, preservando
   IDs e incrementando revisão. Uma revisão desatualizada recebe `DRAFT_CHANGED`; a tela atualiza
   o curso e exige confirmação nova. Curso nunca publicado recusa descarte.

Não registrar nota, título, nome, token ou payload em logs, métricas ou erros. O payload do fato
contém somente IDs, títulos do currículo e metadados de publicação; descrição/nota ficam no
snapshot de Learning. A referência em Media não tem FK para vídeo, título ou nome.
