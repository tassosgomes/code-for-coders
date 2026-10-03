import axios from 'axios';
import { ArrowLeft, CircleCheck, Copy, Info, LoaderCircle, Plus, TriangleAlert } from 'lucide-react';
import { Link, Navigate, useLocation, useNavigate, useNavigationType } from 'react-router';
import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';

import { paths } from '@/config/paths';
import { formatOfferAuditAttribute, getOfferAuditAttributeEntries } from '@/features/audit-trail/utils/format-offer-audit-attribute';
import { useConfirmAuditRecordComplement } from '@/features/audit-trail/api/confirm-audit-record-complement';
import { useAuditRecord, type AuditRecordDetail } from '@/features/audit-trail/api/get-audit-record';
import { AuditTrailForbidden } from '@/features/audit-trail/components/audit-trail-forbidden';
import { parseAuditTrailNavigationState, type AuditTrailPersonFilter } from '@/features/audit-trail/types/audit-trail-navigation';

const typeLabels: Record<string, string> = {
  'cortesia-concedida': 'Cortesia concedida',
  'oferta-publicada': 'Oferta publicada',
  'oferta-alterada': 'Oferta alterada',
  'oferta-despublicada': 'Oferta despublicada',
  'versao-publicada': 'Versão publicada',
  'papel-concedido': 'Papel concedido',
  'papel-revogado': 'Papel revogado',
  'convite-interno-emitido': 'Convite emitido',
  'convite-interno-aceito': 'Convite aceito',
};

const nonComplianceLabels: Record<string, string> = {
  'tipo-desconhecido': 'Tipo de ato desconhecido',
  'autor-ausente': 'Autor não informado pela origem',
  'alvo-ausente': 'Alvo não informado pela origem',
  'motivo-ausente': 'Motivo obrigatório não informado',
  'momento-ausente': 'Momento do ato não informado',
  'complemento-invalido': 'Conteúdo complementar inválido',
  'motivo-excede-limite': 'Motivo acima do limite de tamanho',
};

type AuditRecordDetailScreenProps = {
  recordId: string;
};

