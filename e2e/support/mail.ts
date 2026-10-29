import { env } from './env';

type MessageSummary = { id: string; to: string[]; subject: string; receivedDate: string };

const authHeaders = (): Record<string, string> =>
  env.smtpUser ? { authorization: `Basic ${Buffer.from(`${env.smtpUser}:${env.smtpPassword}`).toString('base64')}` } : {};

const getJson = async <T>(path: string): Promise<T> => {
  const response = await fetch(`${env.smtpUrl}${path}`, { headers: authHeaders() });
  if (!response.ok) throw new Error(`smtp4dev ${path} respondeu ${response.status}`);
  return (await response.json()) as T;
};

const getText = async (path: string): Promise<string> => {
  const response = await fetch(`${env.smtpUrl}${path}`, { headers: authHeaders() });
  if (!response.ok) throw new Error(`smtp4dev ${path} respondeu ${response.status}`);
  return response.text();
};

const sleep = (milliseconds: number) => new Promise((resolve) => setTimeout(resolve, milliseconds));

export type MailQuery = {
  to: string;
  subjectIncludes: string;
  /** Ignora mensagens recebidas antes deste instante (evita ler e-mail antigo do mesmo destinatário). */
  since?: Date;
  timeoutMs?: number;
};

/** Espera o e-mail chegar ao smtp4dev e devolve o corpo (HTML decodificado + texto). */
export const waitForMail = async ({ to, subjectIncludes, since, timeoutMs = 60_000 }: MailQuery): Promise<string> => {
  const deadline = Date.now() + timeoutMs;
  const recipient = to.toLowerCase();
  let lastError: unknown;
  while (Date.now() < deadline) {
    try {
      const page = await getJson<{ results: MessageSummary[] }>('/api/messages?pageSize=100');
      const message = page.results.find(
        (item) =>
          item.to.some((address) => address.toLowerCase() === recipient) &&
          item.subject.toLowerCase().includes(subjectIncludes.toLowerCase()) &&
          (!since || new Date(item.receivedDate) >= since),
      );
      if (message) {
        const [html, plain] = await Promise.all([
          getText(`/api/messages/${message.id}/html`).catch(() => ''),
          getText(`/api/messages/${message.id}/plaintext`).catch(() => ''),
        ]);
        return `${html}\n${plain}`.replaceAll('&amp;', '&');
      }
    } catch (error) {
      lastError = error;
    }
    await sleep(1_000);
  }
  throw new Error(`E-mail "${subjectIncludes}" para ${to} não chegou em ${timeoutMs} ms${lastError ? `: ${String(lastError)}` : ''}`);
};

/** Extrai a primeira URL do corpo que contém o fragmento de caminho informado. */
export const extractLink = (body: string, pathFragment: string): string => {
  const pattern = new RegExp(`https?://[^\\s"'<>]*${pathFragment.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}[^\\s"'<>]*`);
  const match = body.match(pattern);
  if (!match) throw new Error(`Link com "${pathFragment}" não encontrado no e-mail.`);
  return match[0];
};
