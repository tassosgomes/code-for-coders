import axios from 'axios';
import { useRef } from 'react';

import { useStudentAccountLookup, type StudentAccountLookupInput } from '@/features/courtesies/api/lookup-student-account';

export const useCourtesyStudentLookup = () => {
  const mutation = useStudentAccountLookup();
  const lastInput = useRef<StudentAccountLookupInput | null>(null);
  const code = axios.isAxiosError<{ code?: string }>(mutation.error) ? mutation.error.response?.data.code : undefined;
  const notFound = mutation.isError && code === 'STUDENT_ACCOUNT_NOT_FOUND';
  const retryable = mutation.isError && !notFound && code !== 'PERMISSION_DENIED' && code !== 'VALIDATION_ERROR';
  const error = !mutation.isError || notFound ? null
    : code === 'PERMISSION_DENIED' ? 'Você não tem acesso a esta área.'
      : code === 'VALIDATION_ERROR' ? 'Informe um e-mail válido.' : 'Não foi possível localizar agora.';
  const submit = async (input: StudentAccountLookupInput) => {
    lastInput.current = input;
    try { await mutation.mutateAsync(input); return true; }
    catch { return false; }
  };
  const retry = () => { if (lastInput.current) void submit(lastInput.current); };
  return { account: mutation.data, busy: mutation.isPending, error, notFound, retryable, retry, submit, reset: mutation.reset };
};
