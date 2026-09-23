export const paths = {
  home: {
    path: '/',
    getHref: () => '/',
  },
  studentRegistration: {
    path: '/cadastro',
    getHref: () => '/cadastro',
  },
  studentAccountConfirmation: {
    path: '/confirm-account',
    getHref: () => '/confirm-account',
  },
  studentLogin: {
    path: '/entrar',
    getHref: () => '/entrar',
  },
} as const;
