import type { Course } from '@/features/course-authoring/types/course';

export const courseLevelLabel = (level: Course['level']) => level === 'beginner' ? 'Iniciante' : level === 'intermediate' ? 'Intermediário' : level === 'advanced' ? 'Avançado' : 'Sem nível';
