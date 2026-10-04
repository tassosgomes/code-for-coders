import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { StudentAppLayoutRoute } from '@/app/routes/student-app-layout-route';
import { StudentLessonRoute } from '@/app/routes/student-lesson-route';
import { env } from '@/config/env';
import { paths } from '@/config/paths';
import { playbackData } from '@/testing/playback-data';
import { server } from '@/testing/server';
import { lessonId, secondLessonId, studentLessonData } from '@/testing/student-lesson-data';
import { renderWithProviders } from '@/testing/test-utils';

const hls = vi.hoisted(() => ({
  loadSource: vi.fn(),
  attachMedia: vi.fn(),
  destroy: vi.fn(),
  stopLoad: vi.fn(),
}));

vi.mock('hls.js', () => ({
  default: class {
    static isSupported = () => true;
    static Events = { ERROR: 'error' };
    attachMedia = hls.attachMedia;
    loadSource = hls.loadSource;
    destroy = hls.destroy;
    stopLoad = hls.stopLoad;
    on = vi.fn();
  },
}));

const lessonEndpoint = `${env.API_URL}/api/v1/lessons/:lessonId`;
const sessionEndpoint = `${env.API_URL}/api/v1/lessons/:lessonId/playback-sessions`;

const renderLesson = (initialLessonId = lessonId) =>
  renderWithProviders(
    <MemoryRouter initialEntries={[paths.studentLesson.getHref(initialLessonId)]}>
      <Routes>
        <Route element={<StudentAppLayoutRoute />}>
          <Route path={paths.studentLesson.path} element={<StudentLessonRoute />} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );

beforeEach(() => {
  window.sessionStorage.clear();
  window.localStorage.clear();
  hls.loadSource.mockClear();
  hls.attachMedia.mockClear();
  hls.destroy.mockClear();
  hls.stopLoad.mockClear();
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => undefined);
});

