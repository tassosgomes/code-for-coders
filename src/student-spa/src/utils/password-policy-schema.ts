import * as z from 'zod';

export const passwordPolicySchema = z.string()
  .min(8, 'Use oito ou mais caracteres, com maiúscula, minúscula, número e símbolo.')
  .refine((value) => /\p{Lu}/u.test(value), 'A senha precisa conter uma letra maiúscula.')
  .refine((value) => /\p{Ll}/u.test(value), 'A senha precisa conter uma letra minúscula.')
  .refine((value) => /\p{Nd}/u.test(value), 'A senha precisa conter um número.')
  .refine((value) => /[\p{P}\p{S}]/u.test(value), 'A senha precisa conter um símbolo.');
