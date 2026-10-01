export type ShowcaseLevelParam = 'iniciante' | 'intermediario' | 'avancado';

export type ShowcaseLevel = 'beginner' | 'intermediate' | 'advanced';

// The address speaks Portuguese (?nivel=iniciante); the contract speaks the enum. Translated only here.
const levelByParam: Record<ShowcaseLevelParam, ShowcaseLevel> = {
  iniciante: 'beginner',
  intermediario: 'intermediate',
  avancado: 'advanced',
};

const levelLabels: Record<ShowcaseLevel, string> = {
  beginner: 'Iniciante',
  intermediate: 'Intermediário',
  advanced: 'Avançado',
};

export const levelFilterOptions: ReadonlyArray<{ param: ShowcaseLevelParam | undefined; label: string }> = [
  { param: undefined, label: 'Todos' },
  { param: 'iniciante', label: levelLabels.beginner },
  { param: 'intermediario', label: levelLabels.intermediate },
  { param: 'avancado', label: levelLabels.advanced },
];

export const parseLevelParam = (value: string | null): ShowcaseLevelParam | undefined =>
  value !== null && Object.hasOwn(levelByParam, value) ? (value as ShowcaseLevelParam) : undefined;

export const toContractLevel = (param: ShowcaseLevelParam | undefined): ShowcaseLevel | undefined =>
  param === undefined ? undefined : levelByParam[param];

// Clients must tolerate a level the contract adds later: an unknown value simply has no badge.
export const getLevelLabel = (level: string): string | undefined =>
  Object.hasOwn(levelLabels, level) ? levelLabels[level as ShowcaseLevel] : undefined;