describe('Playback navigation and speed control', () => {
  it('navigates to another lesson and starts playback from the beginning with a new session', async () => {
    const user = userEvent.setup();
    const sessionRequests: string[] = [];

    server.use(
      http.get(lessonEndpoint, ({ params }) => {
        if (params.lessonId === secondLessonId) {
          return HttpResponse.json({
            ...studentLessonData,
            lesson: { ...studentLessonData.lesson, lessonId: secondLessonId, title: 'Persistência', position: 2 },
          });
        }
        return HttpResponse.json(studentLessonData);
      }),
      http.post(sessionEndpoint, ({ params }) => {
        sessionRequests.push(params.lessonId as string);
        const sid = params.lessonId === secondLessonId
          ? '00000000-0000-7000-8000-000000000005'
          : '00000000-0000-7000-8000-000000000004';
        return HttpResponse.json(
          { ...playbackData(), sessionId: sid, lessonId: params.lessonId },
          { status: 201 },
        );
      }),
    );

    renderLesson();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();
    await screen.findByTestId('video-watermark');
    expect(sessionRequests).toEqual([lessonId]);

    // Navigate to second lesson
    await user.click(screen.getByRole('link', { name: '2. Persistência' }));

    expect(await screen.findByRole('heading', { name: 'Persistência' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Persistência.*Aula atual/u })).toHaveAttribute('aria-current', 'page');
    expect(hls.destroy).toHaveBeenCalled();

    await waitFor(() => {
      expect(sessionRequests).toEqual([lessonId, secondLessonId]);
    });
  });

  it('allows jumping directly to lesson 5 without viewing preceding lessons (all open for entitled student)', async () => {
    const user = userEvent.setup();
    const lesson5Id = '00000000-0000-7000-8000-000000000015';

    const multiLessonCourse = {
      lesson: { lessonId, moduleId: '00000000-0000-7000-8000-000000000020', title: 'Aula 1', position: 1 },
      course: {
        courseId: '00000000-0000-7000-8000-000000000030',
        title: 'Curso Completo',
        versionNumber: 1,
        modules: [
          {
            moduleId: '00000000-0000-7000-8000-000000000020',
            title: 'Módulo Único',
            position: 1,
            lessons: [
              { lessonId, title: 'Aula 1', position: 1 },
              { lessonId: '00000000-0000-7000-8000-000000000012', title: 'Aula 2', position: 2 },
              { lessonId: '00000000-0000-7000-8000-000000000013', title: 'Aula 3', position: 3 },
              { lessonId: '00000000-0000-7000-8000-000000000014', title: 'Aula 4', position: 4 },
              { lessonId: lesson5Id, title: 'Aula 5', position: 5 },
            ],
          },
        ],
      },
    };

    server.use(
      http.get(lessonEndpoint, ({ params }) => {
        if (params.lessonId === lesson5Id) {
          return HttpResponse.json({
            ...multiLessonCourse,
            lesson: { lessonId: lesson5Id, moduleId: '00000000-0000-7000-8000-000000000020', title: 'Aula 5', position: 5 },
          });
        }
        return HttpResponse.json(multiLessonCourse);
      }),
    );

    renderLesson();
    expect(await screen.findByRole('heading', { name: 'Aula 1' })).toBeInTheDocument();

    const lesson5Link = screen.getByRole('link', { name: '5. Aula 5' });
    expect(lesson5Link).toBeInTheDocument();
    expect(lesson5Link).not.toHaveAttribute('aria-disabled', 'true');

    await user.click(lesson5Link);
    expect(await screen.findByRole('heading', { name: 'Aula 5' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Aula 5.*Aula atual/u })).toHaveAttribute('aria-current', 'page');
  });

  it('provides player speed control defaulting to 1x and offering 0.5x, 1x, 1.25x, 1.5x, 2x', async () => {
    const user = userEvent.setup();
    renderLesson();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();

    const speedBtn = await screen.findByRole('button', { name: /Velocidade/i });
    expect(speedBtn).toHaveTextContent('1x');

    await user.click(speedBtn);

    expect(screen.getByRole('menuitemradio', { name: '0,5x' })).toBeInTheDocument();
    expect(screen.getByRole('menuitemradio', { name: '1x' })).toBeInTheDocument();
    expect(screen.getByRole('menuitemradio', { name: '1,25x' })).toBeInTheDocument();
    expect(screen.getByRole('menuitemradio', { name: '1,5x' })).toBeInTheDocument();
    expect(screen.getByRole('menuitemradio', { name: '2x' })).toBeInTheDocument();
  });

  it('changes video playbackRate immediately upon selecting 1,5x speed', async () => {
    const user = userEvent.setup();
    const view = renderLesson();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();

    const video = view.container.querySelector('video') as HTMLVideoElement;
    expect(video).toBeInTheDocument();
    expect(video.playbackRate).toBe(1);

    const speedBtn = screen.getByRole('button', { name: /Velocidade/i });
    await user.click(speedBtn);

    const option15 = screen.getByRole('menuitemradio', { name: '1,5x' });
    await user.click(option15);

    expect(video.playbackRate).toBe(1.5);
    expect(screen.getByRole('button', { name: /Velocidade/i })).toHaveTextContent('1,5x');
  });

  it('resets speed to 1x when switching to another lesson', async () => {
    const user = userEvent.setup();

    server.use(
      http.get(lessonEndpoint, ({ params }) => {
        if (params.lessonId === secondLessonId) {
          return HttpResponse.json({
            ...studentLessonData,
            lesson: { ...studentLessonData.lesson, lessonId: secondLessonId, title: 'Persistência', position: 2 },
          });
        }
        return HttpResponse.json(studentLessonData);
      }),
    );

    const view = renderLesson();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();

    const speedBtn = screen.getByRole('button', { name: /Velocidade/i });
    await user.click(speedBtn);
    await user.click(screen.getByRole('menuitemradio', { name: '1,5x' }));

    const video1 = view.container.querySelector('video') as HTMLVideoElement;
    expect(video1.playbackRate).toBe(1.5);
    expect(screen.getByRole('button', { name: /Velocidade/i })).toHaveTextContent('1,5x');

    // Switch to lesson 2
    await user.click(screen.getByRole('link', { name: '2. Persistência' }));
    expect(await screen.findByRole('heading', { name: 'Persistência' })).toBeInTheDocument();

    const video2 = view.container.querySelector('video') as HTMLVideoElement;
    expect(video2.playbackRate).toBe(1);
    expect(screen.getByRole('button', { name: /Velocidade/i })).toHaveTextContent('1x');
  });

  it('shows unavailable lesson alert with current course list when lesson was removed in republication', async () => {
    const user = userEvent.setup();

    const republishedCourseData = {
      lesson: {
        ...studentLessonData.lesson,
        title: 'Injeção de dependência na prática',
      },
      course: {
        ...studentLessonData.course,
        versionNumber: 2,
        modules: [
          {
            moduleId: studentLessonData.lesson.moduleId,
            title: 'Módulo Único Revisado',
            position: 1,
            lessons: [
              {
                lessonId,
                title: 'Injeção de dependência na prática',
                position: 1,
              },
              {
                lessonId: '00000000-0000-7000-8000-000000000099',
                title: 'Arquitetura e Inversão de Controle',
                position: 2,
              },
            ],
          },
        ],
      },
    };

    let secondLessonRequested = false;
    server.use(
      http.get(lessonEndpoint, ({ params }) => {
        if (params.lessonId === secondLessonId) {
          secondLessonRequested = true;
          return HttpResponse.json({ code: 'LESSON_NOT_AVAILABLE' }, { status: 404 });
        }
        if (secondLessonRequested && params.lessonId === lessonId) {
          return HttpResponse.json(republishedCourseData);
        }
        return HttpResponse.json(studentLessonData);
      }),
    );

    renderLesson();
    expect(await screen.findByRole('heading', { name: 'Injeção de dependência' })).toBeInTheDocument();

    // Click on second lesson (which is removed in the new version)
    await user.click(screen.getByRole('link', { name: '2. Persistência' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Esta aula não está disponível.');

    // The curriculum of the republished course is displayed and available
    expect(await screen.findByRole('navigation', { name: 'Aulas do curso' })).toBeInTheDocument();
    expect(screen.getByText(/Módulo Único Revisado/u)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '1. Injeção de dependência na prática' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '2. Arquitetura e Inversão de Controle' })).toBeInTheDocument();

    // The removed lesson is NOT in the list!
    expect(screen.queryByRole('link', { name: /Persistência/u })).not.toBeInTheDocument();
    // No lesson is highlighted as current
    expect(screen.queryByRole('link', { name: /Aula atual/u })).not.toBeInTheDocument();
  });

  it('displays the current course list after republication even without location.state (direct link or refresh)', async () => {
    const republishedCourseData = {
      lesson: {
        ...studentLessonData.lesson,
        title: 'Injeção de dependência na prática',
      },
      course: {
        ...studentLessonData.course,
        versionNumber: 2,
        modules: [
          {
            moduleId: studentLessonData.lesson.moduleId,
            title: 'Módulo Único Revisado',
            position: 1,
            lessons: [
              {
                lessonId,
                title: 'Injeção de dependência na prática',
                position: 1,
              },
            ],
          },
        ],
      },
    };

    server.use(
      http.get(lessonEndpoint, ({ params }) => {
        if (params.lessonId === secondLessonId) {
          return HttpResponse.json({ code: 'LESSON_NOT_AVAILABLE' }, { status: 404 });
        }
        return HttpResponse.json(republishedCourseData);
      }),
    );

    // First visit lesson 1 so the client records the course structure
    const { unmount } = renderLesson(lessonId);
    expect(await screen.findByRole('heading', { name: /Injeção de dependência/i })).toBeInTheDocument();
    unmount();

    // Now open the removed lesson directly without location.state (simulating direct link or refresh)
    renderLesson(secondLessonId);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Esta aula não está disponível.');

    // The current version curriculum is displayed without the removed lesson
    expect(await screen.findByRole('navigation', { name: 'Aulas do curso' })).toBeInTheDocument();
    expect(screen.getByText(/Módulo Único Revisado/u)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '1. Injeção de dependência na prática' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Persistência/u })).not.toBeInTheDocument();
  });

  it('shows generic unavailable alert without course nav when lesson is unknown and has no course history', async () => {
    server.use(
      http.get(lessonEndpoint, () => HttpResponse.json({ code: 'LESSON_NOT_AVAILABLE' }, { status: 404 })),
    );

    renderLesson('00000000-0000-7000-8000-999999999999');

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Esta aula não está disponível.');
    expect(screen.getByRole('link', { name: 'Ver cursos' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Aulas do curso' })).not.toBeInTheDocument();
  });
});
