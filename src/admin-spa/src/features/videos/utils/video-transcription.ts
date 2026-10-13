import { getVideoUploadErrorCode } from '@/features/videos/utils/video-upload';

export const getVideoTranscriptionErrorMessage = (error: unknown): string => {
  switch (getVideoUploadErrorCode(error)) {
    case 'VIDEO_NOT_READY': return 'Este vídeo ainda não está pronto para ser transcrito.';
    case 'TRANSCRIPTION_NOT_RETRYABLE': return 'Refazer não resolve este caso. Envie um vídeo mais curto ou menor.';
    case 'PERMISSION_DENIED': return 'Você não tem permissão para pedir a transcrição.';
    case 'CSRF_INVALID': return 'Sua sessão mudou. Recarregue a página e tente de novo.';
    case 'VIDEO_NOT_FOUND': return 'Este vídeo não existe mais.';
    default: return 'Não foi possível pedir a transcrição agora. Tente de novo.';
  }
};

const genericFailureMessage = 'Não foi possível transcrever este vídeo.';
const failureMessages: Record<string, string> = {
  'duration-limit': 'Vídeo mais longo que o limite de 2 horas. Refazer não resolve: envie um vídeo mais curto.',
  'size-limit': 'Vídeo maior que o tamanho aceito para transcrição. Refazer não resolve: envie um vídeo menor.',
  'service-unavailable': 'Serviço de transcrição indisponível no momento.',
  'processing-failed': genericFailureMessage,
  'timed-out': 'A transcrição demorou mais que o limite.',
};

// A reason the screen does not know (a value added later) reads as the generic failure; the code itself is never shown.
export const getVideoTranscriptionFailureMessage = (reason: string | null): string =>
  (reason === null ? undefined : failureMessages[reason]) ?? genericFailureMessage;

export const currentTranscriptionRemainsMessage = 'A anterior continua valendo para os alunos.';