export const AuditRecordDetailScreen = ({ recordId }: AuditRecordDetailScreenProps) => {
  const query = useAuditRecord(recordId);
  const { refetch } = query;
  const confirmationMutation = useConfirmAuditRecordComplement();
  const confirmationButtonRef = useRef<HTMLButtonElement>(null);
  const [confirmationFormOpen, setConfirmationFormOpen] = useState(false);
  const [explanation, setExplanation] = useState('');
  const [idempotencyKey, setIdempotencyKey] = useState<string | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const [retryableError, setRetryableError] = useState(false);
  const [confirmationError, setConfirmationError] = useState<string | null>(null);
  const [confirmationStatus, setConfirmationStatus] = useState<number | null>(null);
  const [pendingConfirmation, setPendingConfirmation] = useState<{ confirmationId: string; startedAt: number } | null>(null);
  const [confirmationTimedOut, setConfirmationTimedOut] = useState(false);
  const [confirmationRecorded, setConfirmationRecorded] = useState(false);
  const location = useLocation();
  const navigationType = useNavigationType();
  const locationState = navigationType === 'POP' ? null : parseAuditTrailNavigationState(location.state);
  const returnState = locationState?.returnToAuditList
    ? { restoreAuditList: locationState.returnToAuditList }
    : undefined;
  const backLink = <Link className="audit-back-link" state={returnState} to={paths.auditTrail.getHref()}>
    <ArrowLeft aria-hidden="true" size={16} /> Trilha de auditoria
  </Link>;

  useEffect(() => {
    if (!pendingConfirmation) return;

    const isConfirmed = (complements: AuditRecordDetail['complements'] | undefined) =>
      complements?.some(complement => complement.confirmationId === pendingConfirmation.confirmationId) ?? false;
    let cancelled = false;
    let timerId: number | undefined;
    const poll = async () => {
      const result = await refetch();
      if (cancelled) return;
      if (isConfirmed(result.data?.complements)) {
        setPendingConfirmation(null);
        setConfirmationTimedOut(false);
        setConfirmationRecorded(true);
        return;
      }

      if (Date.now() - pendingConfirmation.startedAt >= 30_000) {
        setConfirmationTimedOut(true);
        return;
      }

      timerId = window.setTimeout(() => { void poll(); }, 2_000);
    };

    timerId = window.setTimeout(() => { void poll(); }, 2_000);

    return () => {
      cancelled = true;
      if (timerId !== undefined) window.clearTimeout(timerId);
    };
  }, [pendingConfirmation, refetch]);

  const returnFocusToConfirmationButton = () => {
    window.requestAnimationFrame(() => confirmationButtonRef.current?.focus());
  };

  const closeConfirmationForm = () => {
    setConfirmationFormOpen(false);
    setExplanation('');
    setIdempotencyKey(null);
    setValidationMessage(null);
    setRetryableError(false);
    setConfirmationError(null);
    returnFocusToConfirmationButton();
  };

  const submitConfirmation = async () => {
    if (!explanation.trim()) {
      setValidationMessage('Escreva a explicação do que foi apurado.');
      return;
    }

    const attemptKey = idempotencyKey ?? crypto.randomUUID();
    setIdempotencyKey(attemptKey);
    setValidationMessage(null);
    setRetryableError(false);
    setConfirmationError(null);
    setConfirmationStatus(null);
    setConfirmationRecorded(false);

    try {
      const accepted = await confirmationMutation.mutateAsync({ recordId, explanation, idempotencyKey: attemptKey });
      setConfirmationFormOpen(false);
      setExplanation('');
      setIdempotencyKey(null);
      setPendingConfirmation({ confirmationId: accepted.confirmationId, startedAt: Date.now() });
      setConfirmationTimedOut(false);
      returnFocusToConfirmationButton();
    } catch (error: unknown) {
      const status = axios.isAxiosError(error) ? error.response?.status : undefined;
      setConfirmationStatus(status ?? null);
      if (status === 422) {
        const problem = axios.isAxiosError(error) ? error.response?.data as { code?: unknown } | undefined : undefined;
        setConfirmationError(problem?.code === 'IDEMPOTENCY_CONFLICT'
          ? 'Esta chave já foi usada com outros dados. Edite a explicação para iniciar uma nova tentativa.'
          : 'Escreva a explicação do que foi apurado.');
        setRetryableError(false);
        return;
      }

      setRetryableError(true);
    }
  };

  const onConfirmationSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void submitConfirmation();
  };

  const refreshConfirmation = async () => {
    const result = await query.refetch();
    if (result.data?.complements.some(complement => complement.confirmationId === pendingConfirmation?.confirmationId)) {
      setPendingConfirmation(null);
      setConfirmationTimedOut(false);
      setConfirmationRecorded(true);
    }
  };

  if (confirmationStatus === 401) return <Navigate replace to={paths.staffLogin.getHref()} />;
  if (confirmationStatus === 403) return <AuditTrailForbidden />;
  if (query.isPending) {
    return <main className="page-shell audit-trail-page audit-detail-page">
      {backLink}
      <section aria-busy="true" aria-label="Carregando registro de auditoria" className="audit-detail-skeleton" role="status">
        <div className="audit-detail-skeleton-lines">{Array.from({ length: 8 }, (_, index) => <span key={index} />)}</div>
        <span className="audit-detail-skeleton-complement" />
      </section>
    </main>;
  }

  if (query.isError && !query.data) {
    const status = axios.isAxiosError(query.error) ? query.error.response?.status : undefined;
    if (status === 401) return <Navigate replace to={paths.staffLogin.getHref()} />;
    if (status === 403) return <AuditTrailForbidden />;
    if (status === 404) {
      return <main className="page-shell audit-trail-page audit-detail-page">
        {backLink}
        <section className="empty-state audit-detail-empty-state">
          <h1>Registro não encontrado</h1>
          <p>Ele pode não existir ou não estar disponível para você.</p>
          <Link className="outline-button" state={returnState} to={paths.auditTrail.getHref()}>Voltar para a trilha</Link>
        </section>
      </main>;
    }

    return <main className="page-shell audit-trail-page audit-detail-page">
      {backLink}
      <section className="empty-state audit-error-state" role="alert">
        <TriangleAlert aria-hidden="true" size={24} />
        <h1>Não conseguimos abrir o registro agora.</h1>
        <button className="outline-button" onClick={() => void query.refetch()} type="button">Tentar de novo</button>
      </section>
    </main>;
  }

  return <main className="page-shell audit-trail-page audit-detail-page">
    {backLink}
    <div className="page-heading-row">
      <div>
        <p className="eyebrow">Registro de auditoria</p>
        <h1>{query.data.type ? typeLabels[query.data.type] ?? query.data.type : 'Tipo não disponível'}</h1>
      </div>
      <span className={`audit-compliance-badge ${query.data.compliant ? 'is-compliant' : 'is-non-compliant'}`}>
        {query.data.compliant ? <CircleCheck aria-hidden="true" size={15} /> : <TriangleAlert aria-hidden="true" size={15} />}
        {query.data.compliant ? 'Conforme' : 'Não conforme'}
      </span>
    </div>

    {!query.data.compliant && query.data.nonComplianceReasons.length > 0 ? <section className="audit-detail-alert" role="alert">
      <TriangleAlert aria-hidden="true" size={18} />
      <strong>A origem enviou este ato incompleto</strong>
      <ul>{query.data.nonComplianceReasons.map((reason) => <li key={reason}>{nonComplianceLabels[reason] ?? reason}</li>)}</ul>
      <p>Complementos não tornam o registro conforme.</p>
    </section> : null}

    <section aria-labelledby="audit-original-heading" className="audit-detail-card">
      <div className="audit-detail-card-heading">
        <p>Original · não editável</p>
        <h2 id="audit-original-heading">Recebido da origem</h2>
      </div>
      <dl className="audit-detail-fields">
        <DetailField label="Momento do ato">
          {query.data.practicedAt
            ? <time dateTime={query.data.practicedAt}>{formatMoment(query.data.practicedAt)}</time>
            : <MissingValue />}
        </DetailField>
        <DetailField label="Recebido pela Auditoria">
          <time dateTime={query.data.receivedAt}>{formatMoment(query.data.receivedAt)}</time>
        </DetailField>
        <DetailField label="Origem">
          {query.data.origin === 'identidade'
            ? 'Identidade e Acesso'
            : query.data.origin === 'matricula' ? 'Matrícula e Direito de Acesso'
              : query.data.origin === 'conteudo' ? 'Conteúdo e Currículo' : query.data.origin === 'catalogo' ? 'Catálogo e Ofertas' : <code className="audit-mono">{query.data.origin}</code>}
        </DetailField>
        <DetailField label="Tipo">
          {query.data.type
            ? typeLabels[query.data.type]
              ? typeLabels[query.data.type]
              : <span className="audit-unknown-type"><code className="audit-mono">{query.data.type}</code><small>Tipo desconhecido</small></span>
            : <MissingValue />}
        </DetailField>
        <DetailField label="Autor">
          <IdentityReference recordId={recordId} reference={query.data.author} filterKind="author" />
        </DetailField>
        <DetailField label={query.data.type === 'cortesia-concedida' ? 'Aluno' : 'Alvo'}>
          <IdentityReference recordId={recordId} reference={query.data.target} filterKind="target" />
        </DetailField>
        {getOfferAuditAttributeEntries(query.data.attributes)
          .filter(([key]) => query.data.type !== 'cortesia-concedida' || (key !== 'cursoTitulo' && key !== 'concessao'))
          .map(([key, value]) => <DetailField key={key} label={attributeLabel(key)}>
          {key === 'papel' ? <RoleBadge role={value} />
            : query.data.type === 'cortesia-concedida' && key === 'curso' && query.data.attributes.cursoTitulo
              ? <span>{query.data.attributes.cursoTitulo}</span>
              : <code className="audit-mono">{formatOfferAuditAttribute(key, value)}</code>}
        </DetailField>)}
        <DetailField label="Motivo">
          {query.data.reason !== null
            ? <span className="audit-detail-reason">{query.data.reason}</span>
            : (query.data.type === 'convite-interno-aceito' || query.data.type === 'versao-publicada' || ['oferta-publicada', 'oferta-alterada', 'oferta-despublicada'].includes(query.data.type ?? ''))
              ? <span className="audit-not-applicable">Não se aplica a este tipo</span>
              : <MissingValue />}
        </DetailField>
      </dl>
    </section>

    <section aria-labelledby="audit-complements-heading" className="audit-complements-summary">
      <h2 id="audit-complements-heading">
        Complementos{query.data.complements.length > 0 ? <span> {query.data.complements.length}</span> : null}
      </h2>
      {query.data.complements.length === 0 && !pendingConfirmation
        ? <p>Nenhum complemento. Complementos acrescentam o que foi apurado depois, sem mudar o registro acima.</p>
        : null}
      {confirmationRecorded ? <p className="audit-complement-recorded-toast" role="status">Complemento registrado</p> : null}
      <ol aria-live="polite" className="audit-complement-timeline">
        {query.data.complements.map((complement) => <li className="audit-complement-entry" key={complement.id}>
          <span aria-hidden="true" className="audit-complement-entry-rail" />
          <div className="audit-complement-entry-body">
            <p className="audit-complement-entry-marker">ACRESCENTADO DEPOIS</p>
            <div className="audit-complement-entry-header">
              {complement.author?.label
                ? <span>{complement.author.label}</span>
                : <span>Nome não disponível</span>}
              <time dateTime={complement.createdAt}>{formatMoment(complement.createdAt)}</time>
            </div>
            <p className="audit-complement-explanation">{complement.explanation}</p>
          </div>
        </li>)}
        {pendingConfirmation ? <li className="audit-complement-pending-entry">
          <LoaderCircle aria-hidden="true" className="audit-spin" size={16} />Aguardando registro…
        </li> : null}
      </ol>
      {confirmationFormOpen ? <form aria-labelledby="audit-complement-confirmation-heading" className="audit-complement-form" noValidate onSubmit={onConfirmationSubmit}>
        <h3 id="audit-complement-confirmation-heading">Acrescentar complemento</h3>
        <ReasonField
          error={validationMessage}
          value={explanation}
          onChange={(value) => {
            if (idempotencyKey && value !== explanation) {
              setIdempotencyKey(null);
              setRetryableError(false);
            }
            setExplanation(value);
            setValidationMessage(null);
            setConfirmationError(null);
          }}
        />
        <div className="audit-complement-irreversible" role="note">
          <Info aria-hidden="true" size={16} />
          <span>Complementos não podem ser editados nem excluídos. O registro original não muda.</span>
        </div>
        {confirmationError ? <p role="alert" className="audit-complement-error">{confirmationError}</p> : null}
        {retryableError ? <div className="audit-complement-retry-alert" role="alert">
          <p>Não conseguimos confirmar agora. Seu texto foi mantido.</p>
          <button className="outline-button" disabled={confirmationMutation.isPending} onClick={() => void submitConfirmation()} type="button">Tentar de novo</button>
        </div> : null}
        <div className="audit-complement-form-actions">
          <button className="outline-button" disabled={confirmationMutation.isPending} onClick={closeConfirmationForm} type="button">Cancelar</button>
          <button className="primary-button" disabled={confirmationMutation.isPending} type="submit">
            {confirmationMutation.isPending ? 'Enviando…' : 'Confirmar'}
          </button>
        </div>
      </form> : null}

      {!confirmationFormOpen ? <button
        className="outline-button audit-complement-add-button"
        disabled={Boolean(pendingConfirmation)}
        onClick={() => setConfirmationFormOpen(true)}
        ref={confirmationButtonRef}
        type="button"
      ><Plus aria-hidden="true" size={16} />Acrescentar complemento</button> : null}

      {pendingConfirmation && confirmationTimedOut ? <div className="audit-confirmation-pending-alert" role="alert">
        <p>A confirmação foi aceita e ainda está sendo registrada.</p>
        <button className="outline-button" onClick={() => void refreshConfirmation()} type="button">Atualizar</button>
      </div> : null}
    </section>
  </main>;
};

