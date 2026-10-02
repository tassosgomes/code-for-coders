import axios from 'axios';

import { useStudentAccountLookup, type StudentAccountLookupInput } from '@/features/courtesies/api/lookup-student-account';

export const useCourtesyStudentLookup = () => {
  const mutation = useStudentAccountLookup();
  const code = axios.isAxiosError<{ code?: string }>(mutation.error) ? mutation.error.response?.data.code : undefined;
  const error = mutation.isError ? code === 'STUDENT_ACCOUNT_NOT_FOUND' ? 'Não há conta de aluno com este e-mail'
    : code === 'PERMISSION_DENIED' ? 'Você não tem acesso a esta área.'
      : code === 'VALIDATION_ERROR' ? 'Informe um e-mail válido.' : 'Não foi possível localizar o aluno agora. Tente de novo.' : null;
  const submit = async (input: StudentAccountLookupInput) => {
    try { await mutation.mutateAsync(input); return true; }
    catch { return false; }
  };
  return { account: mutation.data, busy: mutation.isPending, error, submit, reset: mutation.reset };
};
