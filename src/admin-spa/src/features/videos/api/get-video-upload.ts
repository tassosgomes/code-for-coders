import { createVideoUploadResponseSchema } from '@/features/videos/api/create-video-upload';
import { apiClient } from '@/lib/api-client';

export const getVideoUpload = async (uploadId: string, signal?: AbortSignal) =>
  createVideoUploadResponseSchema.parse(
    await apiClient.get<unknown>(`/api/v1/video-uploads/${uploadId}`, { signal }),
  );
