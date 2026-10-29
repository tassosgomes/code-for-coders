import { expect, test } from '@playwright/test';

import { studentUrl } from '../support/env';
import { LESSON_TITLE, readSeed } from '../support/seed';
import { newStudent, purchaseSeededCourse, registerConfirmedStudent } from '../support/student';

test.describe('Aluno: aula com player', () => {
  test('abre a aula e reproduz o vídeo protegido', async ({ page }) => {
    const seed = await readSeed();
    const student = newStudent('aula');
    await registerConfirmedStudent(student);
    await purchaseSeededCourse(page, student, seed.course);

    await page.goto(studentUrl(`/aulas/${seed.course.lessonId}`));
    await expect(page.getByRole('heading', { name: LESSON_TITLE })).toBeVisible();

    // O player abre a sessão de reprodução, baixa a playlist HLS cifrada pelo media-edge e carrega o vídeo.
    const player = page.getByRole('toolbar', { name: 'Player da aula' });
    await expect(player).toBeVisible();
    const video = player.locator('video');
    await expect.poll(() => video.evaluate((element: HTMLVideoElement) => element.duration), { timeout: 30_000 }).toBeGreaterThan(1);

    await page.getByRole('button', { name: 'Reproduzir' }).click();
    await expect.poll(() => video.evaluate((element: HTMLVideoElement) => element.currentTime), { timeout: 30_000 }).toBeGreaterThan(1);
    await expect(page.getByRole('button', { name: 'Pausar' })).toBeVisible();
    // A marca d'água identifica o aluno sobre a imagem.
    await expect(page.getByTestId('video-watermark')).toBeVisible();
  });
});
