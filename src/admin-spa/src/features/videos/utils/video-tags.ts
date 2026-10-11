export const normalizeVideoTag = (value: string) => value.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase();
export const displayVideoTag = (value: string) => value.trim().replace(/\s+/gu, ' ');
export const distinctVideoTags = (values: readonly string[]) => {
  const tags = new Map<string, string>();
  for (const value of values) {
    const display = displayVideoTag(value);
    const key = normalizeVideoTag(display);
    if (!tags.has(key)) tags.set(key, display);
  }
  return [...tags.values()].sort((a, b) => normalizeVideoTag(a).localeCompare(normalizeVideoTag(b)));
};
export const sameVideoTags = (a: readonly string[], b: readonly string[]) =>
  JSON.stringify(distinctVideoTags(a).map(normalizeVideoTag)) === JSON.stringify(distinctVideoTags(b).map(normalizeVideoTag));