type ReasonFieldProps = {
  error: string | null;
  value: string;
  onChange: (value: string) => void;
};

const ReasonField = ({ error, value, onChange }: ReasonFieldProps) => <div className="audit-complement-reason-field">
  <label htmlFor="audit-complement-explanation">Explicação do que foi apurado</label>
  <textarea
    aria-describedby={error ? 'audit-complement-explanation-help audit-complement-explanation-error audit-complement-explanation-count' : 'audit-complement-explanation-help audit-complement-explanation-count'}
    aria-invalid={Boolean(error)}
    id="audit-complement-explanation"
    maxLength={1000}
    onChange={(event) => onChange(event.currentTarget.value)}
    rows={5}
    value={value}
  />
  <p className="audit-complement-help" id="audit-complement-explanation-help">Não cite dados pessoais de outras pessoas.</p>
  <p className="audit-complement-character-count" id="audit-complement-explanation-count">{value.length}/1000</p>
  {error ? <p className="audit-complement-error" id="audit-complement-explanation-error" role="alert">{error}</p> : null}
</div>;

type DetailFieldProps = {
  label: string;
  children: ReactNode;
};

const DetailField = ({ label, children }: DetailFieldProps) => <div className="audit-detail-field">
  <dt>{label}</dt>
  <dd>{children}</dd>
