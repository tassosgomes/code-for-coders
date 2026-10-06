import * as z from 'zod';

const storageKey = 'student-pending-purchase';
const lifetimeMs = 24 * 60 * 60 * 1000;
const pendingPurchaseSchema = z.object({ offerId: z.uuid(), courseId: z.uuid(), savedAt: z.number() }).strict();
export const savePendingPurchase = (offerId: string, courseId: string) => {
  const pending = pendingPurchaseSchema.safeParse({ offerId, courseId, savedAt: Date.now() });
  if (pending.success) window.localStorage.setItem(storageKey, JSON.stringify(pending.data));
};
export const clearPendingPurchase = () => window.localStorage.removeItem(storageKey);
export const getPendingPurchase = () => {
  const stored = window.localStorage.getItem(storageKey);
  if (!stored) return undefined;
  try {
    const result = pendingPurchaseSchema.safeParse(JSON.parse(stored));
    if (result.success && result.data.savedAt <= Date.now() && Date.now() - result.data.savedAt < lifetimeMs) return result.data;
  } catch { /* Invalid storage cannot prevent signing in. */ }
  clearPendingPurchase();
  return undefined;
};
