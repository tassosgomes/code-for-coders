import { useEffect, useState } from 'react';

export const useOrderReturnDelay = (following: boolean) => {
  const [delayed, setDelayed] = useState(false);
  useEffect(() => {
    if (!following) return;
    const timer = window.setTimeout(() => setDelayed(true), 120_000);
    return () => window.clearTimeout(timer);
  }, [following]);
  return delayed;
};
