import { useEffect, useRef, useState, type ReactNode } from 'react';

type CourseItemActionsProps = { title: string; disabled: boolean; children: ReactNode };
export const CourseItemActions = ({ title, disabled, children }: CourseItemActionsProps) => {
  const [open, setOpen] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);
  const container = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const closeOutside = (event: PointerEvent) => { if (event.target instanceof Node && !container.current?.contains(event.target)) setOpen(false); };
    document.addEventListener('pointerdown', closeOutside);
    return () => document.removeEventListener('pointerdown', closeOutside);
  }, [open]);
  return <div ref={container} className="course-item-actions" onBlur={(event) => { if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false); }} onKeyDown={(event) => {
    if (event.key === 'Escape') { setOpen(false); trigger.current?.focus(); }
  }} onClick={(event) => {
    if (event.target instanceof HTMLElement && event.target.closest('button') !== trigger.current) { setOpen(false); trigger.current?.focus(); }
  }}>
    <button ref={trigger} type="button" className="course-action-trigger" aria-label={`Ações de ${title}`} aria-expanded={open} disabled={disabled} onClick={() => setOpen(!open)}>⋯</button>
    {open ? <div className="course-action-menu">{children}</div> : null}
  </div>;
};