</div>;

const MissingValue = () => <span className="audit-missing"><TriangleAlert aria-hidden="true" size={14} />— ausente</span>;

type IdentityReferenceProps = {
  recordId: string;
  reference: AuditRecordDetail['author'];
  filterKind: AuditTrailPersonFilter['kind'];
};

const IdentityReference = ({ recordId, reference, filterKind }: IdentityReferenceProps) => {
  const navigate = useNavigate();
  if (!reference) return <MissingValue />;

  const label = reference.label ?? (reference.type === 'curso' ? 'Título não disponível' : reference.type === 'oferta' ? 'Rótulo não disponível' : 'Nome não disponível');
  const referenceId = reference.id;

  return <div className="audit-detail-reference">
    <span>{label}</span>
    {reference.type === 'curso' || reference.type === 'oferta' ? <span className="audit-reference-kind">{referenceTypeLabel(reference.type)}</span> : null}
    {!reference.label && referenceId ? <span className="audit-reference-missing">
      {referenceTypeLabel(reference.type)} · {shortReference(referenceId)}
    </span> : null}
    {referenceId ? <div className="audit-detail-reference-actions">
      {!reference.label ? <button
        aria-label="Copiar referência"
        className="audit-copy-reference"
        onClick={() => { void navigator.clipboard?.writeText(referenceId); }}
        type="button"
      ><Copy aria-hidden="true" size={14} /></button> : null}
      <button
        aria-label={reference.type === 'curso' ? 'Ver atos deste curso (alvo)' : reference.type === 'oferta' ? 'Ver atos desta oferta (alvo)' : `Ver atos desta pessoa (${filterKind === 'author' ? 'autor' : 'alvo'})`}
        className="audit-person-filter-link"
        onClick={() => navigate(paths.auditTrail.getHref(), {
          state: {
            personFilter: { kind: filterKind, id: referenceId, label } satisfies AuditTrailPersonFilter,
            returnToAuditDetail: recordId,
          },
        })}
        type="button"
      >{reference.type === 'curso' ? 'Ver atos deste curso' : reference.type === 'oferta' ? 'Ver atos desta oferta' : 'Ver atos desta pessoa'}</button>
    </div> : null}
  </div>;
};

const RoleBadge = ({ role }: { role: string }) => <span className="role-badge">{roleLabels[role] ?? role}</span>;

const roleLabels: Record<string, string> = {
  administrador: 'Administrador',
  financeiro: 'Financeiro',
  professor: 'Professor',
  suporte: 'Suporte',
};

const attributeLabels: Record<string, string> = {
  papel: 'Papel',
  versao: 'Versão',
  curso: 'Curso',
  precoAnterior: 'Preço anterior',
  precoNovo: 'Preço novo',
  vigenciaAnterior: 'Vigência anterior',
  vigenciaNova: 'Vigência nova',
  vigencia: 'Vigência',
};
const attributeLabel = (key: string) => attributeLabels[key] ?? key;

const referenceTypeLabel = (type: string | null) => {
  if (type === 'curso') return 'Curso';
  if (type === 'oferta') return 'Oferta';
  if (type === 'conta-interna') return 'Conta interna';
  if (type === 'conta-aluno') return 'Conta de aluno';
  if (type === 'convite-interno') return 'Convite interno';
  return type ?? 'Referência';
};

const shortReference = (id: string) => `${id.slice(0, 4)}…${id.slice(-4)}`;

const formatMoment = (value: string) => new Date(value).toLocaleString('pt-BR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
});
