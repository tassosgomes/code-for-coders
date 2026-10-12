import { Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';

import { Dialog } from '@/components/ui/dialog';
import { SingleChoiceForm } from '@/components/ui/form/single-choice-form';
import { CoursePagination } from '@/features/course-authoring/components/course-pagination';
import { paths } from '@/config/paths';
import { useReadyVideos } from '@/features/course-authoring/api/get-ready-videos';
import { updateLessonInputSchema } from '@/features/course-authoring/api/update-lesson';
import type { CourseLesson } from '@/features/course-authoring/types/course';

// O campo de tags vem da composição em app/ (features não importam umas das outras).
export type VideoTagFilterProps = { tags: string[]; disabled: boolean; onChange: (tags: string[]) => void };
export type VideoTagFilterRenderer = (filter: VideoTagFilterProps) => ReactNode;
type CourseVideoPickerProps = { lesson: CourseLesson; busy: boolean; error?: string; renderTagFilter: VideoTagFilterRenderer; onClose: () => void; onSave: (videoId: string | null) => Promise<boolean> };
export const CourseVideoPicker = ({ lesson, busy, error, renderTagFilter, onClose, onSave }: CourseVideoPickerProps) => {
  const [query, setQuery] = useState(''); const [tags, setTags] = useState<string[]>([]); const [tagMatch, setTagMatch] = useState<'all' | 'any'>('all'); const [page, setPage] = useState(1);
  const videos = useReadyVideos({ page, query, tags, tagMatch });
  const filtered = Boolean(query) || tags.length > 0;
  const changeTags = (nextTags: string[]) => { setTags(nextTags); setPage(1); };
  const changeTagMatch = (nextMatch: 'all' | 'any') => { setTagMatch(nextMatch); setPage(1); };
  const clearFilters = () => { setQuery(''); setTags([]); setTagMatch('all'); setPage(1); };
  const save = async (videoId: string | null) => { if (await onSave(videoId)) onClose(); };
  return <Dialog className="course-video-sheet course-ready-video-sheet" title="Escolher vídeo" description="Só vídeos prontos desta escola podem entrar na aula." busy={busy} onClose={onClose}>
    <p className="course-picker-context">Para “{lesson.title}”</p>
    <label className="course-video-search"><span className="sr-only">Buscar título</span><Search size={16} /><input placeholder="Buscar título" autoFocus type="search" value={query} disabled={busy} onChange={(event) => { setQuery(event.target.value); setPage(1); }} /></label>
    <div className="course-video-filters">
      {renderTagFilter({ tags, disabled: busy, onChange: changeTags })}
      {tags.length >= 2 ? <div className="video-tag-match"><span id="course-video-tag-match-label">Combinar tags</span>
        <div aria-labelledby="course-video-tag-match-label" className="video-filter-options" role="group">
          <button aria-pressed={tagMatch === 'all'} className={tagMatch === 'all' ? 'video-filter-active' : ''} disabled={busy} onClick={() => changeTagMatch('all')} type="button">E</button>
          <button aria-pressed={tagMatch === 'any'} className={tagMatch === 'any' ? 'video-filter-active' : ''} disabled={busy} onClick={() => changeTagMatch('any')} type="button">OU</button>
        </div>
      </div> : null}
    </div>
    {videos.isPending ? <p role="status">Carregando vídeos…</p> : null}
    {videos.isError ? <div className="inline-alert" role="alert"><p>Não foi possível carregar os vídeos.</p><button type="button" disabled={videos.isFetching || busy} onClick={() => void videos.refetch()}>Tentar de novo</button></div> : null}
    {error ? <div className="inline-alert" role="alert"><p>{error}</p><button type="button" disabled={videos.isFetching || busy} onClick={() => void videos.refetch()}>Atualizar vídeos</button></div> : null}
    {!videos.isPending && !videos.isError && !videos.data?.data.length ? <p>{tags.length ? 'Nenhum vídeo pronto atende aos filtros.' : query ? 'Nenhum vídeo pronto com esse título.' : 'Ainda não há vídeos prontos. Envie um vídeo na área Vídeos e volte quando a preparação terminar.'}</p> : null}
    {filtered && !videos.data?.data.length ? <button type="button" onClick={clearFilters}>{tags.length ? 'Limpar filtros' : 'Limpar busca'}</button> : null}
    <SingleChoiceForm schema={updateLessonInputSchema.pick({ videoId: true }).required()} fieldName="videoId" defaultValues={{ videoId: lesson.video?.videoId ?? null }} legend="Vídeos prontos" actions={<button className="outline-button" type="button" disabled={busy} onClick={onClose}>Cancelar</button>} submitLabel="Vincular vídeo" disabled={busy || videos.isFetching || videos.isError} onSubmit={(input) => save(input.videoId ?? null)} options={(videos.isError ? [] : videos.data?.data ?? []).map((video) => ({ value: video.videoId, label: <><strong>{video.title}</strong><span>{Math.floor(video.durationSeconds / 60)}:{String(video.durationSeconds % 60).padStart(2, '0')} · {video.uploadedBy.name}</span><span className="course-ready-badge">Pronto</span>{video.tags.length ? <span className="course-ready-tags">{[...video.tags].sort((a, b) => a.localeCompare(b, 'pt-BR')).map((tag) => <span className="video-tag-chip" key={tag}>{tag}</span>)}</span> : null}</> }))}>
    <CoursePagination label="Páginas de vídeos" page={page} totalPages={videos.data?.pagination.totalPages ?? 0} disabled={busy || videos.isFetching} onChange={setPage} />
    <button className="text-button" type="button" disabled={videos.isFetching || busy} onClick={() => void videos.refetch()}>Atualizar lista</button>
    <div className="course-video-links"><Link to={paths.videos.getHref()}>Ir para Vídeos</Link>{lesson.video ? <button className="outline-button" type="button" disabled={busy} onClick={() => void save(null)}>Desvincular vídeo</button> : null}</div>
    </SingleChoiceForm>
  </Dialog>;
};
