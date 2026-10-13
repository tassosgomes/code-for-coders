export type FinanceStudentLookup = {
  account?: { studentId: string; name: string; email: string };
  busy: boolean; error: string | null;
  /** F2.b: "não achado" vira erro do campo; "indisponível" vira alerta com Tentar de novo. */
  errorKind: 'not-found' | 'unavailable' | null;
  locate: (email: string) => Promise<boolean>; reset: () => void;
};
export type FinanceCourseOptions = {
  data: readonly { courseId: string; title: string }[];
  busy: boolean; unavailable: boolean;
};
