import type { CourtesyCourse } from '@/features/courtesies/api/list-courtesy-courses';

type CoursePickRowProps = { course: CourtesyCourse; selected: boolean; onSelect: (course: CourtesyCourse) => void };
export const CoursePickRow = ({ course, selected, onSelect }: CoursePickRowProps) => <li className={`courtesy-course-row ${selected ? 'is-selected' : ''}`}>
  <span>{course.title}</span>
  <button type="button" className={selected ? 'primary-button' : 'secondary-button'} aria-pressed={selected} aria-label={`${selected ? 'Escolhido' : 'Escolher'} ${course.title}`} onClick={() => onSelect(course)}>{selected ? 'Escolhido ✓' : 'Escolher'}</button>
</li>;
