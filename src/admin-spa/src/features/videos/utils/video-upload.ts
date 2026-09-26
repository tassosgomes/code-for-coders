export const maxVideoFileSize = 5 * 1024 * 1024 * 1024;
export const maximumParallelVideoParts = 3;
export const maximumPartUrlBatchSize = 100;

const contentTypesByExtension: Record<string, string> = {
  '.mkv': 'video/x-matroska',
  '.mov': 'video/quicktime',
  '.mp4': 'video/mp4',
};

export const getVideoFileProblem = (file: File): string | null => {
  if (file.size < 1) return 'O arquivo está vazio.';
  if (file.size > maxVideoFileSize) return 'O arquivo deve ter até 5 GiB.';

  const extension = file.name.slice(file.name.lastIndexOf('.')).toLocaleLowerCase('en-US');
  const expectedContentType = contentTypesByExtension[extension];
  if (!expectedContentType || file.type !== expectedContentType) {
    return 'Formato não aceito. Escolha um arquivo MP4, MOV ou MKV.';
  }

  if (file.name.length > 255) return 'O nome do arquivo deve ter até 255 caracteres.';
  return null;
};

export const createVideoFingerprint = async (file: File): Promise<string> => {
  const source = new TextEncoder().encode(JSON.stringify([file.name, file.size, file.lastModified]));
  const digest = await crypto.subtle.digest('SHA-256', source);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
};

export const putVideoPart = (
  url: string,
  part: Blob,
  onProgress: (loaded: number) => void,
  signal?: AbortSignal,
): Promise<void> => new Promise((resolve, reject) => {
  const request = new XMLHttpRequest();
  if (signal?.aborted) {
    reject(new Error('The upload was cancelled.'));
    return;
  }

  request.open('PUT', url);
  request.withCredentials = false;
  request.upload.onprogress = (event) => {
    if (event.lengthComputable) onProgress(event.loaded);
  };
  request.onload = () => {
    if (request.status >= 200 && request.status < 300) {
      resolve();
    } else {
      reject(new VideoPartUploadError(request.status));
    }
  };
  request.onerror = () => reject(new Error('A conexão foi interrompida durante o envio.'));
  request.ontimeout = () => reject(new Error('A conexão demorou para responder.'));
  request.onabort = () => reject(new Error('The upload was cancelled.'));
  signal?.addEventListener('abort', () => request.abort(), { once: true });
  request.send(part);
});

export class VideoPartUploadError extends Error {
  constructor(public readonly statusCode: number) {
    super('Não foi possível enviar uma parte do vídeo.');
    this.name = 'VideoPartUploadError';
  }
}

export const isVideoPartForbidden = (error: unknown) =>
  error instanceof VideoPartUploadError && error.statusCode === 403;

export const getVideoUploadErrorCode = (error: unknown): string | null => {
  if (typeof error !== 'object' || error === null || !('response' in error)) return null;
  const response = error.response;
  if (typeof response !== 'object' || response === null || !('data' in response)) return null;
  const data = response.data;
  if (typeof data !== 'object' || data === null || !('code' in data)) return null;
  return typeof data.code === 'string' ? data.code : null;
};

export const getVideoUploadErrorMessage = (error: unknown): string => {
  const code = getVideoUploadErrorCode(error);
  switch (code) {
    case 'TITLE_REQUIRED': return 'Informe um título para o vídeo.';
    case 'FILE_TOO_LARGE': return 'O arquivo deve ter até 5 GiB.';
    case 'FORMAT_NOT_SUPPORTED': return 'Formato não aceito. Escolha um arquivo MP4, MOV ou MKV.';
    case 'UPLOAD_INCOMPLETE': return 'Ainda faltam partes. Vamos conferir o que chegou e tentar de novo.';
    default: return 'Não foi possível concluir o envio. Confira a conexão e tente novamente.';
  }
};
