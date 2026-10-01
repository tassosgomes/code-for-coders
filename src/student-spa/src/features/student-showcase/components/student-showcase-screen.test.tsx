import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import type { ShowcaseCoursePage } from '@/features/student-showcase/api/get-showcase-courses';
import { StudentShowcaseScreen } from '@/features/student-showcase/components/student-showcase-screen';
import { formatPriceCents } from '@/features/student-showcase/utils/format-price';

const csharp = {
  courseId: '3b4c5d6e-7f80-4a91-8b2c-4d5e6f7a8b9c',
  title: 'Fundamentos de C#',
  level: 'beginner',
  summary: 'Sintaxe, tipos e orientação a objetos em C#.',
  lowestPriceCents: 29700,
  offerCount: 1,
};
const api = {
  courseId: '6f1e2d3c-4b5a-4c69-8d7e-1f2a3b4c5d6e',
  title: '.NET do zero à API',
  level: 'advanced',
  summary: 'Da primeira linha de C# a uma API no ar.',
  lowestPriceCents: 39700,
  offerCount: 2,
};

const successPage = (data: ShowcaseCoursePage['data'], totalPages = 1): ShowcaseCoursePage => ({
  data,
  pagination: { page: 1, size: 12, total: data.length, totalPages },
});

const renderScreen = (props: Partial<Parameters<typeof StudentShowcaseScreen>[0]> = {}) => {
  const handlers = { onLevelChange: vi.fn(), onPageChange: vi.fn(), onRetry: vi.fn() };
  render(
    <MemoryRouter>
      <StudentShowcaseScreen
        level={undefined}
        page={1}
        result={{ status: 'success', page: successPage([api, csharp]) }}
        {...handlers}
        {...props}
      />
    </MemoryRouter>,
  );

  return handlers;
};

describe('student showcase screen', () => {
  it('shows each course as a card linked by course id with level, summary and price in text', () => {
    renderScreen();

    expect(screen.getByRole('heading', { level: 1, name: 'Escolha por onde começar' })).toBeInTheDocument();
    const csharpLink = screen.getByRole('link', { name: 'Fundamentos de C#' });
    expect(csharpLink).toHaveAttribute('href', `/cursos/${csharp.courseId}`);
    expect(screen.getByRole('link', { name: '.NET do zero à API' })).toHaveAttribute('href', `/cursos/${api.courseId}`);
    const list = screen.getByRole('list');
    expect(within(list).getByText('Iniciante')).toBeInTheDocument();
    expect(within(list).getByText('Avançado')).toBeInTheDocument();
    expect(screen.getByText('Sintaxe, tipos e orientação a objetos em C#.')).toBeInTheDocument();
    expect(screen.getByText('R$ 297,00')).toBeInTheDocument();
    expect(screen.getByText('a partir de R$ 397,00')).toBeInTheDocument();
    expect(screen.getByText('2 opções de acesso')).toBeInTheDocument();
    expect(screen.getAllByText(/opções de acesso/)).toHaveLength(1);
    expect(screen.getByRole('status')).toHaveTextContent('2 cursos');
  });

  it('exposes the level filter as a labelled group with the selected option announced', async () => {
    const user = userEvent.setup();
    const { onLevelChange } = renderScreen({ level: 'iniciante' });

    const group = screen.getByRole('radiogroup', { name: 'Nível' });
    expect(within(group).getAllByRole('radio').map((radio) => radio.getAttribute('aria-checked'))).toEqual([
      'false',
      'true',
      'false',
      'false',
    ]);
    expect(screen.getByRole('radio', { name: 'Iniciante' })).toBeChecked();

    await user.click(screen.getByRole('radio', { name: 'Avançado' }));
    expect(onLevelChange).toHaveBeenCalledWith('avancado');
    await user.click(screen.getByRole('radio', { name: 'Todos' }));
    expect(onLevelChange).toHaveBeenLastCalledWith(undefined);
  });

  it('offers "Todos" when a level has no course', async () => {
    const user = userEvent.setup();
    const { onLevelChange } = renderScreen({ level: 'avancado', result: { status: 'success', page: successPage([]) } });

    expect(screen.getByText('Nenhum curso neste nível por enquanto.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver todos os cursos' }));

    expect(onLevelChange).toHaveBeenCalledWith(undefined);
  });

  it('tells the visitor to come back when the school has no course at all', () => {
    renderScreen({ result: { status: 'success', page: successPage([]) } });

    expect(screen.getByText('Ainda não há cursos disponíveis.')).toBeInTheDocument();
    expect(screen.getByText('Volte em breve.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ver todos os cursos' })).not.toBeInTheDocument();
  });

  it('shows a loading state that keeps the filter available', () => {
    renderScreen({ result: { status: 'loading' } });

    expect(screen.getByRole('status', { name: 'Carregando cursos' })).toBeInTheDocument();
    expect(screen.getByRole('radiogroup', { name: 'Nível' })).toBeInTheDocument();
  });

  it('explains a load failure and lets the visitor retry', async () => {
    const user = userEvent.setup();
    const { onRetry } = renderScreen({ result: { status: 'error' } });

    expect(screen.getByRole('alert')).toHaveTextContent('Não foi possível carregar os cursos agora.');
    await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('paginates only when there is more than one page', async () => {
    const user = userEvent.setup();
    const { onPageChange } = renderScreen({ result: { status: 'success', page: successPage([api, csharp], 3) }, page: 2 });

    expect(screen.getByRole('button', { name: 'Página 2' })).toHaveAttribute('aria-current', 'page');
    await user.click(screen.getByRole('button', { name: 'Próxima' }));
    expect(onPageChange).toHaveBeenCalledWith(3);
  });

  it('formats prices from cents without floating point', () => {
    expect(formatPriceCents(29700)).toBe('R$ 297,00');
    expect(formatPriceCents(5)).toBe('R$ 0,05');
    expect(formatPriceCents(123456)).toBe('R$ 1.234,56');
    expect(formatPriceCents(9999999)).toBe('R$ 99.999,99');
  });
});
