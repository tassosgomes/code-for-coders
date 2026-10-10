import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

type ReviewSentenceProps = { student: string; course: string; reason: string; endsOn?: string; lifetime: boolean };
const reasonInSentence = (reason: string) => {
  const trimmed = reason.trim().replace(/[.\s]+$/u, '');
  return trimmed.charAt(0).toLowerCase() + trimmed.slice(1);
};
export const ReviewSentence = ({ student, course, reason, endsOn, lifetime }: ReviewSentenceProps) => {
  const target = lifetime ? `acesso vitalício a ${course}` : `acesso a ${course} ${endsOn ? `até ${formatTermDate(endsOn)}` : 'com término em cálculo'}`;
  const sentence = `Conceder a ${student} ${target} — motivo: ${reasonInSentence(reason)}.`;
  return <p className="courtesy-review-sentence" aria-label={sentence}>{sentence}</p>;
};
