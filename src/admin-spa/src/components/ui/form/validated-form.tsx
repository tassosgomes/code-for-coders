import { zodResolver } from '@hookform/resolvers/zod';
import type { ReactNode } from 'react';
import { useForm, type DefaultValues, type FieldValues, type UseFormReturn } from 'react-hook-form';
import type { z } from 'zod';

type ValidatedFormProps<T extends FieldValues> = {
  schema: z.ZodType<T, T>; defaultValues: DefaultValues<T>;
  onSubmit: (input: T) => Promise<boolean>; children: (form: UseFormReturn<T>) => ReactNode;
};
export const ValidatedForm = <T extends FieldValues,>({ schema, defaultValues, onSubmit, children }: ValidatedFormProps<T>) => {
  const form = useValidatedForm(schema, defaultValues);
  return <form className="text-details-form" noValidate onSubmit={form.handleSubmit(async (input) => {
    if (await onSubmit(input)) form.reset(input);
  })}>{children(form)}</form>;
};

export const useValidatedForm = <T extends FieldValues,>(schema: z.ZodType<T, T>, defaultValues: DefaultValues<T>) =>
  useForm<T>({ resolver: zodResolver(schema), defaultValues, mode: 'onChange' });
