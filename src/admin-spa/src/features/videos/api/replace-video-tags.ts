import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';

import { getVideosQueryOptions, videoSchema, type VideoPage } from '@/features/videos/api/get-videos';
import { displayVideoTag, distinctVideoTags, normalizeVideoTag } from '@/features/videos/utils/video-tags';
import { apiClient } from '@/lib/api-client';

export const replaceVideoTagsInputSchema = z.object({
  tags: z.array(z.string().refine((tag) => {
    const display = displayVideoTag(tag);
    return display.length >= 1 && display.length <= 30 && normalizeVideoTag(display).length > 0 && !/\p{Cc}/u.test(tag);
  }, 'Cada tag precisa ter de 1 a 30 caracteres.')).refine((tags) => distinctVideoTags(tags).length <= 10, 'Um vídeo pode ter no máximo 10 tags.'),
});
export type ReplaceVideoTagsInput = z.infer<typeof replaceVideoTagsInputSchema>;

export const replaceVideoTags = async (videoId: string, input: ReplaceVideoTagsInput, idempotencyKey: string) =>
  videoSchema.parse(await apiClient.put<unknown>(`/api/v1/videos/${videoId}/tags`, input, {
    headers: { 'Idempotency-Key': idempotencyKey },
  }));

export const useReplaceVideoTags = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ videoId, input, idempotencyKey }: { videoId: string; input: ReplaceVideoTagsInput; idempotencyKey: string }) =>
      replaceVideoTags(videoId, input, idempotencyKey),
    onSuccess: async (video) => {
      const queryKey = getVideosQueryOptions().queryKey.slice(0, 1);
      queryClient.setQueriesData<VideoPage>({ queryKey }, (page) => page ? {
        ...page, data: page.data.map((current) => current.videoId === video.videoId ? video : current),
      } : page);
      await queryClient.invalidateQueries({ queryKey, refetchType: 'none' });
    },
  });
};
