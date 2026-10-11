import { useEffect, useId, useRef, useState } from 'react';
import { X } from 'lucide-react';

import { replaceVideoTagsInputSchema } from '@/features/videos/api/replace-video-tags';
import { distinctVideoTags, displayVideoTag, normalizeVideoTag } from '@/features/videos/utils/video-tags';

type VideoTagFieldProps = {
  tags: string[]; disabled: boolean; error?: string | null;
  onChange: (tags: string[]) => void; onDraftChange: (draft: string) => void;
};
export const VideoTagField = ({ tags, disabled, error, onChange, onDraftChange }: VideoTagFieldProps) => {
  const id = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [draft, setDraft] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);
  const [duplicate, setDuplicate] = useState<string | null>(null);
  useEffect(() => { inputRef.current?.focus(); }, []);
  const confirm = () => {
    if (!draft) return;
    const result = replaceVideoTagsInputSchema.safeParse({ tags: [...tags, draft] });
    if (!result.success) { setLocalError(result.error.issues[0]?.message ?? null); return; }
    const existing = tags.find((tag) => normalizeVideoTag(tag) === normalizeVideoTag(displayVideoTag(draft)));
    setDuplicate(existing ?? null);
    onChange(distinctVideoTags(result.data.tags));
    setDraft(''); onDraftChange(''); setLocalError(null);
  };
  const message = localError ?? error;
  return <div className="video-tag-field">
    <label htmlFor={id}>Tags</label>
    <input id={id} ref={inputRef} value={draft} disabled={disabled} aria-invalid={Boolean(message)} aria-describedby={`${id}-hint${message ? ` ${id}-error` : ''}`}
      onChange={(event) => { setDraft(event.target.value); onDraftChange(event.target.value); setLocalError(null); setDuplicate(null); }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ',') { event.preventDefault(); confirm(); }
      }} />
    <div className="video-tag-chips" aria-label="Tags selecionadas">{tags.map((tag) => <span className={`video-tag-chip${duplicate === tag ? ' video-tag-duplicate' : ''}`} key={tag}>{tag}
      <button type="button" disabled={disabled} aria-label={`Remover tag ${tag}`} onClick={() => { onChange(tags.filter((value) => value !== tag)); setLocalError(null); }}><X aria-hidden="true" size={12} /></button>
    </span>)}</div>
    <p id={`${id}-hint`} className="video-title-hint">Enter ou vírgula adiciona uma tag. Até 10 tags, com 1 a 30 caracteres.</p>
    <span className="visually-hidden" role="status" aria-live="polite">{duplicate ? `${duplicate} já incluída` : ''}</span>
    {message ? <p id={`${id}-error`} className="field-error" role="alert">{message}</p> : null}
  </div>;
};
