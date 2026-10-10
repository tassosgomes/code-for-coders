import type { ReactNode } from 'react';
import Markdown from 'react-markdown';

type MarkdownTextProps = { children: string; className?: string };

const Heading = ({ children }: { children?: ReactNode }) => <p><strong>{children}</strong></p>;

// Raw HTML is dropped and images are not rendered; headings become compact paragraphs so they never compete with the page h1.
export const MarkdownText = ({ children, className }: MarkdownTextProps) => <div className={className ? `markdown-text ${className}` : 'markdown-text'}>
  <Markdown
    skipHtml
    disallowedElements={['img']}
    components={{
      h1: Heading, h2: Heading, h3: Heading, h4: Heading, h5: Heading, h6: Heading,
      a: (props) => <a href={props.href} target="_blank" rel="noopener noreferrer">{props.children}</a>,
    }}
  >{children}</Markdown>
</div>;
