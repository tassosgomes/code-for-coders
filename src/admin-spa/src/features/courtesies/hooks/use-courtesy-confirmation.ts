import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { useRef, useState } from 'react';

import { useValidatedForm } from '@/components/ui/form/validated-form';
import { courtesyGrantSchema, useGrantCourtesy } from '@/features/courtesies/api/grant-courtesy';
import { courtesyTermQueryOptions, useCourtesyTerm } from '@/features/courtesies/api/preview-courtesy-term';

const messages: Record<string, string> = {
  FIELD_INVALID: 'Revise a vigência e o motivo da cortesia.',
  IDEMPOTENCY_KEY_REUSED: 'Esta tentativa já foi usada com outros dados. Volte e revise para iniciar uma nova tentativa.',
  COURSE_NOT_ELIGIBLE: 'Este curso não está disponível para cortesia. Volte e escolha outro curso.',
  STUDENT_ACCOUNT_NOT_ELIGIBLE: 'A conta do aluno não está ativa. Volte e localize o aluno novamente.',
  STUDENT_ACCOUNT_CHECK_UNAVAILABLE: 'Não foi possível confirmar a conta do aluno. Tente novamente.',
  COMMERCE_UNAVAILABLE: 'Não foi possível conceder a cortesia agora. Tente novamente.',
  UPSTREAM_TIMEOUT: 'A confirmação demorou mais que o esperado. Tente novamente com os mesmos dados.',
  PERMISSION_DENIED: 'Você não tem permissão para conceder cortesias.',
};
export const useCourtesyConfirmation = (studentId: string, courseId: string, step: number, setStep: (step: number) => void) => {
  const form = useValidatedForm(courtesyGrantSchema, { studentId, courseId, accessPeriod: { type: 'months', months: 6 }, reason: '' });
  const period = form.watch('accessPeriod');
  const months = period.type === 'months' ? period.months : 0;
  const preview = useCourtesyTerm(months, (step === 2 || step === 4) && period.type === 'months' && Number.isInteger(months) && months >= 1 && months <= 60);
  const mutation = useGrantCourtesy();
  const queries = useQueryClient();
  const key = useRef<string | null>(null);
  const submitting = useRef(false);
  const [error, setError] = useState<string | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const review = async () => {
    if (!await form.trigger()) return;
    setError(null); setReviewing(true);
    try {
      if (period.type === 'months') await queries.fetchQuery(courtesyTermQueryOptions(period.months));
      key.current = crypto.randomUUID(); mutation.reset(); setStep(4);
    } catch { setError('Não foi possível calcular o término. Tente novamente.'); }
    finally { setReviewing(false); }
  };
  const confirm = async () => {
    if (submitting.current || !key.current) return;
    const parsed = courtesyGrantSchema.safeParse({ ...form.getValues(), studentId, courseId });
    if (!parsed.success) { setError(messages.FIELD_INVALID ?? null); return; }
    submitting.current = true; setError(null);
    try { await mutation.mutateAsync({ body: parsed.data, key: key.current }); setStep(5); }
    catch (failure) {
      const code: unknown = axios.isAxiosError<{ code?: unknown; detail?: string }>(failure) ? failure.response?.data.code : undefined;
      setError(typeof code === 'string' ? messages[code] ?? 'Não foi possível conceder a cortesia. Tente novamente.' : messages.COMMERCE_UNAVAILABLE ?? null);
      if (code === 'FIELD_INVALID') {
        const field = axios.isAxiosError<{ detail?: string }>(failure) ? failure.response?.data.detail?.split(' ')[0] : undefined;
        if (field === 'accessPeriod') { form.setError('accessPeriod', { message: 'Informe de 1 a 60 meses inteiros.' }); setStep(2); }
        else { form.setError('reason', { message: 'Informe um motivo de 1 a 500 caracteres.' }); setStep(3); }
      }
    } finally { submitting.current = false; }
  };
  return { form, period, preview, mutation, error, review, reviewing, confirm };
};
