import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import {
  studentConfirmationEmailSchema,
  type StudentConfirmationEmailInput,
} from '@/features/student-confirmation/api/student-confirmation';

export const useStudentConfirmationRequestForm = () =>
  useForm<StudentConfirmationEmailInput>({
    defaultValues: { email: '' },
    resolver: zodResolver(studentConfirmationEmailSchema),
  });
