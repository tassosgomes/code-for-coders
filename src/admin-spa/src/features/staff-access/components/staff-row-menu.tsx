import { Fragment, useEffect, useRef, useState } from 'react';
import { Ellipsis, type LucideIcon } from 'lucide-react';

export type StaffMenuItem = {
  destructive?: boolean;
  icon: LucideIcon;
  label: string;
  onSelect: () => void;
};

type StaffRowMenuProps = {
  groups: StaffMenuItem[][];
  label: string;
};

export const StaffRowMenu = ({ groups, label }: StaffRowMenuProps) => {
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (!open) return;
    const onMouseDown = (event: MouseEvent) => {
      if (!wrapRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.stopPropagation();
      setOpen(false);
      triggerRef.current?.focus();
    };
    document.addEventListener('mousedown', onMouseDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onMouseDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);
  const visibleGroups = groups.filter((group) => group.length > 0);

  return (
    <div className="row-menu-wrap" ref={wrapRef}>
      <button aria-expanded={open} aria-haspopup="menu" aria-label={label} className="icon-button" onClick={() => setOpen(!open)} ref={triggerRef} type="button"><Ellipsis size={18} /></button>
      {open ? <div className="row-menu" role="menu">
        {visibleGroups.map((group, index) => <Fragment key={group.map((item) => item.label).join('|')}>
          {index > 0 ? <div className="row-menu-separator" role="separator" /> : null}
          {group.map(({ destructive, icon: Icon, label: itemLabel, onSelect }) => <button className={destructive ? 'destructive' : undefined} key={itemLabel} onClick={() => { setOpen(false); onSelect(); }} role="menuitem" type="button"><Icon aria-hidden="true" size={16} />{itemLabel}</button>)}
        </Fragment>)}
      </div> : null}
    </div>
  );
};
