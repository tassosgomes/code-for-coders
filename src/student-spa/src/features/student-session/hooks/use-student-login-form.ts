import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import {
  createStudentSessionSchema,
  type CreateStudentSessionInput,
} from '@/features/student-session/api/student-session';

export const useStudentLoginForm = () =>
  useForm<CreateStudentSessionInput>({
    resolver: zodResolver(createStudentSessionSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });
