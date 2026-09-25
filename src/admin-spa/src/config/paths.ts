export const paths = {
  home: {
    path: '/',
    getHref: () => '/',
  },
  staffLogin: {
    path: '/entrar',
    getHref: () => '/entrar',
  },
  staffPasswordReset: {
    path: '/redefinir-senha',
    getHref: () => '/redefinir-senha',
  },
} as const;
