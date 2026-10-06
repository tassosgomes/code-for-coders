export type FinanceStudentLookup = {
  account?: { studentId: string; name: string; email: string };
  busy: boolean; error: string | null;
  locate: (email: string) => Promise<boolean>; reset: () => void;
};
export type FinanceCourseOptions = {
  data: readonly { courseId: string; title: string }[]; search: string; setSearch: (value: string) => void;
  busy: boolean; unavailable: boolean; page: number; totalPages: number; changePage: (page: number) => void;
};
