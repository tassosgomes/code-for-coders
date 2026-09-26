export const paths = {
  home: {
    path: '/',
    getHref: () => '/',
  },
  staffLogin: {
    path: '/entrar',
    getHref: () => '/entrar',
  },
  staffAccess: {
    path: '/acessos',
    getHref: () => '/acessos',
  },
  staffFinance: {
    path: '/financeiro',
    getHref: () => '/financeiro',
  },
  staffPasswordReset: {
    path: '/redefinir-senha',
    getHref: () => '/redefinir-senha',
  },
  staffPasswordRecovery: {
    path: '/recuperar-senha',
    getHref: () => '/recuperar-senha',
  },
  staffInvitation: {
    path: '/convite',
    getHref: () => '/convite',
  },
} as const;
