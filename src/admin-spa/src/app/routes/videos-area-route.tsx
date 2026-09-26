import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useBlocker, useOutletContext } from 'react-router';

import type { StaffSession } from '@/features/staff-session/api/staff-session';
import { completeVideoUpload } from '@/features/videos/api/complete-video-upload';
import { createVideoUpload } from '@/features/videos/api/create-video-upload';
import { createVideoUploadPartUrls } from '@/features/videos/api/create-video-upload-part-urls';
import { getVideoUpload } from '@/features/videos/api/get-video-upload';
import { getPendingVideoUploadsQueryOptions, usePendingVideoUploads } from '@/features/videos/api/get-pending-video-uploads';
import { getVideosQueryOptions, useVideos } from '@/features/videos/api/get-videos';
import { VideoUploadDialog } from '@/features/videos/components/video-upload-dialog';
import { VideosAreaScreen } from '@/features/videos/components/videos-area-screen';
import type { VideoTransferView } from '@/features/videos/components/video-transfer-panel';
import { createVideoFingerprint, getVideoUploadErrorMessage, getVideoUploadErrorCode, isVideoPartForbidden, maximumParallelVideoParts, maximumPartUrlBatchSize, putVideoPart } from '@/features/videos/utils/video-upload';

type TransferSession = {
  uploadId: string;
  file: File;
  title: string;
  partSize: number;
  partCount: number;
  completionIdempotencyKey: string;
};

const pause = (milliseconds: number) => new Promise<void>((resolve) => window.setTimeout(resolve, milliseconds));

const bytesInPart = (fileSize: number, partSize: number, partNumber: number) =>
  Math.max(0, Math.min(partSize, fileSize - ((partNumber - 1) * partSize)));

export const VideosAreaRoute = () => {
  const session = useOutletContext<StaffSession>();
  if (!session.permissions.includes('midia.enviar')) {
    return <VideosAreaScreen state="forbidden" />;
  }

  return <VideosAreaContent />;
};

const VideosAreaContent = () => {
  const videos = useVideos();
  const pendingUploads = usePendingVideoUploads();
  const queryClient = useQueryClient();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogBusy, setDialogBusy] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [transfer, setTransfer] = useState<VideoTransferView | null>(null);
  const [transferInProgress, setTransferInProgress] = useState(false);
  const sessionRef = useRef<TransferSession | null>(null);
  const operationControllerRef = useRef<AbortController | null>(null);
  const blocker = useBlocker(transferInProgress);

  useEffect(() => {
    if (!transferInProgress) return undefined;
    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warnBeforeUnload);
    return () => window.removeEventListener('beforeunload', warnBeforeUnload);
  }, [transferInProgress]);

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
    setTransfer((current) => current ? { ...current, progress } : current);
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
              message: 'A conexão oscilou. Tentando novamente…',
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
      setTransfer((current) => current ? { ...current, status: 'completing', message: null } : current);
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
        setTransfer((current) => current ? { ...current, progress: 100, status: 'complete', message: null } : current);
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
          status: 'reconnecting',
          message: 'Conferindo as partes recebidas…',
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
    const divisor = receivedBytes >= 1024 * 1024 * 1024 ? 1024 * 1024 * 1024 : 1024 * 1024;
    const unit = divisor === 1024 * 1024 * 1024 ? 'GiB' : 'MiB';
    const resumedMessage = resumed
      ? receivedBytes > 0
        ? `Retomado de onde parou: ${new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 }).format(receivedBytes / divisor)} ${unit} já estavam na escola.`
        : 'Continuando o envio de onde parou.'
      : null;
    setTransfer({ fileName: session.file.name, title: session.title, progress, status: 'uploading', message: resumedMessage });
    try {
      await finishTransfer(session, receivedParts, controller);
    } catch (error) {
      if (controller.signal.aborted) return;
      setTransfer((current) => current ? {
        ...current,
        status: 'paused',
        message: getVideoUploadErrorMessage(error),
      } : current);
      setTransferInProgress(true);
    }
  };

  const startUpload = async (file: File, title: string) => {
    const controller = new AbortController();
    operationControllerRef.current = controller;
    setDialogBusy(true);
    setDialogError(null);
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
      setTransfer((current) => current ? { ...current, status: 'paused', message: getVideoUploadErrorMessage(error) } : current);
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
      onUpload={() => { setDialogError(null); setDialogOpen(true); }}
      onRetry={() => void videos.refetch()}
      onRetryTransfer={() => void retryTransfer()}
      onResumeUpload={() => { setDialogError(null); setDialogOpen(true); }}
    />
    {dialogOpen ? <VideoUploadDialog
      busy={dialogBusy}
      error={dialogError}
      onClose={() => { if (!dialogBusy) setDialogOpen(false); }}
      onStart={startUpload}
      pendingUploads={pendingUploads.data?.data ?? []}
    /> : null}
    {blocker.state === 'blocked' ? <div className="dialog-backdrop">
      <section aria-labelledby="leave-video-upload-title" aria-modal="true" className="dialog-card leave-upload-dialog" role="alertdialog">
        <h2 id="leave-video-upload-title">Sair durante o envio?</h2>
        <p>O envio será interrompido. Você pode ficar nesta tela até ele terminar.</p>
        <div className="dialog-actions">
          <button className="outline-button" onClick={() => blocker.reset()} type="button">Continuar enviando</button>
          <button className="primary-button" onClick={leaveDuringTransfer} type="button">Sair mesmo assim</button>
        </div>
      </section>
    </div> : null}
  </>;
};
