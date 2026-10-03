import { formatTermDate } from '@/features/courtesies/utils/format-term-date';

type ReviewSentenceProps = { student: string; course: string; reason: string; endsOn?: string; lifetime: boolean };
export const ReviewSentence = ({ student, course, reason, endsOn, lifetime }: ReviewSentenceProps) => {
  const sentence = `Conceder a ${student} acesso a ${course}, ${lifetime ? 'vitalício' : endsOn ? `até ${formatTermDate(endsOn)}` : 'com término em cálculo'} — motivo: ${reason}.`;
  return <p className="courtesy-review-sentence" aria-label={sentence}>{sentence}</p>;
};
