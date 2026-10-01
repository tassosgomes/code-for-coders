import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { renderWithProviders } from '@/testing/test-utils';

import type { ShowcaseCourseDetail } from '@/features/student-showcase/api/get-showcase-course';
import { StudentCoursePage } from '@/features/student-showcase/components/student-course-page';
import { formatAccessPeriod } from '@/features/student-showcase/utils/access-period';

const course: ShowcaseCourseDetail = {
  courseId: '6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e',
  title: '.NET do zero à API',
  level: 'advanced',
  description: 'Do primeiro programa a uma API publicada, com testes e deploy.',
  prerequisite: {
    text: 'Git e C# básico.',
    recommendedCourses: [
      { courseId: '3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c', title: 'Fundamentos de C#', inShowcase: true },
      { courseId: '9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d', title: 'Introdução a APIs', inShowcase: false },
    ],
  },
  modules: [
    { title: 'Fundamentos da linguagem', lessons: [{ title: 'Tipos e variáveis' }, { title: 'Controle de fluxo' }] },
    { title: 'Construindo a API', lessons: [{ title: 'Rotas' }] },
  ],
  offers: [
    { offerId: '7c8d9e0f-1a2b-4c3d-8e4f-5a6b7c8d9e0f', name: 'Acesso por 12 meses', priceCents: 39700, accessPeriod: { type: 'months', months: 12 } },
    { offerId: '8d9e0f1a-2b3c-4d4e-9f5a-6b7c8d9e0f1a', name: 'Acesso vitalício', priceCents: 89700, accessPeriod: { type: 'lifetime' } },
  ],
};

const renderPage = (result: Parameters<typeof StudentCoursePage>[0]['result'], onRetry = vi.fn()) => {
  const view = renderWithProviders(
    <MemoryRouter>
      <StudentCoursePage onRetry={onRetry} result={result} />
    </MemoryRouter>,
  );

  return { ...view, onRetry };
};

describe('student course page', () => {
  it('shows level, title, description and the section headings a screen reader can navigate by', () => {
    renderPage({ status: 'success', course });

    expect(screen.getByRole('heading', { level: 1, name: '.NET do zero à API' })).toBeInTheDocument();
    expect(screen.getByText('Avançado')).toBeInTheDocument();
    expect(
      screen.getAllByRole('heading', { level: 2 }).map((heading) => heading.textContent),
    ).toEqual(['Sobre o curso', 'Recomendamos saber antes', 'O que você vai ver', 'Opções de acesso']);
    expect(screen.getByText('Do primeiro programa a uma API publicada, com testes e deploy.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Todos os cursos' })).toHaveAttribute('href', '/cursos');
  });

  it('links a recommended course only when it is in the showcase and never as a condition to buy', () => {
    renderPage({ status: 'success', course });

    const section = screen.getByRole('region', { name: 'Recomendamos saber antes' });
    expect(within(section).getByText('Git e C# básico.')).toBeInTheDocument();
    expect(within(section).getByRole('link', { name: 'Fundamentos de C#' })).toHaveAttribute(
      'href',
      '/cursos/3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c',
    );
    expect(within(section).getByText('Introdução a APIs')).toBeInTheDocument();
    expect(within(section).queryByRole('link', { name: 'Introdução a APIs' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Comprar/ })).toHaveLength(2);
    screen.getAllByRole('button', { name: /^Comprar/ }).forEach((button) => expect(button).toBeEnabled());
  });

  it('shows one purchase option per offer with the period and the price in text, cheapest first as sent', () => {
    renderPage({ status: 'success', course });

    const options = screen.getByRole('region', { name: 'Opções de acesso' });
    const items = within(options).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]!).getByRole('heading', { level: 3, name: 'Acesso por 12 meses' })).toBeInTheDocument();
    expect(within(items[0]!).getByText('Acesso por 12 meses, contados a partir da liberação')).toBeInTheDocument();
    expect(within(items[0]!).getByText('R$ 397,00')).toBeInTheDocument();
    expect(within(items[1]!).getByText('Acesso vitalício', { selector: 'p' })).toBeInTheDocument();
    expect(within(items[1]!).getByText('R$ 897,00')).toBeInTheDocument();
  });

  it.each([
    [{ type: 'months', months: 12 }, 'Acesso por 12 meses, contados a partir da liberação'],
    [{ type: 'months', months: 1 }, 'Acesso por 1 mês, contado a partir da liberação'],
    [{ type: 'months', months: 60 }, 'Acesso por 60 meses, contados a partir da liberação'],
    [{ type: 'lifetime' }, 'Acesso vitalício'],
  ])('states the period %j as "%s"', (period, expected) => {
    expect(formatAccessPeriod(period)).toBe(expected);
  });

  it('shows the modules with their lesson titles and opens the first one', () => {
    renderPage({ status: 'success', course });

    expect(screen.getByRole('button', { name: /Fundamentos da linguagem \(2 aulas\)/ })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Tipos e variáveis')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Construindo a API \(1 aula\)/ })).toHaveAttribute('aria-expanded', 'false');
  });

  it('hides the sections that have nothing to say', () => {
    renderPage({
      status: 'success',
      course: { ...course, description: '  ', prerequisite: { text: null, recommendedCourses: [] } },
    });

    expect(screen.queryByRole('heading', { name: 'Sobre o curso' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Recomendamos saber antes' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'O que você vai ver' })).toBeInTheDocument();
  });

  it('renders text coming from the server as text, never as markup', () => {
    const markup = '<img src=x onerror="window.pwned=true"><b>negrito</b>';
    const { container } = renderPage({
      status: 'success',
      course: {
        ...course,
        title: markup,
        description: markup,
        offers: [{ ...course.offers[0]!, name: markup }],
      },
    });

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('b')).toBeNull();
    expect(screen.getAllByText(markup, { exact: false }).length).toBeGreaterThanOrEqual(3);
  });

  it('never writes a promise of exclusivity or copy protection in any text the platform generates', () => {
    const neutral: ShowcaseCourseDetail = {
      ...course,
      title: 'Curso',
      description: 'Texto.',
      prerequisite: { text: 'Texto.', recommendedCourses: [{ ...course.prerequisite.recommendedCourses[0]!, title: 'Outro' }] },
      modules: [{ title: 'Módulo', lessons: [{ title: 'Aula' }] }],
      offers: [{ ...course.offers[0]!, name: 'Opção' }],
    };
    const states = [
      { status: 'success', course: neutral },
      { status: 'loading' },
      { status: 'error' },
      { status: 'not-found' },
    ] as const;

    for (const state of states) {
      const { container, unmount } = renderPage(state);
      expect(container.textContent ?? '').not.toMatch(/exclusiv|proteg/i);
      expect(container.innerHTML).not.toMatch(/exclusiv|proteg/i);
      unmount();
    }
  });

  it('says the course is not available, with a way back to the showcase, and nothing about why', () => {
    renderPage({ status: 'not-found' });

    expect(screen.getByRole('heading', { level: 1, name: 'Este curso não está disponível.' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver todos os cursos' })).toHaveAttribute('href', '/cursos');
  });

  it('explains a load failure and offers to try again', async () => {
    const user = userEvent.setup();
    const { onRetry } = renderPage({ status: 'error' });

    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar este curso agora.');
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
