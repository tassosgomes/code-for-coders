import { videoSchema } from '@/features/videos/api/get-videos';
import { apiClient } from '@/lib/api-client';

export const completeVideoUpload = async (uploadId: string, idempotencyKey: string, signal?: AbortSignal) =>
  videoSchema.parse(
    await apiClient.post<unknown>(`/api/v1/video-uploads/${uploadId}/complete`, undefined, {
      headers: { 'Idempotency-Key': idempotencyKey },
      signal,
    }),
  );
