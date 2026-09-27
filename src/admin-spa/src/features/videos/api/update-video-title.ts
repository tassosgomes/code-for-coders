import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';

import { videoSchema } from '@/features/videos/api/get-videos';
import { apiClient } from '@/lib/api-client';

export const updateVideoTitleInputSchema = z.object({
  title: z.string().trim().min(1, 'Dê um título para reconhecer o vídeo.').max(200, 'O título deve ter até 200 caracteres.'),
});

export type UpdateVideoTitleInput = z.infer<typeof updateVideoTitleInputSchema>;

export const updateVideoTitle = async (videoId: string, input: UpdateVideoTitleInput) =>
  videoSchema.parse(await apiClient.patch<unknown>(`/api/v1/videos/${videoId}`, input, {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  }));

export const useUpdateVideoTitle = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ videoId, input }: { videoId: string; input: UpdateVideoTitleInput }) => updateVideoTitle(videoId, input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['videos'] });
    },
  });
};
