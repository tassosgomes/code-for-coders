import { Code2 } from 'lucide-react';

type CodeWindowProps = {
  className?: string;
  code: string;
  filename: string;
};

export const CodeWindow = ({ className, code, filename }: CodeWindowProps) => (
  <figure
    aria-label={`Exemplo de código: ${filename}`}
    className={`overflow-hidden rounded-xl border border-white/10 bg-code text-code-foreground shadow-lg ${className ?? ''}`}
  >
    <figcaption className="flex items-center gap-3 border-b border-white/10 px-4 py-3 font-mono text-xs text-code-foreground/70">
      <Code2 aria-hidden="true" className="size-4 text-code-accent" />
      <span>{filename}</span>
      <span className="ml-auto text-code-accent">TS</span>
    </figcaption>
    <pre className="overflow-x-auto p-5 font-mono text-sm leading-6">
      <code>{code}</code>
    </pre>
  </figure>
);
