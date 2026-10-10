// Junta a base da API (que pode ter prefixo, como /students) com um caminho absoluto da API.
// Não usar `new URL(path, base)`: com um caminho que começa com "/", ele descarta o prefixo da base.
export const joinApiUrl = (base: string, path: string) => `${base.replace(/\/+$/, '')}${path}`;
