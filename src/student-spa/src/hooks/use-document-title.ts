import { useEffect } from 'react';

export const useDocumentTitle = (title: string | undefined) => {
  useEffect(() => {
    if (title !== undefined) document.title = title;
  }, [title]);
};
