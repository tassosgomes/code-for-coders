type StepSummaryProps = { label: string; value: string; onEdit: () => void; disabled?: boolean };
export const StepSummary = ({ label, value, onEdit, disabled = false }: StepSummaryProps) => <div className="courtesy-step-summary-card" aria-label={`Resumo ${label}`}>
  <span className="courtesy-step-summary-label">{label}</span>
  <span className="courtesy-step-summary-value">{value}</span>
  <button type="button" className="text-button courtesy-step-summary-edit" disabled={disabled} aria-label={`Editar ${label.toLowerCase()}`} onClick={onEdit}>Editar</button>
</div>;
