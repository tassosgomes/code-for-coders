import axios from 'axios';
import { ArrowLeft, Copy, CircleCheck, TriangleAlert } from 'lucide-react';
import { Link, Navigate, useLocation, useNavigate, useNavigationType } from 'react-router';
import type { ReactNode } from 'react';

import { paths } from '@/config/paths';
import { useAuditRecord, type AuditRecordDetail } from '@/features/audit-trail/api/get-audit-record';
import { AuditTrailForbidden } from '@/features/audit-trail/components/audit-trail-forbidden';
import { parseAuditTrailNavigationState, type AuditTrailPersonFilter } from '@/features/audit-trail/types/audit-trail-navigation';

const typeLabels: Record<string, string> = {
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
  const location = useLocation();
  const navigationType = useNavigationType();
  const locationState = navigationType === 'POP' ? null : parseAuditTrailNavigationState(location.state);
  const returnState = locationState?.returnToAuditList
    ? { restoreAuditList: locationState.returnToAuditList }
    : undefined;
  const backLink = <Link className="audit-back-link" state={returnState} to={paths.auditTrail.getHref()}>
    <ArrowLeft aria-hidden="true" size={16} /> Trilha de auditoria
  </Link>;

  if (query.isPending) {
    return <main className="page-shell audit-trail-page audit-detail-page">
      {backLink}
      <section aria-busy="true" aria-label="Carregando registro de auditoria" className="audit-detail-skeleton" role="status">
        <div className="audit-detail-skeleton-lines">{Array.from({ length: 8 }, (_, index) => <span key={index} />)}</div>
        <span className="audit-detail-skeleton-complement" />
      </section>
    </main>;
  }

  if (query.isError) {
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
            : <code className="audit-mono">{query.data.origin}</code>}
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
        <DetailField label="Alvo">
          <IdentityReference recordId={recordId} reference={query.data.target} filterKind="target" />
        </DetailField>
        {Object.entries(query.data.attributes).map(([key, value]) => <DetailField key={key} label={attributeLabel(key)}>
          {key === 'papel' ? <RoleBadge role={value} /> : <code className="audit-mono">{value}</code>}
        </DetailField>)}
        <DetailField label="Motivo">
          {query.data.reason !== null
            ? <span className="audit-detail-reason">{query.data.reason}</span>
            : query.data.type === 'convite-interno-aceito'
              ? <span className="audit-not-applicable">Não se aplica a este tipo</span>
              : <MissingValue />}
        </DetailField>
      </dl>
    </section>

    {query.data.complements.length > 0 ? <section aria-labelledby="audit-complements-heading" className="audit-complements-summary">
      <h2 id="audit-complements-heading">Complementos <span>{query.data.complements.length}</span></h2>
      <p>Há complementos registrados para este ato.</p>
    </section> : null}
  </main>;
};

type DetailFieldProps = {
  label: string;
  children: ReactNode;
};

const DetailField = ({ label, children }: DetailFieldProps) => <div className="audit-detail-field">
  <dt>{label}</dt>
  <dd>{children}</dd>
</div>;

const MissingValue = () => <span className="audit-missing">— ausente</span>;

type IdentityReferenceProps = {
  recordId: string;
  reference: AuditRecordDetail['author'];
  filterKind: AuditTrailPersonFilter['kind'];
};

const IdentityReference = ({ recordId, reference, filterKind }: IdentityReferenceProps) => {
  const navigate = useNavigate();
  if (!reference) return <MissingValue />;

  const label = reference.label ?? 'Nome não disponível';
  const referenceId = reference.id;

  return <div className="audit-detail-reference">
    <span>{label}</span>
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
        aria-label={`Ver atos desta pessoa (${filterKind === 'author' ? 'autor' : 'alvo'})`}
        className="audit-person-filter-link"
        onClick={() => navigate(paths.auditTrail.getHref(), {
          state: {
            personFilter: { kind: filterKind, id: referenceId, label } satisfies AuditTrailPersonFilter,
            returnToAuditDetail: recordId,
          },
        })}
        type="button"
      >Ver atos desta pessoa</button>
    </div> : null}
  </div>;
};

const RoleBadge = ({ role }: { role: string }) => <span className="role-badge">{roleLabels[role] ?? role}</span>;

const roleLabels: Record<string, string> = {
  administrador: 'Administrador',
  professor: 'Professor',
  suporte: 'Suporte',
};

const attributeLabel = (key: string) => key === 'papel' ? 'Papel' : key;

const referenceTypeLabel = (type: string | null) => {
  if (type === 'conta-interna') return 'Conta interna';
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
