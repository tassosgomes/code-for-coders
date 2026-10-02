import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';

import { Dialog } from '@/components/ui/dialog';
import { renderWithProviders } from '@/testing/test-utils';

const PublicationDialog = () => {
  const [open, setOpen] = useState(false);
  return <><button onClick={() => setOpen(true)}>Publicar</button>{open ? <Dialog title="Publicar curso" description="Confira o currículo." busy={false} onClose={() => setOpen(false)}><button onClick={() => setOpen(false)}>Cancelar</button></Dialog> : null}</>;
};

describe('Dialog keyboard navigation', () => {
  it('moves focus into the dialog, traps Tab and restores focus when Escape closes it', async () => {
    const user = userEvent.setup(); renderWithProviders(<PublicationDialog />);
    const trigger = screen.getByRole('button', { name: 'Publicar' });
    await user.click(trigger);
    const close = screen.getByRole('button', { name: 'Fechar' });
    expect(close).toHaveFocus();
    await user.tab({ shift: true }); expect(screen.getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    await user.tab(); expect(close).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(); expect(trigger).toHaveFocus();
  });
});
