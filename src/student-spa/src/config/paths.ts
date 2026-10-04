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
    getHref: (returnTo?: string) => returnTo ? `/entrar?returnTo=${encodeURIComponent(returnTo)}` : '/entrar',
  },
  studentPasswordRecovery: {
    path: '/recuperar-senha',
    getHref: () => '/recuperar-senha',
  },
  studentPasswordReset: {
    path: '/redefinir-senha',
    getHref: () => '/redefinir-senha',
  },
  studentShowcase: {
    path: '/cursos',
    getHref: (levelParam?: string) => (levelParam ? `/cursos?nivel=${encodeURIComponent(levelParam)}` : '/cursos'),
  },
  studentShowcaseCourse: {
    path: '/cursos/:courseId',
    getHref: (courseId: string) => `/cursos/${encodeURIComponent(courseId)}`,
  },
  studentLesson: {
    path: '/aulas/:lessonId',
    getHref: (lessonId: string) => `/aulas/${encodeURIComponent(lessonId)}`,
  },
  studentPasswordChange: {
    path: '/trocar-senha',
    getHref: () => '/trocar-senha',
  },
} as const;
