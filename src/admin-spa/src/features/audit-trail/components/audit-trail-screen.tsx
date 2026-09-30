import { useEffect, useRef, useState } from 'react';
import axios from 'axios';
import { ChevronRight, CircleCheck, MessageSquarePlus, TriangleAlert, X } from 'lucide-react';
import { Link, Navigate, useLocation, useNavigate, useNavigationType } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';

import { paths } from '@/config/paths';
import { useAuditRecordSearch, type AuditRecordSearchInput, type AuditRecordSummary } from '@/features/audit-trail/api/search-audit-records';
import { parseAuditTrailNavigationState, type AuditTrailComplianceFilter, type AuditTrailDraftFilters, type AuditTrailListNavigation, type AuditTrailPersonFilter } from '@/features/audit-trail/types/audit-trail-navigation';
import { AuditTrailForbidden } from '@/features/audit-trail/components/audit-trail-forbidden';

const initialDraft: AuditTrailDraftFilters = { from: '', to: '', type: '' };
const pageSize = 20;
const typeOptions = [
  { value: 'versao-publicada', label: 'Versão publicada' },
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
  const [mobileFiltersOpen, setMobileFiltersOpen] = useState(false);
  const [search, setSearch] = useState<AuditRecordSearchInput>(
    restoredList?.search ?? createPersonSearch(initialPersonFilter),
  );
  const [periodError, setPeriodError] = useState<string | null>(null);
  const [snapshotExpired, setSnapshotExpired] = useState(false);
  // A new generation forces a fresh first page (and a new snapshot) even when the filters are unchanged.
  const [generation, setGeneration] = useState(restoredList?.generation ?? 0);
  const mobileFilterTriggerRef = useRef<HTMLButtonElement>(null);
  const mobileFilterDialogRef = useRef<HTMLElement>(null);
  const queryClient = useQueryClient();
  const query = useAuditRecordSearch(search, generation);
  const restartNotice = navigationType === 'POP'
    && Boolean(locationState?.personFilter || locationState?.restoreAuditList?.personFilter);
  const returnToDetailId = navigationType === 'POP' ? locationState?.returnToAuditDetail : undefined;

  useEffect(() => {
    if (!mobileFiltersOpen) return;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const dialog = mobileFilterDialogRef.current;
    const trigger = mobileFilterTriggerRef.current;
    dialog?.querySelector<HTMLButtonElement>('.audit-filter-sheet-close')?.focus();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        setMobileFiltersOpen(false);
        return;
      }

      if (event.key !== 'Tab' || !dialog) return;
      const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
      ));
      const first = focusable[0];
      const last = focusable.at(-1);
      if (!first || !last) return;

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = previousOverflow;
      trigger?.focus();
    };
  }, [mobileFiltersOpen]);

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
      return false;
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
    return true;
  };

  const updateDraft = (field: keyof AuditTrailDraftFilters, value: string) => {
    setPeriodError(null);
    setDraft((current) => ({ ...current, [field]: value }));
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
  const activeFilterCount = [search.from, search.to, search.type, search.authorId || search.targetId]
    .filter(Boolean).length;
  const listNavigation: AuditTrailListNavigation = { search, draft, compliance, generation, personFilter };
  const openRecord = (recordId: string) => navigate(paths.auditRecordDetail.getHref(recordId), {
    state: { returnToAuditList: listNavigation },
  });
  const total = page?.pagination.total ?? 0;
  const paginationItems = page ? getPaginationItems(page.pagination.page, page.pagination.totalPages) : [];
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
        <p className="page-subtitle">
          Quem fez o quê, com quem e quando. <span className="audit-subtitle-desktop">Nada aqui pode ser alterado.</span>
        </p>
      </div>
    </div>

    <div className="audit-mobile-toolbar">
      <button
        aria-controls="audit-mobile-filter-sheet"
        aria-expanded={mobileFiltersOpen}
        className="audit-mobile-filter-trigger outline-button"
        onClick={() => setMobileFiltersOpen(true)}
        ref={mobileFilterTriggerRef}
        type="button"
      >Filtros{activeFilterCount ? ` (${activeFilterCount})` : ''}</button>
      <span aria-live="polite">{total} {total === 1 ? 'registro' : 'registros'}</span>
    </div>

    <form className="audit-filter-card" onSubmit={(event) => { event.preventDefault(); applySearch(); }}>
      <div className="audit-filter-controls">
        <AuditFilterFields draft={draft} idPrefix="audit-desktop" onChange={updateDraft} />
        <div className="audit-filter-actions">
          <button aria-label="Limpar filtros" className="secondary-button" disabled={query.isFetching} onClick={clearFilters} type="button">Limpar</button>
          <button className="primary-button" disabled={query.isFetching || Boolean(periodError)} type="submit">Buscar</button>
        </div>
      </div>
      {periodError ? <p className="audit-period-error" role="alert">{periodError}</p> : null}
    </form>

    {restartNotice ? <p className="audit-restarted-alert" role="status">
      A busca foi reiniciada sem o filtro de pessoa.
      {returnToDetailId ? <Link to={paths.auditRecordDetail.getHref(returnToDetailId)}>Voltar ao registro</Link> : null}
    </p> : null}

    {personFilter ? <div className="audit-filter-chip audit-person-filter-chip">
      <span>{personFilter.kind === 'author' ? 'Autor' : 'Alvo'}: {personFilter.label}</span>
      <button
        aria-label={`Remover filtro de ${personFilter.kind === 'author' ? 'autor' : 'alvo'}`}
        onClick={removePersonFilter}
        type="button"
      ><X aria-hidden="true" size={14} /></button>
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
      <span>
        {total} {total === 1 ? 'registro' : 'registros'}{fixedAt ? ` · resultado fixado às ${fixedAt}` : ''}
        <span className="audit-results-note"> · atos novos entram ao buscar de novo</span>
      </span>
      <button aria-label="Atualizar resultados" className="audit-refresh-button" disabled={query.isFetching} onClick={updateSearch} type="button">
        Atualizar
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
      <div className="audit-table-wrap audit-desktop-table-wrap">
        <table className="audit-table">
          <caption>Registros da trilha de auditoria</caption>
          <thead><tr>
            <th scope="col">Momento do ato</th>
            <th scope="col">Tipo</th>
            <th scope="col">Autor</th>
            <th scope="col">Alvo</th>
            <th scope="col">Situação</th>
            <th scope="col"><span className="visually-hidden">Abrir registro</span></th>
          </tr></thead>
          <tbody>{page.data.map((record) => <AuditRecordRow
            key={record.id}
            onOpen={() => openRecord(record.id)}
            record={record}
          />)}</tbody>
        </table>
      </div>
      <ul aria-label="Registros da trilha de auditoria" className="audit-mobile-cards">
        {page.data.map((record) => <li key={record.id}>
          <AuditRecordCard onOpen={() => openRecord(record.id)} record={record} />
        </li>)}
      </ul>
      <nav aria-label="Paginação da trilha" className="audit-pagination">
        <span className="audit-pagination-summary">{total} {total === 1 ? 'registro' : 'registros'} · página {page.pagination.page} de {Math.max(page.pagination.totalPages, 1)}</span>
        <div className="audit-pagination-pages">
        <button
          aria-label="Página anterior"
          disabled={query.isFetching || page.pagination.page <= 1}
          onClick={() => setSearch((current) => ({ ...current, _page: page.pagination.page - 1, snapshot: page.pagination.snapshot }))}
          type="button"
        >‹</button>
        {paginationItems.map((item, index) => item === 'ellipsis'
          ? <span aria-hidden="true" className="audit-pagination-ellipsis" key={`ellipsis-${index}`}>…</span>
          : <button
            aria-current={page.pagination.page === item ? 'page' : undefined}
            aria-label={`Página ${item}`}
            disabled={query.isFetching}
            key={item}
            onClick={() => setSearch((current) => ({ ...current, _page: item, snapshot: page.pagination.snapshot }))}
            type="button"
          >{item}</button>)}
        <button
          aria-label="Próxima página"
          disabled={query.isFetching || page.pagination.page >= page.pagination.totalPages}
          onClick={() => setSearch((current) => ({ ...current, _page: page.pagination.page + 1, snapshot: page.pagination.snapshot }))}
          type="button"
        >›</button>
        </div>
        <span className="audit-page-size">(20 por página)</span>
      </nav>
    </> : null}

    {mobileFiltersOpen ? <>
      <div aria-hidden="true" className="audit-filter-sheet-backdrop" onClick={() => setMobileFiltersOpen(false)} />
      <section
        aria-labelledby="audit-mobile-filter-title"
        aria-modal="true"
        className="audit-filter-sheet"
        id="audit-mobile-filter-sheet"
        ref={mobileFilterDialogRef}
        role="dialog"
      >
        <div className="audit-filter-sheet-header">
          <h2 id="audit-mobile-filter-title">Filtros</h2>
          <button aria-label="Fechar filtros" className="audit-filter-sheet-close" onClick={() => setMobileFiltersOpen(false)} type="button">
            <X aria-hidden="true" size={20} />
          </button>
        </div>
        <form className="audit-filter-sheet-form" onSubmit={(event) => {
          event.preventDefault();
          if (applySearch()) setMobileFiltersOpen(false);
        }}>
          <AuditFilterFields draft={draft} idPrefix="audit-mobile" onChange={updateDraft} />
          {personFilter ? <div className="audit-filter-chip audit-sheet-person-chip">
            <span>{personFilter.kind === 'author' ? 'Autor' : 'Alvo'}: {personFilter.label}</span>
            <button
              aria-label={`Remover filtro de ${personFilter.kind === 'author' ? 'autor' : 'alvo'}`}
              onClick={removePersonFilter}
              type="button"
            ><X aria-hidden="true" size={14} /></button>
          </div> : null}
          {periodError ? <p className="audit-period-error" role="alert">{periodError}</p> : null}
          <div className="audit-filter-sheet-actions">
            <button className="outline-button" disabled={query.isFetching} onClick={() => {
              clearFilters();
              setMobileFiltersOpen(false);
            }} type="button">Limpar</button>
            <button className="primary-button" disabled={query.isFetching || Boolean(periodError)} type="submit">Aplicar</button>
          </div>
        </form>
      </section>
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
  <td><div className="audit-type-cell">
    <span>{getTypeLabel(record.type)}</span>
    {record.role ? <span className="role-badge">{formatRoleLabel(record.role)}</span> : null}
  </div></td>
  <td><IdentityReference reference={record.author} /></td>
  <td><IdentityReference reference={record.target} /></td>
  <td><div className="audit-status-cell">
    <span className={`audit-compliance-badge ${record.compliant ? 'is-compliant' : 'is-non-compliant'}`}>
      {record.compliant ? <CircleCheck aria-hidden="true" size={15} /> : <TriangleAlert aria-hidden="true" size={15} />}
      {record.compliant ? 'Conforme' : 'Não conforme'}
    </span>
    {record.hasComplements ? <span className="audit-complement-indicator"><MessageSquarePlus aria-hidden="true" size={13} />Complementado</span> : null}
  </div></td>
  <td className="audit-row-action"><ChevronRight aria-hidden="true" size={16} /></td>
</tr>;

const AuditRecordCard = ({ record, onOpen }: { record: AuditRecordSummary; onOpen: () => void }) => <button
  aria-label={`Abrir registro ${getTypeLabel(record.type)}`}
  className="audit-mobile-card"
  onClick={onOpen}
  type="button"
>
  <span className="audit-mobile-card-heading">
    <span>{getTypeLabel(record.type)}</span>
    <span className={`audit-compliance-badge ${record.compliant ? 'is-compliant' : 'is-non-compliant'}`}>
      {record.compliant ? <CircleCheck aria-hidden="true" size={14} /> : <TriangleAlert aria-hidden="true" size={14} />}
      {record.compliant ? 'Conforme' : 'Não conforme'}
    </span>
  </span>
  {record.role || record.hasComplements ? <span className="audit-mobile-card-metadata">
    {record.role ? <span className="role-badge">{formatRoleLabel(record.role)}</span> : null}
    {record.hasComplements ? <span className="audit-complement-indicator"><MessageSquarePlus aria-hidden="true" size={13} />Complementado</span> : null}
  </span> : null}
  <span className="audit-mobile-card-identities">
    <span>{formatIdentityReference(record.author)}</span>
    <span aria-hidden="true">→</span>
    <span>{formatIdentityReference(record.target)}</span>
  </span>
  <span className="audit-mobile-card-footer">
    <span>{record.practicedAt
      ? <time dateTime={record.practicedAt}>{new Date(record.practicedAt).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })}</time>
      : <span className="audit-missing">— ausente</span>}</span>
    <ChevronRight aria-hidden="true" size={16} />
  </span>
