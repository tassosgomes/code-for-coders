import { useEffect, useId, useRef, type ReactNode } from 'react';
import { X } from 'lucide-react';

type DialogProps = { title: string; description: string; busy: boolean; onClose: () => void; children: ReactNode; className?: string; role?: 'dialog' | 'alertdialog' };
export const Dialog = ({ title, description, busy, onClose, children, className = '', role = 'dialog' }: DialogProps) => {
  const titleId = useId();
  const descriptionId = useId();
  const ref = useRef<HTMLElement>(null);
  const previousFocus = useRef(document.activeElement);
  useEffect(() => {
    const previous = previousFocus.current;
    if (!ref.current?.contains(document.activeElement)) {
      const first = ref.current?.querySelector<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), a[href]');
      (first ?? ref.current)?.focus();
    }
    return () => { if (previous instanceof HTMLElement) previous.focus(); };
  }, []);
  return <div className={`dialog-backdrop ${className}`}>
    <section className="dialog-card course-create-dialog" role={role} aria-modal="true" aria-labelledby={titleId} aria-describedby={descriptionId} ref={ref} tabIndex={-1} onKeyDown={(event) => {
      if (event.key === 'Escape' && !busy) { event.preventDefault(); onClose(); }
      if (event.key !== 'Tab') return;
      const controls = Array.from(ref.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), a[href]') ?? [])
        .filter((control) => !control.closest('[hidden]'));
      const first = controls?.[0]; const last = controls?.[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    }}>
      <div className="course-dialog-heading"><div><h2 id={titleId}>{title}</h2><p id={descriptionId}>{description}</p></div>
        <button aria-label="Fechar" className="dialog-close" disabled={busy} onClick={onClose} type="button"><X size={18} /></button>
      </div>
      {children}
    </section>
  </div>;
};
