import { Search } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router';

import { Dialog } from '@/components/ui/dialog';
import { SingleChoiceForm } from '@/components/ui/form/single-choice-form';
import { CoursePagination } from '@/features/course-authoring/components/course-pagination';
import { paths } from '@/config/paths';
import { useReadyVideos } from '@/features/course-authoring/api/get-ready-videos';
import { updateLessonInputSchema } from '@/features/course-authoring/api/update-lesson';
import type { CourseLesson } from '@/features/course-authoring/types/course';

type CourseVideoPickerProps = { lesson: CourseLesson; busy: boolean; error?: string; onClose: () => void; onSave: (videoId: string | null) => Promise<boolean> };
export const CourseVideoPicker = ({ lesson, busy, error, onClose, onSave }: CourseVideoPickerProps) => {
  const [query, setQuery] = useState(''); const [page, setPage] = useState(1);
  const videos = useReadyVideos({ page, query });
  const save = async (videoId: string | null) => { if (await onSave(videoId)) onClose(); };
  return <Dialog className="course-video-sheet course-ready-video-sheet" title="Escolher vídeo" description="Só vídeos prontos desta escola podem entrar na aula." busy={busy} onClose={onClose}>
    <p className="course-picker-context">Para “{lesson.title}”</p>
    <label className="course-video-search"><span className="sr-only">Buscar título</span><Search size={16} /><input placeholder="Buscar título" autoFocus type="search" value={query} disabled={busy} onChange={(event) => { setQuery(event.target.value); setPage(1); }} /></label>
    {videos.isPending ? <p role="status">Carregando vídeos…</p> : null}
    {videos.isError ? <div className="inline-alert" role="alert"><p>Não foi possível carregar os vídeos.</p><button type="button" disabled={videos.isFetching || busy} onClick={() => void videos.refetch()}>Tentar de novo</button></div> : null}
    {error ? <div className="inline-alert" role="alert"><p>{error}</p><button type="button" disabled={videos.isFetching || busy} onClick={() => void videos.refetch()}>Atualizar vídeos</button></div> : null}
    {!videos.isPending && !videos.isError && !videos.data?.data.length ? <p>{query ? 'Nenhum vídeo pronto com esse título.' : 'Ainda não há vídeos prontos. Envie um vídeo na área Vídeos e volte quando a preparação terminar.'}</p> : null}
    {query && !videos.data?.data.length ? <button type="button" onClick={() => { setQuery(''); setPage(1); }}>Limpar busca</button> : null}
    <SingleChoiceForm schema={updateLessonInputSchema.pick({ videoId: true }).required()} fieldName="videoId" defaultValues={{ videoId: lesson.video?.videoId ?? null }} legend="Vídeos prontos" actions={<button className="outline-button" type="button" disabled={busy} onClick={onClose}>Cancelar</button>} submitLabel="Vincular vídeo" disabled={busy || videos.isFetching || videos.isError} onSubmit={(input) => save(input.videoId ?? null)} options={(videos.isError ? [] : videos.data?.data ?? []).map((video) => ({ value: video.videoId, label: <><strong>{video.title}</strong><span>{Math.floor(video.durationSeconds / 60)}:{String(video.durationSeconds % 60).padStart(2, '0')} · {video.uploadedBy.name}</span><span className="course-ready-badge">Pronto</span></> }))}>
    <CoursePagination label="Páginas de vídeos" page={page} totalPages={videos.data?.pagination.totalPages ?? 0} disabled={busy || videos.isFetching} onChange={setPage} />
    <button className="text-button" type="button" disabled={videos.isFetching || busy} onClick={() => void videos.refetch()}>Atualizar lista</button>
    <div className="course-video-links"><Link to={paths.videos.getHref()}>Ir para Vídeos</Link>{lesson.video ? <button className="text-button" type="button" disabled={busy} onClick={() => void save(null)}>Desvincular vídeo</button> : null}</div>
    </SingleChoiceForm>
  </Dialog>;
};
