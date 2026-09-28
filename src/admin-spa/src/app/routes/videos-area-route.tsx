import { useQueryClient } from '@tanstack/react-query';
import { X } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useBlocker, useOutletContext, useSearchParams } from 'react-router';

import type { StaffSession } from '@/features/staff-session/api/staff-session';
import { completeVideoUpload } from '@/features/videos/api/complete-video-upload';
import { createVideoUpload } from '@/features/videos/api/create-video-upload';
import { createVideoUploadPartUrls } from '@/features/videos/api/create-video-upload-part-urls';
import { getVideoUpload } from '@/features/videos/api/get-video-upload';
import { getPendingVideoUploadsQueryOptions, usePendingVideoUploads } from '@/features/videos/api/get-pending-video-uploads';
import { getVideosQueryOptions, useVideos, type VideoPage, type VideoStatus } from '@/features/videos/api/get-videos';
import { useUpdateVideoTitle } from '@/features/videos/api/update-video-title';
import { EditVideoTitleDialog } from '@/features/videos/components/edit-video-title-dialog';
import { VideoUploadDialog } from '@/features/videos/components/video-upload-dialog';
import { VideosAreaScreen } from '@/features/videos/components/videos-area-screen';
import type { VideoTransferView } from '@/features/videos/components/video-transfer-panel';
import { createVideoFingerprint, getVideoUploadErrorMessage, getVideoUploadErrorCode, isVideoPartForbidden, maximumParallelVideoParts, maximumPartUrlBatchSize, putVideoPart } from '@/features/videos/utils/video-upload';

type TransferSession = {
  uploadId: string;
  file: File;
  title: string;
  expiresAt: string;
  partSize: number;
  partCount: number;
  completionIdempotencyKey: string;
};

const pause = (milliseconds: number) => new Promise<void>((resolve) => window.setTimeout(resolve, milliseconds));

const bytesInPart = (fileSize: number, partSize: number, partNumber: number) =>
  Math.max(0, Math.min(partSize, fileSize - ((partNumber - 1) * partSize)));

const formatBytes = (bytes: number) => {
  const divisor = bytes >= 1_000_000_000 ? 1_000_000_000 : bytes >= 1_000_000 ? 1_000_000 : bytes >= 1_000 ? 1_000 : 1;
  const unit = divisor === 1_000_000_000 ? 'GB' : divisor === 1_000_000 ? 'MB' : divisor === 1_000 ? 'KB' : 'B';
  return `${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: divisor === 1 ? 0 : 1 }).format(bytes / divisor)} ${unit}`;
};

const formatExpiry = (value: string) => {
  const parts = new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  }).formatToParts(new Date(value));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';
  return `${part('day')}/${part('month')} às ${part('hour')}:${part('minute')}`;
};

export const VideosAreaRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('midia.enviar')) {
    return <VideosAreaScreen state="forbidden" />;
  }

  return <VideosAreaContent session={session} />;
};

