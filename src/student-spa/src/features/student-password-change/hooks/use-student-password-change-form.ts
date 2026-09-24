import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import {
  studentPasswordChangeSchema,
  type StudentPasswordChangeInput,
} from '@/features/student-password-change/api/change-student-password';

export const useStudentPasswordChangeForm = () =>
  useForm<StudentPasswordChangeInput>({
    defaultValues: { currentPassword: '', newPassword: '' },
    resolver: zodResolver(studentPasswordChangeSchema),
  });
