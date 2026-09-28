import { useEffect, useState } from 'react';
import axios from 'axios';
import { CircleCheck, RefreshCw, TriangleAlert } from 'lucide-react';
import { Link, Navigate, useLocation, useNavigate, useNavigationType } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';

import { paths } from '@/config/paths';
import { useAuditRecordSearch, type AuditRecordSearchInput, type AuditRecordSummary } from '@/features/audit-trail/api/search-audit-records';
import { parseAuditTrailNavigationState, type AuditTrailComplianceFilter, type AuditTrailDraftFilters, type AuditTrailListNavigation, type AuditTrailPersonFilter } from '@/features/audit-trail/types/audit-trail-navigation';
import { AuditTrailForbidden } from '@/features/audit-trail/components/audit-trail-forbidden';

const initialDraft: AuditTrailDraftFilters = { from: '', to: '', type: '' };
const pageSize = 20;
const typeOptions = [
  { value: 'papel-concedido', label: 'Papel concedido' },
  { value: 'papel-revogado', label: 'Papel revogado' },
  { value: 'convite-interno-emitido', label: 'Convite emitido' },
  { value: 'convite-interno-aceito', label: 'Convite aceito' },
];

export const AuditTrailScreen = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const navigationType = useNavigationType();
  const locationState = parseAuditTrailNavigationState(location.state);
  const navigationState = navigationType === 'POP' ? null : locationState;
  const restoredList = navigationState?.restoreAuditList;
  const incomingPersonFilter = navigationState?.personFilter ?? null;
  const initialPersonFilter = restoredList?.personFilter ?? incomingPersonFilter;
  const [personFilter, setPersonFilter] = useState<AuditTrailPersonFilter | null>(initialPersonFilter);
  const [draft, setDraft] = useState<AuditTrailDraftFilters>(restoredList?.draft ?? initialDraft);
  const [compliance, setCompliance] = useState<AuditTrailComplianceFilter>(restoredList?.compliance ?? 'all');
  const [search, setSearch] = useState<AuditRecordSearchInput>(
    restoredList?.search ?? createPersonSearch(initialPersonFilter),
  );
  const [periodError, setPeriodError] = useState<string | null>(null);
  const [snapshotExpired, setSnapshotExpired] = useState(false);
  // A new generation forces a fresh first page (and a new snapshot) even when the filters are unchanged.
  const [generation, setGeneration] = useState(restoredList?.generation ?? 0);
  const queryClient = useQueryClient();
  const query = useAuditRecordSearch(search, generation);
  const restartNotice = navigationType === 'POP'
    && Boolean(locationState?.personFilter || locationState?.restoreAuditList?.personFilter);
  const returnToDetailId = navigationType === 'POP' ? locationState?.returnToAuditDetail : undefined;

  useEffect(() => {
    const snapshot = search.snapshot;
    if (!snapshot) return;

    return queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== 'updated') return;
      const request = event.query.queryKey[2] as AuditRecordSearchInput | undefined;
      if (request?.snapshot !== snapshot || !isExpiredSnapshotError(event.query.state.error)) return;

      setSnapshotExpired(true);
      setGeneration((current) => current + 1);
      setSearch((current) => current.snapshot === snapshot
        ? { ...current, _page: 1, snapshot: undefined }
        : current);
    });
  }, [queryClient, search.snapshot]);

  const applySearch = () => {
    const fromDate = draft.from ? new Date(draft.from) : null;
    const toDate = draft.to ? new Date(draft.to) : null;
    if (fromDate && toDate && fromDate.getTime() > toDate.getTime()) {
      setPeriodError('A data final vem antes da inicial.');
      return;
    }

    setPeriodError(null);
    setSnapshotExpired(false);
    setSearch({
      _page: 1,
      _size: pageSize,
      ...(fromDate ? { from: fromDate.toISOString() } : {}),
      ...(toDate ? { to: toDate.toISOString() } : {}),
      ...(draft.type ? { type: draft.type } : {}),
      ...(compliance === 'all' ? {} : { compliant: compliance === 'compliant' }),
      ...(personFilter?.kind === 'author' ? { authorId: personFilter.id } : {}),
      ...(personFilter?.kind === 'target' ? { targetId: personFilter.id } : {}),
    });
  };

  const changeCompliance = (value: AuditTrailComplianceFilter) => {
    setCompliance(value);
    setSnapshotExpired(false);
    setSearch((current) => ({
      _page: 1,
      _size: pageSize,
      ...(current.from ? { from: current.from } : {}),
      ...(current.to ? { to: current.to } : {}),
      ...(current.type ? { type: current.type } : {}),
      ...(value === 'all' ? {} : { compliant: value === 'compliant' }),
    }));
  };

  const clearFilters = () => {
    setDraft(initialDraft);
    setCompliance('all');
    setPersonFilter(null);
    setPeriodError(null);
    setSnapshotExpired(false);
    setSearch({ _page: 1, _size: pageSize });
  };

  const removePersonFilter = () => {
    setPersonFilter(null);
    setSearch((current) => {
      const updated = { ...current, _page: 1, _size: pageSize, snapshot: undefined };
      delete updated.authorId;
      delete updated.targetId;
      return updated;
    });
  };

  const updateSearch = () => {
    setSnapshotExpired(false);
    setGeneration((current) => current + 1);
    setSearch((current) => ({ ...current, _page: 1, snapshot: undefined }));
  };

  const page = query.data;
  const hasFilters = Boolean(search.from || search.to || search.type || search.authorId || search.targetId || search.compliant !== undefined);
  const listNavigation: AuditTrailListNavigation = { search, draft, compliance, generation, personFilter };
  const total = page?.pagination.total ?? 0;
  const fixedAt = query.dataUpdatedAt
    ? new Date(query.dataUpdatedAt).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
    : null;

  if (query.isError && axios.isAxiosError(query.error) && query.error.response?.status === 401) {
    return <Navigate replace to={paths.staffLogin.getHref()} />;
  }

  if (query.isError && axios.isAxiosError(query.error) && query.error.response?.status === 403) {
    return <AuditTrailForbidden />;
  }

  return <main className="page-shell audit-trail-page">
    <div className="page-heading-row">
      <div>
        <p className="eyebrow">Auditoria</p>
        <h1>Trilha de atos administrativos</h1>
        <p className="page-subtitle">Quem fez o quê, com quem e quando. Nada aqui pode ser alterado.</p>
      </div>
    </div>

    <form className="audit-filter-card" onSubmit={(event) => { event.preventDefault(); applySearch(); }}>
      <div className="audit-filter-grid">
        <div className="audit-field">
          <label htmlFor="audit-from">De</label>
          <input
            id="audit-from"
            type="datetime-local"
            value={draft.from}
            onChange={(event) => { setPeriodError(null); setDraft((current) => ({ ...current, from: event.target.value })); }}
          />
        </div>
        <div className="audit-field">
          <label htmlFor="audit-to">Até</label>
          <input
            id="audit-to"
            type="datetime-local"
            value={draft.to}
            onChange={(event) => { setPeriodError(null); setDraft((current) => ({ ...current, to: event.target.value })); }}
          />
        </div>
        <div className="audit-field">
          <label htmlFor="audit-type">Tipo</label>
          <select
            id="audit-type"
            value={draft.type}
            onChange={(event) => { setPeriodError(null); setDraft((current) => ({ ...current, type: event.target.value })); }}
          >
            <option value="">Todos</option>
            {typeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </div>
      </div>
      {periodError ? <p className="audit-period-error" role="alert">{periodError}</p> : null}
      <div className="audit-filter-actions">
        <button className="secondary-button" disabled={query.isFetching} onClick={clearFilters} type="button">Limpar filtros</button>
        <button className="primary-button" disabled={query.isFetching || Boolean(periodError)} type="submit">Buscar</button>
      </div>
    </form>

    {restartNotice ? <p className="audit-restarted-alert" role="status">
      A busca foi reiniciada sem o filtro de pessoa.
      {returnToDetailId ? <Link to={paths.auditRecordDetail.getHref(returnToDetailId)}>Voltar ao registro</Link> : null}
    </p> : null}

    {personFilter ? <div className="audit-filter-chip">
      <span>{personFilter.kind === 'author' ? 'Autor' : 'Alvo'}: {personFilter.label}</span>
      <button
        aria-label={`Remover filtro de ${personFilter.kind === 'author' ? 'autor' : 'alvo'}`}
        onClick={removePersonFilter}
        type="button"
      >×</button>
    </div> : null}

    <div aria-label="Filtrar por conformidade" className="audit-tabs" role="tablist">
      {([
        ['all', 'Todos'],
        ['compliant', 'Conformes'],
        ['non-compliant', 'Não conformes'],
      ] as const).map(([value, label]) => <button
        aria-selected={compliance === value}
        className={compliance === value ? 'is-selected' : ''}
        key={value}
        onClick={() => changeCompliance(value)}
        role="tab"
        type="button"
      >{label}</button>)}
    </div>

    {snapshotExpired ? <p className="audit-expired-alert" role="status">A busca expirou e foi refeita. Você voltou à primeira página.</p> : null}
    {page ? <div aria-live="polite" className="audit-results-summary">
      <span>{total} {total === 1 ? 'registro' : 'registros'}{fixedAt ? ` · resultado fixado às ${fixedAt}` : ''}</span>
      <button aria-label="Atualizar resultados" className="audit-refresh-button" disabled={query.isFetching} onClick={updateSearch} type="button">
        <RefreshCw aria-hidden="true" size={16} /> Atualizar
      </button>
    </div> : null}

    {query.isPending ? <section aria-busy="true" aria-label="Carregando trilha de auditoria" className="audit-loading">
      <p aria-live="polite">Carregando registros…</p>
      <div aria-hidden="true" className="audit-skeleton-rows">{Array.from({ length: 5 }, (_, index) => <div className="audit-skeleton-row" key={index} />)}</div>
    </section> : null}

    {query.isError && !isExpiredSnapshotError(query.error) ? <section className="empty-state audit-error-state" role="alert">
      <TriangleAlert aria-hidden="true" size={24} />
      <h2>Não conseguimos carregar a trilha agora.</h2>
      <button className="outline-button" onClick={() => void query.refetch()} type="button">Tentar de novo</button>
    </section> : null}

    {page && total === 0 ? <section className="empty-state audit-empty-state">
      <h2>{hasFilters ? 'Nenhum registro com esses filtros.' : 'Nenhum ato registrado ainda'}</h2>
      <p>{hasFilters ? 'Altere o período, o tipo ou a situação.' : 'Convites e mudanças de papel feitos em Acessos aparecem aqui.'}</p>
      {hasFilters
        ? <button className="outline-button" onClick={clearFilters} type="button">Limpar filtros</button>
        : <Link className="outline-button" to={paths.staffAccess.getHref()}>Abrir acessos</Link>}
    </section> : null}

    {page && page.data.length > 0 ? <>
      <div className="audit-table-wrap">
        <table className="audit-table">
          <caption>Registros da trilha de auditoria</caption>
          <thead><tr>
            <th scope="col">Momento do ato</th>
            <th scope="col">Tipo</th>
            <th scope="col">Autor</th>
            <th scope="col">Alvo</th>
            <th scope="col">Situação</th>
          </tr></thead>
          <tbody>{page.data.map((record) => <AuditRecordRow
            key={record.id}
            onOpen={() => navigate(paths.auditRecordDetail.getHref(record.id), {
              state: { returnToAuditList: listNavigation },
            })}
            record={record}
          />)}</tbody>
        </table>
      </div>
      <nav aria-label="Paginação da trilha" className="audit-pagination">
        <button
          aria-label="Página anterior"
          disabled={query.isFetching || page.pagination.page <= 1}
          onClick={() => setSearch((current) => ({ ...current, _page: page.pagination.page - 1, snapshot: page.pagination.snapshot }))}
          type="button"
        >Anterior</button>
        <span aria-live="polite">Página {page.pagination.page} de {Math.max(page.pagination.totalPages, 1)}</span>
        <button
          aria-label="Próxima página"
          disabled={query.isFetching || page.pagination.page >= page.pagination.totalPages}
          onClick={() => setSearch((current) => ({ ...current, _page: page.pagination.page + 1, snapshot: page.pagination.snapshot }))}
          type="button"
        >Próxima</button>
      </nav>
    </> : null}
  </main>;
};

