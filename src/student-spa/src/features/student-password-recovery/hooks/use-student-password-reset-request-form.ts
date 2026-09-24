import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import {
  studentPasswordResetRequestSchema,
  type StudentPasswordResetRequestInput,
} from '@/features/student-password-recovery/api/request-student-password-reset';

export const useStudentPasswordResetRequestForm = () =>
  useForm<StudentPasswordResetRequestInput>({
    defaultValues: { email: '' },
    resolver: zodResolver(studentPasswordResetRequestSchema),
  });
