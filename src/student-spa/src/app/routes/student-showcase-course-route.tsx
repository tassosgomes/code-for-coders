import { Link } from 'react-router';

import { Button } from '@/components/ui/button';
import { paths } from '@/config/paths';
import { useDocumentTitle } from '@/hooks/use-document-title';

// The course page itself arrives with the next slice; the route exists so cards link by courseId (C-07).
export const StudentShowcaseCourseRoute = () => {
  useDocumentTitle('Curso | Code4Coders');

  return (
    <div className="flex max-w-xl flex-col items-start gap-4">
      <h1 className="typo-h2">A página do curso chega em breve</h1>
      <p className="text-muted-foreground">Enquanto isso, você pode ver os outros cursos.</p>
      <Button asChild variant="outline">
        <Link to={paths.studentShowcase.getHref()}>Ver todos os cursos</Link>
      </Button>
    </div>
  );
};
