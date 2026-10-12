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
