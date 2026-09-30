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
  auditTrail: {
    path: '/auditoria',
    getHref: () => '/auditoria',
  },
  auditRecordDetail: {
    path: '/auditoria/:recordId',
    getHref: (recordId: string) => `/auditoria/${recordId}`,
  },
  authoring: { path: '/autoria', getHref: () => '/autoria' },
  authoringCourse: { path: '/autoria/:courseId', getHref: (courseId: string) => `/autoria/${courseId}` },
  videos: {
    path: '/videos',
    getHref: () => '/videos',
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
