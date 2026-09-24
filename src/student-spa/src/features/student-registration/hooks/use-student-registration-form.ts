import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import {
  registerStudentSchema,
  type RegisterStudentInput,
} from '@/features/student-registration/api/register-student';

export const useStudentRegistrationForm = () =>
  useForm<RegisterStudentInput>({
    defaultValues: { name: '', email: '', password: '' },
    resolver: zodResolver(registerStudentSchema),
  });
