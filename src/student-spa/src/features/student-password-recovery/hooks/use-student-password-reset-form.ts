import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import {
  studentPasswordResetFormSchema,
  type StudentPasswordResetFormInput,
} from '@/features/student-password-recovery/api/reset-student-password';

export const useStudentPasswordResetForm = () =>
  useForm<StudentPasswordResetFormInput>({
    defaultValues: { newPassword: '' },
    resolver: zodResolver(studentPasswordResetFormSchema),
  });
