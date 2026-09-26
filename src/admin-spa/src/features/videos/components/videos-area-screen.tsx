import { Clapperboard, Upload } from 'lucide-react';
import { Link } from 'react-router';

import { paths } from '@/config/paths';

type VideosAreaScreenProps = {
  state: 'loading' | 'empty' | 'has-videos' | 'unavailable' | 'forbidden';
  onRetry?: () => void;
};

export const VideosAreaScreen = ({ state, onRetry }: VideosAreaScreenProps) => {
  if (state === 'forbidden') {
    return <main className="page-shell videos-page">
      <p className="eyebrow">Vídeos</p>
      <h1>Sem permissão</h1>
      <section className="empty-state" role="alert">
        <h2>Você não tem permissão para acessar esta área.</h2>
        <Link className="outline-button" to={paths.home.getHref()}>Voltar para o início</Link>
      </section>
    </main>;
  }

  return <main className="page-shell videos-page">
    <div className="page-heading-row">
      <div>
        <p className="eyebrow">Vídeos</p>
        <h1>Vídeos da escola</h1>
        <p className="page-subtitle">Envie as gravações das aulas e acompanhe até ficarem prontas.</p>
      </div>
      <button className="primary-button" disabled type="button"><Upload aria-hidden="true" size={16} />Enviar vídeo</button>
    </div>

    {state === 'loading' ? <section aria-label="Carregando vídeos" className="empty-state videos-state" aria-busy="true">
      <p>Carregando vídeos…</p>
    </section> : null}

    {state === 'empty' ? <section aria-label="Biblioteca de vídeos vazia" className="empty-state videos-state">
      <div aria-hidden="true" className="code-window">
        <div className="code-title"><span className="window-dots"><i /><i /><i /></span>videos.http</div>
        <pre>1  $ ls videos/{'\n'}2  <span>(vazio)</span></pre>
      </div>
      <h2>Nenhum vídeo ainda</h2>
      <p>Envie a primeira gravação. Ela fica pronta para a aula sozinha.</p>
      <button className="primary-button" disabled type="button"><Upload aria-hidden="true" size={16} />Enviar vídeo</button>
    </section> : null}

    {state === 'has-videos' ? <section className="empty-state videos-state">
      <Clapperboard aria-hidden="true" size={28} />
      <p>A lista de vídeos será exibida aqui.</p>
    </section> : null}

    {state === 'unavailable' ? <section className="empty-state videos-state" role="alert">
      <h2>Não conseguimos carregar os vídeos agora.</h2>
      <button className="outline-button" onClick={onRetry} type="button">Tentar de novo</button>
    </section> : null}
  </main>;
};
