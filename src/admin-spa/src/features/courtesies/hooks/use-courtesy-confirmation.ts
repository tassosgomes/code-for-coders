import { useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { useRef, useState } from 'react';

import { useValidatedForm } from '@/components/ui/form/validated-form';
import { courtesyGrantSchema, useGrantCourtesy } from '@/features/courtesies/api/grant-courtesy';
import { courtesyTermQueryOptions, useCourtesyTerm } from '@/features/courtesies/api/preview-courtesy-term';
import { useStudentAccessGrants } from '@/features/courtesies/api/list-student-access-grants';
import { existingAccessMessage } from '@/features/courtesies/utils/existing-access-message';

export type ResultError = 'ineligible' | 'idempotency' | 'unavailable';
const ineligibleCodes = ['STUDENT_ACCOUNT_NOT_ELIGIBLE', 'COURSE_NOT_ELIGIBLE'];
const unavailableCodes = ['STUDENT_ACCOUNT_CHECK_UNAVAILABLE', 'COMMERCE_UNAVAILABLE', 'UPSTREAM_TIMEOUT'];
const classify = (code: unknown, status: number | undefined): ResultError | null =>
  typeof code === 'string' && ineligibleCodes.includes(code) ? 'ineligible'
    : code === 'IDEMPOTENCY_KEY_REUSED' ? 'idempotency'
      : typeof code === 'string' && unavailableCodes.includes(code) || status === 502 || status === 504 ? 'unavailable' : null;
const monthsMessage = 'Informe de 1 a 60 meses inteiros.';
export const useCourtesyConfirmation = (studentId: string, courseId: string, step: number, setStep: (step: number) => void) => {
  const form = useValidatedForm(courtesyGrantSchema, { studentId, courseId, accessPeriod: { type: 'months', months: 6 }, reason: '' });
  const period = form.watch('accessPeriod');
  const months = period.type === 'months' ? period.months : 0;
  const monthsInvalid = period.type === 'months' && !(Number.isInteger(months) && months >= 1 && months <= 60);
  const preview = useCourtesyTerm(months, (step === 2 || step === 4) && period.type === 'months' && !monthsInvalid);
  const mutation = useGrantCourtesy();
  const queries = useQueryClient();
  const key = useRef<string | null>(null);
  const submitting = useRef(false);
  const [error, setError] = useState<{ text: string; retryable: boolean } | null>(null);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [resultError, setResultError] = useState<ResultError | null>(null);
  const [reviewing, setReviewing] = useState(false);
  const [lifetimeConfirmed, setLifetimeConfirmed] = useState(false);
  const grants = useStudentAccessGrants(studentId, step === 4);
  const existingAccess = existingAccessMessage(grants.data, courseId);
  const selectMonths = () => { form.setValue('accessPeriod', { type: 'months', months: 6 }); form.clearErrors('accessPeriod'); };
  const selectLifetime = () => { form.setValue('accessPeriod', { type: 'lifetime' }); form.clearErrors('accessPeriod'); };
  const review = async () => {
    if (!await form.trigger()) return;
    setReviewError(null); setError(null); setReviewing(true);
    try {
      if (period.type === 'months') await queries.fetchQuery(courtesyTermQueryOptions(period.months));
      key.current = crypto.randomUUID(); mutation.reset(); setResultError(null); setLifetimeConfirmed(false); setStep(4);
    } catch { setReviewError('Não foi possível calcular o término. Tente novamente.'); }
    finally { setReviewing(false); }
  };
  const backToReview = () => { key.current = crypto.randomUUID(); mutation.reset(); setResultError(null); setError(null); setLifetimeConfirmed(false); setStep(4); };
  const confirm = async () => {
    const retrying = step === 5 && resultError === 'unavailable';
    if (step !== 4 && !retrying || submitting.current || !key.current || period.type === 'lifetime' && !lifetimeConfirmed) return;
    const parsed = courtesyGrantSchema.safeParse({ ...form.getValues(), studentId, courseId });
    if (!parsed.success) { setError({ text: 'Revise a vigência e o motivo da cortesia.', retryable: false }); return; }
    submitting.current = true; setError(null);
    try { await mutation.mutateAsync({ body: parsed.data, key: key.current }); setResultError(null); setStep(5); }
    catch (failure) {
      const response = axios.isAxiosError<{ code?: unknown; detail?: string }>(failure) ? failure.response : undefined;
      const code: unknown = response?.data.code;
      const result = classify(code, response?.status);
      if (code === 'FIELD_INVALID') {
        const field = response?.data.detail?.split(' ')[0];
        if (field === 'accessPeriod') { form.setError('accessPeriod', { message: monthsMessage }); setStep(2); }
        else { form.setError('reason', { message: 'Informe um motivo de 1 a 500 caracteres.' }); setStep(3); }
      } else if (result) { setResultError(result); setStep(5); }
      else if (code === 'PERMISSION_DENIED') setError({ text: 'Você não tem permissão para conceder cortesias.', retryable: false });
      else setError({ text: 'Não foi possível conceder agora.', retryable: true });
    } finally { submitting.current = false; }
  };
  return { form, period, preview, mutation, error, reviewError, resultError, monthsInvalid, selectMonths, selectLifetime, backToReview, review, reviewing, confirm, lifetimeConfirmed, setLifetimeConfirmed, existingAccess, grants };
};
