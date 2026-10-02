type CoursePaginationProps = { page: number; totalPages: number; label: string; disabled?: boolean; onChange: (page: number) => void };

export const CoursePagination = ({ page, totalPages, label, disabled = false, onChange }: CoursePaginationProps) => {
  if (totalPages < 1) return null;
  const start = Math.max(1, Math.min(page - 2, totalPages - 4));
  const pages = Array.from({ length: Math.min(5, totalPages) }, (_, index) => start + index);
  return <nav aria-label={label} className="course-pagination">
    <button aria-label="Página anterior" disabled={disabled || page === 1} onClick={() => onChange(page - 1)} type="button">‹</button>
    {pages.map((number) => <button aria-label={`Página ${number}`} aria-current={page === number ? 'page' : undefined} disabled={disabled} key={number} onClick={() => onChange(number)} type="button">{number}</button>)}
    <button aria-label="Próxima página" disabled={disabled || page >= totalPages} onClick={() => onChange(page + 1)} type="button">›</button>
    <span aria-hidden="true">· 20 por página</span><span className="sr-only">Página {page} de {totalPages} · 20 por página</span>
  </nav>;
};