const VideosAreaContent = ({ session }: { session: StaffSession }) => {
  const [searchParams, setSearchParams] = useSearchParams();
  const filter = searchParams.get('status') ?? 'all';
  const search = searchParams.get('q') ?? '';
  const [searchInput, setSearchInput] = useState(search);
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const statuses: VideoStatus[] = filter === 'progress' ? ['received', 'preparing']
    : filter === 'ready' || filter === 'failed' ? [filter] : [];
  const videos = useVideos({ page, size: 20, statuses, query: search.trim() || undefined });
  const pendingUploads = usePendingVideoUploads();
  const queryClient = useQueryClient();
  const updateTitle = useUpdateVideoTitle();
  const [editingVideo, setEditingVideo] = useState<VideoPage['data'][number] | null>(null);
  const [editError, setEditError] = useState<string | null>(null);
  const [editIdempotencyKey, setEditIdempotencyKey] = useState('');
  const [successToast, setSuccessToast] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogBusy, setDialogBusy] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [transfer, setTransfer] = useState<VideoTransferView | null>(null);
  const [transferInProgress, setTransferInProgress] = useState(false);
  const sessionRef = useRef<TransferSession | null>(null);
  const operationControllerRef = useRef<AbortController | null>(null);
  const transferStartedAtRef = useRef(0);
  const transferInitialBytesRef = useRef(0);
  const blocker = useBlocker(transferInProgress);

  const changeFilters = useCallback((nextFilter: string, nextSearch: string, nextPage = 1) => {
    const params = new URLSearchParams();
    if (nextFilter !== 'all') params.set('status', nextFilter);
    if (nextSearch.trim()) params.set('q', nextSearch);
    if (nextPage > 1) params.set('page', String(nextPage));
    setSearchParams(params);
  }, [setSearchParams]);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      if (searchInput !== search) changeFilters(filter, searchInput);
    }, 300);
    return () => window.clearTimeout(timeout);
  }, [searchInput, search, filter, changeFilters]);

  useEffect(() => {
    if (!transferInProgress) return undefined;
    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warnBeforeUnload);
    return () => window.removeEventListener('beforeunload', warnBeforeUnload);
  }, [transferInProgress]);

  useEffect(() => {
    if (!successToast) return undefined;
    const timeout = window.setTimeout(() => setSuccessToast(null), 4000);
    return () => window.clearTimeout(timeout);
  }, [successToast]);

  const updateProgress = (
    session: TransferSession,
    completedParts: ReadonlySet<number>,
    partProgress: ReadonlyMap<number, number>,
  ) => {
    let uploadedBytes = 0;
    for (let partNumber = 1; partNumber <= session.partCount; partNumber += 1) {
      const size = bytesInPart(session.file.size, session.partSize, partNumber);
      uploadedBytes += completedParts.has(partNumber)
        ? size
        : Math.min(size, partProgress.get(partNumber) ?? 0);
    }
    const progress = Math.min(100, Math.round((uploadedBytes / session.file.size) * 100));
    const elapsedSeconds = Math.max(0, (Date.now() - transferStartedAtRef.current) / 1000);
    const newlyUploadedBytes = Math.max(0, uploadedBytes - transferInitialBytesRef.current);
    const remainingSeconds = newlyUploadedBytes >= 1_000_000 && elapsedSeconds >= 2 && uploadedBytes < session.file.size
      ? Math.ceil((elapsedSeconds / newlyUploadedBytes) * (session.file.size - uploadedBytes))
      : null;
    setTransfer((current) => current ? { ...current, progress, transferredBytes: uploadedBytes, remainingSeconds } : current);
  };

  const uploadMissingParts = async (
    session: TransferSession,
    receivedParts: readonly number[],
    controller: AbortController,
  ) => {
    const completedParts = new Set(receivedParts.filter((number) => number >= 1 && number <= session.partCount));
    const partProgress = new Map<number, number>();
    updateProgress(session, completedParts, partProgress);

    const missingParts = Array.from({ length: session.partCount }, (_, index) => index + 1)
      .filter((partNumber) => !completedParts.has(partNumber));
    const partUrls = new Map<number, { url: string; expiresAt: string }>();
    let partUrlRequest: Promise<void> | null = null;
    const requestPartUrlBatch = async () => {
      if (partUrlRequest) return partUrlRequest;
      const batch = missingParts
        .filter((partNumber) => !partUrls.has(partNumber))
        .slice(0, Math.min(maximumPartUrlBatchSize, maximumParallelVideoParts * 2));
      if (batch.length === 0) return;
      partUrlRequest = createVideoUploadPartUrls(session.uploadId, batch, controller.signal)
        .then((response) => {
          for (const part of response.parts) partUrls.set(part.partNumber, { url: part.url, expiresAt: part.expiresAt });
        })
        .finally(() => { partUrlRequest = null; });
      return partUrlRequest;
    };
    const getPartUrl = async (partNumber: number) => {
      const cachedPartUrl = partUrls.get(partNumber);
      if (!cachedPartUrl || Date.parse(cachedPartUrl.expiresAt) <= Date.now() + 60_000) {
        partUrls.delete(partNumber);
        await requestPartUrlBatch();
      }
      const partUrl = partUrls.get(partNumber);
      if (!partUrl) throw new Error('A part URL was not returned.');
      return partUrl.url;
    };
    const renewPartUrl = async (partNumber: number) => {
      const response = await createVideoUploadPartUrls(session.uploadId, [partNumber], controller.signal);
      const part = response.parts.find((value) => value.partNumber === partNumber);
      if (!part) throw new Error('A renewed part URL was not returned.');
      partUrls.set(partNumber, { url: part.url, expiresAt: part.expiresAt });
      return part.url;
    };

    let nextPartIndex = 0;
    const sendNextParts = async () => {
      while (nextPartIndex < missingParts.length) {
        if (controller.signal.aborted) throw new Error('The upload was cancelled.');
        const partNumber = missingParts[nextPartIndex];
        nextPartIndex += 1;
        if (partNumber === undefined) return;
        let url = await getPartUrl(partNumber);
        const start = (partNumber - 1) * session.partSize;
        const end = Math.min(start + session.partSize, session.file.size);
        const part = session.file.slice(start, end);

        let uploaded = false;
        let urlRenewed = false;
        for (let attempt = 0; attempt <= 3 && !uploaded; attempt += 1) {
          try {
            await putVideoPart(url, part, (loaded) => {
              partProgress.set(partNumber, loaded);
              updateProgress(session, completedParts, partProgress);
            }, controller.signal);
            completedParts.add(partNumber);
            partProgress.delete(partNumber);
            updateProgress(session, completedParts, partProgress);
            uploaded = true;
          } catch (error) {
            if (isVideoPartForbidden(error)) {
              if (urlRenewed) throw error;
              url = await renewPartUrl(partNumber);
              urlRenewed = true;
              attempt -= 1;
              continue;
            }
            if (controller.signal.aborted || attempt === 3) throw error;
            setTransfer((current) => current ? {
              ...current,
              status: 'reconnecting',
              message: 'A parte que falhou é reenviada sozinha (até 3 tentativas).',
            } : current);
            await pause(250 * (attempt + 1));
            setTransfer((current) => current ? { ...current, status: 'uploading', message: null } : current);
          }
        }
      }
    };

    const workerCount = Math.min(maximumParallelVideoParts, missingParts.length);
    const workers = Array.from({ length: workerCount }, () => sendNextParts());
    const results = await Promise.allSettled(workers);
    const failure = results.find((result): result is PromiseRejectedResult => result.status === 'rejected');
    if (failure) throw failure.reason;
    return completedParts;
  };

  const finishTransfer = async (
    session: TransferSession,
    firstReceivedParts: readonly number[],
    controller: AbortController,
  ) => {
    let receivedParts = firstReceivedParts;
    for (let incompleteRetry = 0; incompleteRetry <= 3; incompleteRetry += 1) {
      await uploadMissingParts(session, receivedParts, controller);
      if (controller.signal.aborted) return;
      setTransfer((current) => current ? { ...current, status: 'completing', message: 'Quase lá: confirmando as partes recebidas.' } : current);
      try {
        const completedVideo = await completeVideoUpload(session.uploadId, session.completionIdempotencyKey, controller.signal);
        queryClient.setQueryData(getVideosQueryOptions().queryKey, (currentPage) => {
          if (!currentPage) return currentPage;
          const alreadyListed = currentPage.data.some((video) => video.videoId === completedVideo.videoId);
          const total = currentPage.pagination.total + (alreadyListed ? 0 : 1);
          return {
            ...currentPage,
            data: [completedVideo, ...currentPage.data.filter((video) => video.videoId !== completedVideo.videoId)],
            pagination: {
              ...currentPage.pagination,
              total,
              totalPages: Math.ceil(total / currentPage.pagination.size),
            },
          };
        });
        setTransfer(null);
        setSuccessToast(`${session.title} recebido. A preparação começou.`);
        setTransferInProgress(false);
        sessionRef.current = null;
        operationControllerRef.current = null;
        await queryClient.invalidateQueries({ queryKey: getVideosQueryOptions().queryKey });
        await queryClient.invalidateQueries({ queryKey: getPendingVideoUploadsQueryOptions().queryKey });
        return;
      } catch (error) {
        if (getVideoUploadErrorCode(error) !== 'UPLOAD_INCOMPLETE' || incompleteRetry === 3) throw error;
        setTransfer((current) => current ? {
          ...current,
          status: 'completing',
          message: 'Quase lá: confirmando as partes recebidas.',
        } : current);
        const currentUpload = await getVideoUpload(session.uploadId, controller.signal);
        receivedParts = currentUpload.receivedParts;
      }
    }
  };

  const startTransfer = async (
    session: TransferSession,
    receivedParts: readonly number[],
    controller: AbortController,
    resumed = false,
  ) => {
    setTransferInProgress(true);
    const receivedBytes = receivedParts.reduce(
      (total, partNumber) => total + bytesInPart(session.file.size, session.partSize, partNumber),
      0,
    );
    const progress = Math.min(100, Math.round((receivedBytes / session.file.size) * 100));
    const resumedMessage = resumed
      ? receivedBytes > 0
        ? `Retomado de onde parou: ${formatBytes(receivedBytes)} já estavam na escola.`
        : 'Continuando o envio de onde parou.'
      : null;
    transferStartedAtRef.current = Date.now();
    transferInitialBytesRef.current = receivedBytes;
    setTransfer({
      fileName: session.file.name,
      title: session.title,
      progress,
      transferredBytes: receivedBytes,
      totalBytes: session.file.size,
      remainingSeconds: null,
      resumedBytes: receivedBytes,
      resumed,
      status: 'uploading',
      message: resumedMessage,
    });
    try {
      await finishTransfer(session, receivedParts, controller);
    } catch (error) {
      if (controller.signal.aborted) return;
      setTransfer((current) => current ? {
        ...current,
        status: 'paused',
        message: `${getVideoUploadErrorMessage(error)} As partes já enviadas ficam guardadas até ${formatExpiry(session.expiresAt)}.`,
      } : current);
      setTransferInProgress(true);
    }
  };

  const startUpload = async (file: File, title: string) => {
    const controller = new AbortController();
    operationControllerRef.current = controller;
    setDialogBusy(true);
    setDialogError(null);
    setSuccessToast(null);
    setTransferInProgress(true);
    try {
      const fingerprint = await createVideoFingerprint(file);
      const upload = await createVideoUpload({
        title,
        fileName: file.name,
        fileSize: file.size,
        contentType: file.type,
        fingerprint,
      }, crypto.randomUUID(), controller.signal);
      const session: TransferSession = {
        uploadId: upload.uploadId,
        file,
        title: upload.title,
        expiresAt: upload.expiresAt,
        partSize: upload.partSize,
        partCount: upload.partCount,
        completionIdempotencyKey: crypto.randomUUID(),
      };
      sessionRef.current = session;
      setDialogOpen(false);
      const resumed = upload.receivedParts.length > 0
        || pendingUploads.data?.data.some((pending) => pending.fileName === file.name && pending.fileSize === file.size) === true;
      await startTransfer(session, upload.receivedParts, controller, resumed);
    } catch (error) {
      if (controller.signal.aborted) return;
      setTransferInProgress(false);
      operationControllerRef.current = null;
      setDialogError(getVideoUploadErrorMessage(error));
    } finally {
      setDialogBusy(false);
    }
  };

  const retryTransfer = async () => {
    const session = sessionRef.current;
    if (!session) return;
    const controller = new AbortController();
    operationControllerRef.current = controller;
    setTransferInProgress(true);
    setTransfer((current) => current ? { ...current, status: 'reconnecting', message: 'Conferindo as partes recebidas…' } : current);
    try {
      const upload = await getVideoUpload(session.uploadId, controller.signal);
      await startTransfer(session, upload.receivedParts, controller, true);
    } catch (error) {
      if (controller.signal.aborted) return;
      setTransfer((current) => current ? {
        ...current,
        status: 'paused',
        message: `${getVideoUploadErrorMessage(error)} As partes já enviadas ficam guardadas até ${formatExpiry(session.expiresAt)}.`,
      } : current);
      setTransferInProgress(true);
    }
  };

  const leaveDuringTransfer = () => {
    operationControllerRef.current?.abort();
    sessionRef.current = null;
    setTransferInProgress(false);
    if (blocker.state === 'blocked') blocker.proceed();
  };

  const state = videos.isPending
    ? 'loading'
    : videos.isError
      ? 'unavailable'
      : videos.data.data.length === 0
        ? 'empty'
        : 'has-videos';

  return <>
    <VideosAreaScreen
      state={state}
      videos={videos.data}
      pendingUploads={transferInProgress ? [] : pendingUploads.data?.data ?? []}
      uploadDisabled={transferInProgress}
      transfer={transfer}
      currentAccountId={session.accountId}
      onUpload={() => { setDialogError(null); setDialogOpen(true); }}
      onRetry={() => void videos.refetch()}
      onRetryTransfer={() => void retryTransfer()}
      onResumeUpload={() => { setDialogError(null); setDialogOpen(true); }}
      filter={filter}
      search={searchInput}
      onFilterChange={(value) => changeFilters(value, searchInput)}
      onSearchChange={setSearchInput}
      onPageChange={(value) => changeFilters(filter, search, value)}
      onEditTitle={(video) => { setEditError(null); setSuccessToast(null); setEditIdempotencyKey(crypto.randomUUID()); setEditingVideo(video); }}
      successMessage={successToast}
    />
    {editingVideo ? <EditVideoTitleDialog
      key={editingVideo.videoId}
      title={editingVideo.title}
      busy={updateTitle.isPending}
      error={editError}
      onClose={() => setEditingVideo(null)}
      onSave={async (input) => {
        try {
          await updateTitle.mutateAsync({ videoId: editingVideo.videoId, input, idempotencyKey: editIdempotencyKey });
          setEditingVideo(null);
          setSuccessToast('Título atualizado');
        } catch (error) {
          setEditError(getVideoUploadErrorCode(error) === 'TITLE_REQUIRED'
            ? 'TITLE_REQUIRED'
            : 'Não conseguimos atualizar o título agora. Tente novamente.');
        }
      }}
    /> : null}
    {dialogOpen ? <VideoUploadDialog
      busy={dialogBusy}
      error={dialogError}
      onClose={() => { if (!dialogBusy) setDialogOpen(false); }}
      onStart={startUpload}
      pendingUploads={pendingUploads.data?.data ?? []}
    /> : null}
    {blocker.state === 'blocked' ? <div className="dialog-backdrop">
      <section aria-labelledby="leave-video-upload-title" aria-modal="true" className="dialog-card leave-upload-dialog" role="alertdialog">
        <div className="video-dialog-heading">
          <h2 id="leave-video-upload-title">Sair interrompe o envio</h2>
          <button aria-label="Continuar enviando" className="dialog-close" onClick={() => blocker.reset()} type="button"><X size={18} /></button>
        </div>
        <p>{sessionRef.current?.file.name ?? 'O vídeo'} está em {transfer?.progress ?? 0}%. O que já foi enviado fica guardado até {sessionRef.current ? formatExpiry(sessionRef.current.expiresAt) : 'o prazo expirar'} — volte e selecione o mesmo arquivo para continuar.</p>
        <div className="dialog-actions">
          <button className="outline-button" onClick={leaveDuringTransfer} type="button">Sair mesmo assim</button>
          <button className="primary-button" onClick={() => blocker.reset()} type="button">Continuar enviando</button>
        </div>
      </section>
    </div> : null}
  </>;
};
