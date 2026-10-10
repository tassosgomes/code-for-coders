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
  useEffect(() => {
    const node = container.current;
    if (!node) return;
    const onFocusOut = (event: FocusEvent) => {
      const next = event.relatedTarget;
      if (!(next instanceof Node) || !node.contains(next)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => { if (event.key === 'Escape') { setOpen(false); trigger.current?.focus(); } };
    const onClick = (event: MouseEvent) => {
      // Adiado: fechar agora desmontaria o item antes do onClick do React, e a ação nunca rodaria.
      if (event.target instanceof HTMLElement && event.target.closest('button') !== trigger.current && !event.target.closest('[data-keep-menu]')) { window.setTimeout(() => setOpen(false)); trigger.current?.focus(); }
    };
    node.addEventListener('focusout', onFocusOut);
    node.addEventListener('keydown', onKeyDown);
    node.addEventListener('click', onClick);
    return () => { node.removeEventListener('focusout', onFocusOut); node.removeEventListener('keydown', onKeyDown); node.removeEventListener('click', onClick); };
  }, []);
  return <div ref={container} className="course-item-actions">
    <button ref={trigger} type="button" className="course-action-trigger" aria-label={`Ações de ${title}`} aria-expanded={open} disabled={disabled} onClick={() => setOpen(!open)}>⋯</button>
    {open ? <div className="course-action-menu">{children}</div> : null}
  </div>;
};
