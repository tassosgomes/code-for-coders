import { useEffect, useRef, type ReactNode } from 'react';
import { X } from 'lucide-react';

type DialogProps = { title: string; description: string; busy: boolean; onClose: () => void; children: ReactNode };
export const Dialog = ({ title, description, busy, onClose, children }: DialogProps) => {
  const ref = useRef<HTMLElement>(null);
  useEffect(() => {
    const previous = document.activeElement;
    return () => { if (previous instanceof HTMLElement) previous.focus(); };
  }, []);
  return <div className="dialog-backdrop">
    <section className="dialog-card course-create-dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title" aria-describedby="dialog-description" ref={ref} onKeyDown={(event) => {
      if (event.key === 'Escape' && !busy) { event.preventDefault(); onClose(); }
      if (event.key !== 'Tab') return;
      const controls = ref.current?.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), a[href]');
      const first = controls?.[0]; const last = controls?.[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    }}>
      <div className="course-dialog-heading"><div><h2 id="dialog-title">{title}</h2><p id="dialog-description">{description}</p></div>
        <button aria-label="Fechar" className="dialog-close" disabled={busy} onClick={onClose} type="button"><X size={18} /></button>
      </div>
      {children}
    </section>
  </div>;
};
