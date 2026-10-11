type CatalogPaginationProps = { page: number; totalPages: number; size: number; disabled: boolean; onPageChange: (page: number) => void };

export const CatalogPagination = ({ page, totalPages, size, disabled, onPageChange }: CatalogPaginationProps) => {
  const pages = Array.from({ length: Math.max(totalPages, 1) }, (_, index) => index + 1);
  return <nav aria-label="Páginas do catálogo" className="catalog-pagination">
    <button aria-label="Página anterior" disabled={page <= 1 || disabled} onClick={() => onPageChange(page - 1)} type="button">‹</button>
    {pages.map((item) => <button aria-current={item === page ? 'page' : undefined} aria-label={`Página ${item}`} disabled={disabled} key={item} onClick={() => onPageChange(item)} type="button">{item}</button>)}
    <button aria-label="Próxima página" disabled={page >= totalPages || disabled} onClick={() => onPageChange(page + 1)} type="button">›</button>
    <span>· {size} por página</span>
  </nav>;
};