</button>;

type AuditFilterFieldsProps = {
  draft: AuditTrailDraftFilters;
  idPrefix: string;
  onChange: (field: keyof AuditTrailDraftFilters, value: string) => void;
};

const AuditFilterFields = ({ draft, idPrefix, onChange }: AuditFilterFieldsProps) => {
  const fromId = `${idPrefix}-from`;
  const toId = `${idPrefix}-to`;
  const typeId = `${idPrefix}-type`;

  return <div className="audit-filter-grid">
    <div className="audit-field">
      <label htmlFor={fromId}>De</label>
      <input id={fromId} type="datetime-local" value={draft.from} onChange={(event) => onChange('from', event.currentTarget.value)} />
    </div>
    <div className="audit-field">
      <label htmlFor={toId}>Até</label>
      <input id={toId} type="datetime-local" value={draft.to} onChange={(event) => onChange('to', event.currentTarget.value)} />
    </div>
    <div className="audit-field">
      <label htmlFor={typeId}>Tipo</label>
      <select id={typeId} value={draft.type} onChange={(event) => onChange('type', event.currentTarget.value)}>
        <option value="">Todos</option>
        {typeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </div>
  </div>;
};

const getTypeLabel = (type: string | null) => type
  ? typeOptions.find((option) => option.value === type)?.label ?? type
  : 'Tipo não disponível';

const formatIdentityReference = (reference: AuditRecordSummary['author']) => {
  if (!reference) return '— ausente';
  if (reference.label) return reference.type === 'curso' ? `Curso · ${reference.label}` : reference.label;
  return `${reference.type === 'curso' ? 'Curso · título não disponível' : 'Nome não disponível'} · ${shortReference(reference.id)}`;
};

const formatRoleLabel = (role: string) => roleLabels[role] ?? role;

const roleLabels: Record<string, string> = {
  administrador: 'Administrador',
  financeiro: 'Financeiro',
  professor: 'Professor',
  suporte: 'Suporte',
};

const getPaginationItems = (currentPage: number, totalPages: number): Array<number | 'ellipsis'> => {
  if (totalPages <= 4) return Array.from({ length: totalPages }, (_, index) => index + 1);
  if (currentPage <= 3) return [1, 2, 3, 'ellipsis', totalPages];
  if (currentPage >= totalPages - 2) return [1, 'ellipsis', totalPages - 2, totalPages - 1, totalPages];
  return [1, 'ellipsis', currentPage - 1, currentPage, currentPage + 1, 'ellipsis', totalPages];
};

const IdentityReference = ({ reference }: { reference: AuditRecordSummary['author'] }) => {
  if (!reference) return <span className="audit-missing">— ausente</span>;
  if (reference.label) return <span>{reference.label}</span>;
  return <span className="audit-reference-missing">{reference.type === 'curso' ? 'Curso · título não disponível' : 'Nome não disponível'} · {shortReference(reference.id)}</span>;
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
