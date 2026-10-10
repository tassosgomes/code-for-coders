type ReviewRow = { label: string; value: string; onEdit: () => void };
type ReviewTableProps = { rows: ReviewRow[]; disabled: boolean };
export const ReviewTable = ({ rows, disabled }: ReviewTableProps) => <dl className="courtesy-review-table">
  {rows.map((row) => <div key={row.label} className="courtesy-review-row">
    <dt>{row.label}</dt><dd>{row.value}</dd>
    <button type="button" className="text-button" disabled={disabled} aria-label={`Editar ${row.label.toLowerCase()}`} onClick={row.onEdit}>Editar</button>
  </div>)}
</dl>;
