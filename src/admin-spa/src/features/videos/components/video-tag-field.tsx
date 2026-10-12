import { useEffect, useId, useRef, useState } from 'react';
import { CircleAlert, Search, X } from 'lucide-react';

import { replaceVideoTagsInputSchema } from '@/features/videos/api/replace-video-tags';
import { useVideoTagSuggestions } from '@/features/videos/hooks/use-video-tag-suggestions';
import { distinctVideoTags, displayVideoTag, normalizeVideoTag } from '@/features/videos/utils/video-tags';

type VideoTagFieldProps = {
  label?: string; autoFocus?: boolean; tags: string[]; disabled: boolean; error?: string | null; mode?: 'free-text' | 'suggestion-only';
  help?: string; onChange: (tags: string[]) => void; onDraftChange: (draft: string) => void;
};
// Tag Field do DS (320:2474). No modo de filtro (só sugestões existentes) segue o "Filtro por tags" de V1.i e A6.f:
// rótulo "Tags", etiquetas antes do campo, lupa e placeholder "Filtrar por tag".
export const VideoTagField = ({ label = 'Tags', autoFocus = true, tags, disabled, error, mode = 'free-text', help, onChange, onDraftChange }: VideoTagFieldProps) => {
  const id = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [draft, setDraft] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const [localError, setLocalError] = useState<string | null>(null);
  const [duplicate, setDuplicate] = useState<string | null>(null);
  const { suggestions, isFetching } = useVideoTagSuggestions(draft, !disabled && open);
  const filter = mode === 'suggestion-only';
  const expanded = open && suggestions.length > 0;
  const active = expanded ? suggestions[activeIndex] : undefined;
  useEffect(() => { if (autoFocus) inputRef.current?.focus(); }, [autoFocus]);
  const confirm = (suggestion?: string) => {
    const canonical = suggestion ?? suggestions.find((tag) => normalizeVideoTag(tag) === normalizeVideoTag(displayVideoTag(draft)));
    const value = canonical ?? (filter ? '' : draft);
    if (!value) {
      if (filter) setLocalError('Escolha uma tag existente nas sugestões.');
      return;
    }
    const result = replaceVideoTagsInputSchema.safeParse({ tags: [...tags, value] });
    if (!result.success) { setLocalError(result.error.issues[0]?.message ?? null); return; }
    const existing = tags.find((tag) => normalizeVideoTag(tag) === normalizeVideoTag(displayVideoTag(value)));
    setDuplicate(existing ?? null);
    onChange(distinctVideoTags(result.data.tags));
    setDraft(''); onDraftChange(''); setLocalError(null); setOpen(false); setActiveIndex(-1);
  };
  const message = localError ?? error;
  const hint = filter ? 'Use as setas e Enter para escolher uma tag existente.' : help ?? 'Enter ou vírgula confirma a tag.';
  const describedBy = message ? `${id}-error` : `${id}-hint`;
  const chips = tags.length ? <div className="video-tag-chips" aria-label="Tags selecionadas">{tags.map((tag) => <span className={`video-tag-chip is-removable${duplicate === tag ? ' video-tag-duplicate' : ''}`} key={tag}>{tag}
    <button type="button" disabled={disabled} aria-label={`Remover tag ${tag}`} onClick={() => { onChange(tags.filter((value) => value !== tag)); setLocalError(null); }}><X aria-hidden="true" size={12} /></button>
  </span>)}</div> : null;
  const input = <input id={id} ref={inputRef} value={draft} disabled={disabled} role="combobox" aria-autocomplete="list"
    placeholder={filter ? 'Filtrar por tag' : 'Digite e tecle Enter'}
    aria-expanded={expanded} aria-controls={`${id}-suggestions`} aria-activedescendant={active ? `${id}-option-${activeIndex}` : undefined}
    aria-invalid={Boolean(message)} aria-describedby={describedBy}
    onFocus={() => setOpen(true)} onBlur={() => { setOpen(false); setActiveIndex(-1); }}
    onChange={(event) => {
      setDraft(event.target.value); onDraftChange(event.target.value); setLocalError(null); setDuplicate(null); setOpen(true); setActiveIndex(-1);
    }}
    onKeyDown={(event) => {
      if (event.nativeEvent.isComposing) return;
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault(); setOpen(true);
        if (suggestions.length) setActiveIndex((index) => event.key === 'ArrowDown'
          ? (index + 1) % suggestions.length : (index <= 0 ? suggestions.length - 1 : index - 1));
      }
      if (event.key === 'Escape' && expanded) {
        event.preventDefault(); event.stopPropagation(); setOpen(false); setActiveIndex(-1);
      }
      if (event.key === 'Enter' || event.key === ',') { event.preventDefault(); confirm(active); }
      if (event.key === 'Backspace' && !draft && tags.length) { onChange(tags.slice(0, -1)); setLocalError(null); }
    }} />;
  return <div className={`video-tag-field${filter ? ' is-filter' : ''}`}>
    <label htmlFor={id}>{filter ? <><span className="visually-hidden">Filtrar por </span>Tags</> : label}</label>
    {filter ? <>{chips}<span className="video-tag-input"><Search aria-hidden="true" size={16} />{input}</span></> : <>{input}{chips}</>}
    <div className="video-tag-suggestions" hidden={!expanded}>
      <p aria-hidden="true">{suggestions.length} {suggestions.length === 1 ? 'sugestão' : 'sugestões'}</p>
      <ul id={`${id}-suggestions`} role="listbox" aria-label="Sugestões de tags">
        {suggestions.map((tag, index) => <li key={tag} id={`${id}-option-${index}`} role="option" aria-selected={index === activeIndex}
          onMouseDown={(event) => event.preventDefault()} onClick={() => confirm(tag)}>{tag}</li>)}
      </ul>
    </div>
    {message ? <p id={`${id}-error`} className="video-tag-error" role="alert"><CircleAlert aria-hidden="true" size={14} />{message}</p>
      : <p id={`${id}-hint`} className={`video-tag-help${filter ? ' visually-hidden' : ''}`} role={duplicate ? 'status' : undefined}>
        {duplicate ? `${duplicate} já incluída. Nenhuma etiqueta nova.` : hint}</p>}
    <span className="visually-hidden" role={expanded || (open && isFetching) ? 'status' : undefined} aria-live="polite">{expanded
      ? `${suggestions.length} sugestões disponíveis${active ? `, ${active}` : ''}` : open && isFetching ? 'Buscando tags…' : ''}</span>
  </div>;
};
