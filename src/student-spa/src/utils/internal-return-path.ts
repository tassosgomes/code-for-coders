// Redirects stay in the SPA; encoded separators, backslashes and control characters are rejected too.
export const internalReturnPath = (value: string | null): string | undefined => {
  if (!value) return undefined;
  try {
    const decoded = decodeURIComponent(value);
    if (!decoded.startsWith('/') || decoded.startsWith('//') || decoded.includes('\\') || [...decoded].some((character) => character.charCodeAt(0) <= 32)) return undefined;
    return value;
  } catch { return undefined; }
};
