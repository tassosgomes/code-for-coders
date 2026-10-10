import { Check } from 'lucide-react';

import type { CourtesyCourse } from '@/features/courtesies/api/list-courtesy-courses';

type CoursePickRowProps = { course: CourtesyCourse; selected: boolean; onSelect: (course: CourtesyCourse) => void };
export const CoursePickRow = ({ course, selected, onSelect }: CoursePickRowProps) => <li className={`courtesy-course-row ${selected ? 'is-selected' : ''}`}>
  <span className="courtesy-course-title">{course.title}</span>
  <span className={`courtesy-offer-pill ${course.hasOffer ? 'has-offer' : ''}`}>{course.hasOffer ? 'Com oferta' : 'Sem oferta'}</span>
  <button type="button" className={selected ? 'courtesy-chosen' : 'secondary-button courtesy-pick-button'} aria-pressed={selected} aria-label={`${selected ? 'Escolhido' : 'Escolher'} ${course.title}`} onClick={() => onSelect(course)}>
    {selected ? <><Check size={14} aria-hidden="true" />Escolhido</> : 'Escolher'}
  </button>
</li>;
