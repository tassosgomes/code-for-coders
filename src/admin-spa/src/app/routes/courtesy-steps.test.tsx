import { cleanup, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';

import { env } from '@/config/env';
import { lookupCourtesyStudent, renderCourtesy } from '@/testing/courtesy-route-test-helpers';
import { server } from '@/testing/server';

const openTerm = async () => {
  const user = await lookupCourtesyStudent();
  await screen.findByRole('heading', { name: 'Joana Ribeiro' });
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await user.click(await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' }));
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await screen.findByText('Termina em até 02/04/2027 — vindo do servidor.');
  return user;
};
const toReview = async (reason = 'Bolsa de mentoria.', lifetime = false) => {
  const user = await openTerm();
  if (lifetime) await user.click(screen.getByRole('radio', { name: 'Vitalícia' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled());
  await user.click(screen.getByRole('button', { name: 'Continuar' }));
  await user.type(screen.getByRole('textbox', { name: 'Motivo da cortesia' }), reason);
  await user.click(screen.getByRole('button', { name: 'Revisar' }));
  await screen.findByRole('heading', { name: 'Revisão' });
  return user;
};
const grantResponse = (extra = {}) => HttpResponse.json({ grantId: crypto.randomUUID(), courseTitle: 'Fundamentos de C#', accessPeriod: { type: 'months', months: 6 }, endsOn: '2027-04-02', reason: 'Bolsa de mentoria.', ...extra }, { status: 201 });
const failGrant = (code: string, status: number) => server.use(http.post(`${env.API_URL}/api/v1/courtesy-grants`, () => HttpResponse.json({ code }, { status })));

describe('courtesy vigência, motivo and revisão', () => {
  afterEach(cleanup);

  it('clears the months error when switching the radio and shows only the field error without a preview box (B1, B2)', async () => {
    renderCourtesy(); const user = await openTerm(); const months = screen.getByRole('spinbutton', { name: 'Meses' });
    await user.clear(months); await user.type(months, '61');
    expect(await screen.findByRole('alert')).toHaveTextContent('Informe de 1 a 60 meses inteiros.');
    expect(screen.getAllByText('Informe de 1 a 60 meses inteiros.')).toHaveLength(1);
    expect(screen.queryByText(/Termina em até/)).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Continuar' })).toBeDisabled();
    await user.click(screen.getByRole('radio', { name: 'Vitalícia' }));
    expect(screen.queryByText('Informe de 1 a 60 meses inteiros.')).not.toBeInTheDocument();
    expect(screen.getByText('Acesso vitalício, sem data de término.')).toBeInTheDocument();
    await user.click(screen.getByRole('radio', { name: 'Por período' }));
    expect(screen.getByRole('spinbutton', { name: 'Meses' })).toHaveValue(6); expect(screen.queryByText('Informe de 1 a 60 meses inteiros.')).not.toBeInTheDocument();
    expect(await screen.findByText('Termina em até 02/04/2027 — vindo do servidor.')).toBeInTheDocument();
  });

  it('shows the course summary, hides Continuar when the preview fails and retries', async () => {
    renderCourtesy(); let fail = true;
    server.use(http.get(`${env.API_URL}/api/v1/courtesy-term-preview`, () => fail ? HttpResponse.json({ code: 'X' }, { status: 502 }) : HttpResponse.json({ months: 6, computedAt: '2026-10-02T12:00:00Z', endsOn: '2027-04-02', expiresAt: '2027-04-03T03:00:00Z' })));
    const user = await lookupCourtesyStudent(); await screen.findByRole('heading', { name: 'Joana Ribeiro' }); await user.click(screen.getByRole('button', { name: 'Continuar' }));
    await user.click(await screen.findByRole('button', { name: 'Escolher Fundamentos de C#' })); await user.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(screen.getByLabelText('Resumo Curso')).toHaveTextContent('Fundamentos de C#');
    expect(await screen.findByText('Não foi possível mostrar a prévia agora. Tente de novo antes de continuar.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Continuar' })).not.toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Voltar' })).toBeInTheDocument();
    fail = false; await user.click(screen.getByRole('button', { name: 'Tentar de novo' }));
    expect(await screen.findByText('Termina em até 02/04/2027 — vindo do servidor.')).toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled();
  });

  it('writes the review sentence without a duplicated period, comma or capital letter (B3)', async () => {
    renderCourtesy(); await toReview('Bolsa de mentoria.');
    expect(screen.getByLabelText('Conceder a joana@student.test acesso a Fundamentos de C# até 02/04/2027 — motivo: bolsa de mentoria.')).toBeInTheDocument();
  });

  it('keeps an acronym at the start of the reason in the review sentence', async () => {
    renderCourtesy(); await toReview('QA: validação da tela');
    expect(screen.getByLabelText('Conceder a joana@student.test acesso a Fundamentos de C# até 02/04/2027 — motivo: QA: validação da tela.')).toBeInTheDocument();
  });

  it('writes the lifetime sentence and labels the lifetime confirm button', async () => {
    renderCourtesy(); await toReview('Parceria', true);
    expect(screen.getByLabelText('Conceder a joana@student.test acesso vitalício a Fundamentos de C# — motivo: parceria.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Confirmar cortesia vitalícia' })).toBeDisabled();
  });

  it('lets the review table jump back to each step', async () => {
    renderCourtesy(); const user = await toReview(); const review = screen.getByRole('region', { name: 'Passo Revisão' });
    expect(review).toHaveTextContent('Joana Ribeiro (joana@student.test)'); expect(review).toHaveTextContent('6 meses · até 02/04/2027'); expect(review).toHaveTextContent('Bolsa de mentoria.');
    expect(review).not.toHaveTextContent('A cortesia será registrada na trilha de auditoria.');
    await user.click(screen.getByRole('button', { name: 'Editar motivo' })); expect(await screen.findByRole('region', { name: 'Passo Motivo' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Revisar' })); await screen.findByRole('heading', { name: 'Revisão' });
    await user.click(screen.getByRole('button', { name: 'Editar vigência' })); expect(await screen.findByRole('region', { name: 'Passo Vigência' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Continuar' }));
    await user.click(screen.getByRole('button', { name: 'Revisar' })); await screen.findByRole('heading', { name: 'Revisão' });
    await user.click(screen.getByRole('button', { name: 'Editar curso' })); expect(await screen.findByRole('region', { name: 'Passo Curso' })).toBeInTheDocument();
  });

  it('shows the success result with the toast and returns to the student step from "Ver concessões do aluno"', async () => {
    renderCourtesy(); server.use(http.post(`${env.API_URL}/api/v1/courtesy-grants`, () => grantResponse()));
    const user = await toReview(); await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    const result = await screen.findByRole('region', { name: 'Resultado da cortesia' });
    expect(result).toHaveTextContent('Cortesia concedida.'); expect(result).toHaveTextContent('6 meses · até 02/04/2027'); expect(result).toHaveTextContent('Motivo: Bolsa de mentoria.'); expect(result).not.toHaveTextContent('auditoria');
    expect(screen.getByRole('status')).toHaveTextContent('Cortesia concedida.');
    await user.click(screen.getByRole('button', { name: 'Ver concessões do aluno' })); expect(await screen.findByRole('region', { name: 'Passo Aluno' })).toBeVisible();
    expect(screen.getByRole('heading', { name: 'Joana Ribeiro' })).toBeInTheDocument();
  });

  it.each(['STUDENT_ACCOUNT_NOT_ELIGIBLE', 'COURSE_NOT_ELIGIBLE'])('shows the not-eligible result for %s with links back', async (code) => {
    renderCourtesy(); const user = await toReview(); failGrant(code, 422);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    expect(await screen.findByText('Não foi possível conceder: verifique o aluno e o curso.')).toBeInTheDocument(); expect(screen.queryByRole('heading', { name: 'Revisão' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Voltar ao Curso' })); expect(await screen.findByRole('region', { name: 'Passo Curso' })).toBeInTheDocument();
  });

  it('offers a new idempotency key from the reused-key result', async () => {
    renderCourtesy(); const keys: (string | null)[] = []; let calls = 0;
    server.use(http.post(`${env.API_URL}/api/v1/courtesy-grants`, ({ request }) => { keys.push(request.headers.get('Idempotency-Key')); calls++; return calls === 1 ? HttpResponse.json({ code: 'IDEMPOTENCY_KEY_REUSED' }, { status: 409 }) : grantResponse(); }));
    const user = await toReview(); await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    expect(await screen.findByText('Esta confirmação já foi usada de outro jeito. Revise e confirme de novo.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Voltar à revisão' })); await screen.findByRole('heading', { name: 'Revisão' });
    await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' })); await screen.findByText('Cortesia concedida.', { selector: 'p.courtesy-result-title' });
    expect(keys).toHaveLength(2); expect(keys[0]).not.toBe(keys[1]);
  });

  it.each([['COMMERCE_UNAVAILABLE', 502], ['UPSTREAM_TIMEOUT', 504]])('shows the unavailable result for %s', async (code, status) => {
    renderCourtesy(); const user = await toReview(); failGrant(code as string, status as number);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Confirmar cortesia' })).toBeEnabled()); await user.click(screen.getByRole('button', { name: 'Confirmar cortesia' }));
    expect(await screen.findByText('Não foi possível conceder agora.')).toBeInTheDocument(); expect(screen.getByRole('button', { name: 'Tentar de novo' })).toBeInTheDocument();
  });

  it('warns about existing lifetime access in the review', async () => {
    renderCourtesy();
    server.use(http.get(`${env.API_URL}/api/v1/students/:studentId/access-grants`, () => HttpResponse.json({ data: [{ grantId: crypto.randomUUID(), courseId: '0198dfac-674a-7000-8000-000000000088', courseTitle: 'Fundamentos de C#', origin: 'courtesy', status: 'active', accessPeriod: { type: 'lifetime' }, grantedAt: '2026-10-02T12:00:00Z', endsOn: null, expiresAt: null, reason: 'x' }], pagination: { page: 1, size: 10, total: 1, totalPages: 1 } })));
    await toReview(); expect(await screen.findByText('Este aluno já tem acesso vitalício a este curso.')).toBeInTheDocument();
    expect(screen.getByText('A nova cortesia não estende nem encerra a outra; as duas passam a existir.')).toBeInTheDocument();
  });
});
