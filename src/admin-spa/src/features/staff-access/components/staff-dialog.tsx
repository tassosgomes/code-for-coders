import { useEffect, useRef, type ReactNode } from 'react';
import { X } from 'lucide-react';

type StaffDialogProps = {
  children: ReactNode;
  description?: string;
  onClose: () => void;
  title: string;
};

export const StaffDialog = ({ children, description, onClose, title }: StaffDialogProps) => {
  const dialogRef = useRef<HTMLElement>(null);
  const backdropRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    dialogRef.current?.querySelector('button')?.focus();
  }, []);
  useEffect(() => {
    const node = backdropRef.current;
    if (!node) return;
    const onMouseDown = (event: MouseEvent) => { if (event.target === node) onClose(); };
    node.addEventListener('mousedown', onMouseDown);
    return () => node.removeEventListener('mousedown', onMouseDown);
  }, [onClose]);

  return (
    <div className="dialog-backdrop" ref={backdropRef}>
      <section aria-describedby={description ? 'dialog-description' : undefined} aria-labelledby="dialog-title" aria-modal="true" className="dialog-card staff-dialog" onKeyDown={(event) => { if (event.key === 'Escape') onClose(); }} ref={dialogRef} role="dialog">
        <button aria-label="Fechar" className="dialog-close" onClick={onClose} type="button"><X size={16} /></button>
        <header className="staff-dialog-header"><h2 id="dialog-title">{title}</h2>{description ? <p id="dialog-description">{description}</p> : null}</header>
        {children}
      </section>
    </div>
  );
};
