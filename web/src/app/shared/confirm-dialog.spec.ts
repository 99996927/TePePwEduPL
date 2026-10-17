import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialog } from './confirm-dialog';

describe('ConfirmDialog', () => {
  async function open() {
    const ref = TestBed.inject(MatDialog).open(ConfirmDialog, {
      data: { title: 'Tytuł', message: 'Czy na pewno?', confirmLabel: 'Tak, usuń' },
    });
    const closed = firstValueFrom(ref.afterClosed());
    const button = (text: string) =>
      Array.from(document.querySelectorAll('app-confirm-dialog button')).find(
        (b) => b.textContent!.trim() === text,
      ) as HTMLButtonElement;
    await firstValueFrom(ref.afterOpened());
    await TestBed.inject(ApplicationRef).whenStable();
    return { ref, closed, button };
  }

  it('renders_title_message_and_confirm_label', async () => {
    const { button } = await open();
    const text = document.querySelector('app-confirm-dialog')!.textContent!;
    expect(text).toContain('Tytuł');
    expect(text).toContain('Czy na pewno?');
    expect(button('Tak, usuń')).toBeTruthy();
  });

  it('confirm_button_closes_with_true', async () => {
    const { closed, button } = await open();
    button('Tak, usuń').click();
    expect(await closed).toBe(true);
  });

  it('cancel_button_closes_with_false', async () => {
    const { closed, button } = await open();
    button('Anuluj').click();
    expect(await closed).toBe(false);
  });
});
