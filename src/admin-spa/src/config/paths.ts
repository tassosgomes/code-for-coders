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
  catalog: { path: '/catalogo', getHref: () => '/catalogo' },
  catalogCourse: { path: '/catalogo/:courseId', getHref: (courseId: string) => `/catalogo/${courseId}` },
  courtesies: { path: '/cortesias', getHref: () => '/cortesias' },
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
  authoringVersion: { path: '/autoria/:courseId/versoes/:versionNumber', getHref: (courseId: string, versionNumber: number) => `/autoria/${courseId}/versoes/${versionNumber}` },
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
