import * as z from 'zod';

export const passwordPolicyRequirements = [
  {
    id: 'minimum-length',
    label: 'Pelo menos 8 caracteres',
    message: 'Use oito ou mais caracteres, com maiúscula, minúscula, número e símbolo.',
    isSatisfied: (value: string) => value.length >= 8,
  },
  {
    id: 'uppercase',
    label: 'Uma letra maiúscula',
    message: 'A senha precisa conter uma letra maiúscula.',
    isSatisfied: (value: string) => /\p{Lu}/u.test(value),
  },
  {
    id: 'lowercase',
    label: 'Uma letra minúscula',
    message: 'A senha precisa conter uma letra minúscula.',
    isSatisfied: (value: string) => /\p{Ll}/u.test(value),
  },
  {
    id: 'number',
    label: 'Um número',
    message: 'A senha precisa conter um número.',
    isSatisfied: (value: string) => /\p{Nd}/u.test(value),
  },
  {
    id: 'symbol',
    label: 'Um símbolo',
    message: 'A senha precisa conter um símbolo.',
    isSatisfied: (value: string) => /[\p{P}\p{S}]/u.test(value),
  },
] as const;

export const passwordPolicySchema = z.string().superRefine((value, context) => {
  for (const requirement of passwordPolicyRequirements) {
    if (!requirement.isSatisfied(value)) {
      context.addIssue({ code: 'custom', message: requirement.message });
    }
  }
});
