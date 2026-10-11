import { useEffect, useState } from 'react';

import { useVideoTags } from '@/features/videos/api/get-video-tags';
import { displayVideoTag } from '@/features/videos/utils/video-tags';

export const useVideoTagSuggestions = (draft: string, enabled: boolean) => {
  const query = displayVideoTag(draft);
  const [debouncedQuery, setDebouncedQuery] = useState(query);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedQuery(query), 200);
    return () => window.clearTimeout(timer);
  }, [query]);
  const result = useVideoTags(debouncedQuery || undefined, enabled && query === debouncedQuery && query.length <= 100);
  return { ...result, suggestions: query === debouncedQuery ? result.data?.data ?? [] : [] };
};