const AuditRecordRow = ({ record, onOpen }: { record: AuditRecordSummary; onOpen: () => void }) => <tr
  aria-label={`Abrir registro ${record.type ?? 'sem tipo'}`}
  onClick={onOpen}
  onKeyDown={(event) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onOpen();
    }
  }}
  role="link"
  tabIndex={0}
>
  <td>{record.practicedAt
    ? <time dateTime={record.practicedAt}>{new Date(record.practicedAt).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}</time>
    : <span className="audit-missing">— ausente</span>}</td>
  <td>{record.type ? (typeOptions.find((option) => option.value === record.type)?.label ?? record.type) : 'Tipo não disponível'}</td>
  <td><IdentityReference reference={record.author} /></td>
  <td><IdentityReference reference={record.target} /></td>
  <td><div className="audit-status-cell">
    <span className={`audit-compliance-badge ${record.compliant ? 'is-compliant' : 'is-non-compliant'}`}>
      {record.compliant ? <CircleCheck aria-hidden="true" size={15} /> : <TriangleAlert aria-hidden="true" size={15} />}
      {record.compliant ? 'Conforme' : 'Não conforme'}
    </span>
    {record.hasComplements ? <span className="audit-complement-badge">Complementado</span> : null}
  </div></td>
</tr>;

const IdentityReference = ({ reference }: { reference: AuditRecordSummary['author'] }) => {
  if (!reference) return <span className="audit-missing">— ausente</span>;
  if (reference.label) return <span>{reference.label}</span>;
  return <span className="audit-reference-missing">Nome não disponível · {shortReference(reference.id)}</span>;
};

const shortReference = (id: string) => `${id.slice(0, 4)}…${id.slice(-4)}`;

const createPersonSearch = (personFilter: AuditTrailPersonFilter | null): AuditRecordSearchInput => {
  const base = { _page: 1, _size: pageSize };
  if (!personFilter) return base;
  return personFilter.kind === 'author'
    ? { ...base, authorId: personFilter.id }
    : { ...base, targetId: personFilter.id };
};

const isExpiredSnapshotError = (error: unknown) => axios.isAxiosError(error)
  && error.response?.status === 422
  && (error.response.data as { code?: string } | undefined)?.code === 'AUDIT_FILTER_INVALID';
