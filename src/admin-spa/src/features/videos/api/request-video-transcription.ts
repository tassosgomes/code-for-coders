import { useMutation, useQueryClient, type UseMutationOptions } from '@tanstack/react-query';

import { getVideosQueryOptions, videoSchema, type VideoPage } from '@/features/videos/api/get-videos';
import { apiClient } from '@/lib/api-client';

type RequestVideoTranscriptionVariables = { videoId: string; idempotencyKey: string };

export const requestVideoTranscription = async ({ videoId, idempotencyKey }: RequestVideoTranscriptionVariables) =>
  videoSchema.parse(await apiClient.post<unknown>(`/api/v1/videos/${videoId}/transcription-requests`, undefined, {
    headers: { 'Idempotency-Key': idempotencyKey },
  }));

type RequestVideoTranscriptionConfig = Omit<UseMutationOptions<Awaited<ReturnType<typeof requestVideoTranscription>>, Error, RequestVideoTranscriptionVariables>, 'mutationFn'>;

export const useRequestVideoTranscription = (mutationConfig: RequestVideoTranscriptionConfig = {}) => {
  const queryClient = useQueryClient();
  const queryKey = getVideosQueryOptions().queryKey.slice(0, 1);
  return useMutation({
    ...mutationConfig,
    mutationFn: requestVideoTranscription,
    onSuccess: async (video, input, onMutateResult, context) => {
      await queryClient.cancelQueries({ queryKey });
      queryClient.setQueriesData<VideoPage>({ queryKey }, (page) => page ? {
        ...page, data: page.data.map((current) => current.videoId === video.videoId ? video : current),
      } : page);
      await queryClient.invalidateQueries({ queryKey, refetchType: 'none' });
      await mutationConfig.onSuccess?.(video, input, onMutateResult, context);
    },
    onError: async (error, input, onMutateResult, context) => {
      await queryClient.invalidateQueries({ queryKey });
      await mutationConfig.onError?.(error, input, onMutateResult, context);
    },
  });
};
