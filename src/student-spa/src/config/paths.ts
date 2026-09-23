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
  studentPasswordRecovery: {
    path: '/recuperar-senha',
    getHref: () => '/recuperar-senha',
  },
  studentPasswordReset: {
    path: '/redefinir-senha',
    getHref: () => '/redefinir-senha',
  },
} as const;
