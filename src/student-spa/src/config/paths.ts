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
} as const;
