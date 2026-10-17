import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { ConversationSummaryDto } from '../../api/models/conversation-summary-dto';
import { formatInstantDateTime } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { SessionService } from '../../core/session';
import { ConversationList } from './conversation-list';

function conv(id: string, userId: string, over: Partial<ConversationSummaryDto> = {}): ConversationSummaryDto {
  return { id, userId, startedAtUtc: '2026-10-05T10:30:00Z', preview: `Pytanie ${id}`, messageCount: 4, ...over };
}

describe('ConversationList', () => {
  function setup(inputs: Partial<Record<string, unknown>> = {}) {
    const user = signal({ userId: 'ume', employeeId: 'e9', userName: 'x', displayName: 'X', roles: ['HR'] });
    const names: Record<string, string> = { ume: 'Ty', uo: 'Jan Kowalski' };
    TestBed.configureTestingModule({
      providers: [
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: SessionService, useValue: { user: user.asReadonly() } },
        { provide: EmployeeDirectory, useValue: { userLabel: (id: string) => names[id] ?? '—' } },
      ],
    });
    const fixture: ComponentFixture<ConversationList> = TestBed.createComponent(ConversationList);
    const base: Record<string, unknown> = {
      conversations: [conv('c1', 'ume'), conv('c2', 'uo')],
      selectedId: null, showAuthor: false, loading: false, error: null,
    };
    for (const [k, v] of Object.entries({ ...base, ...inputs })) fixture.componentRef.setInput(k, v);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    return { fixture, el, row: (id: string) => el.querySelector<HTMLElement>(`[data-conversation="${id}"]`) };
  }

  it('renders rows with preview, date and message count, in given order', () => {
    const { el, row } = setup();
    expect([...el.querySelectorAll('[data-conversation]')].map((r) => r.getAttribute('data-conversation'))).toEqual(['c1', 'c2']);
    const text = row('c1')!.textContent!;
    expect(text).toContain('Pytanie c1');
    expect(text).toContain(formatInstantDateTime('2026-10-05T10:30:00Z'));
    expect(text).toContain('4');
  });

  it('shows placeholder for null preview', () => {
    const { row } = setup({ conversations: [conv('c1', 'ume', { preview: null })] });
    expect(row('c1')!.textContent).toContain('(bez pytania)');
  });

  it('shows author when showAuthor: Ty, name, dash', () => {
    const { row } = setup({ showAuthor: true, conversations: [conv('c1', 'ume'), conv('c2', 'uo'), conv('c3', 'ux')] });
    expect(row('c1')!.querySelector('.author')!.textContent!.trim()).toBe('Ty');
    expect(row('c2')!.querySelector('.author')!.textContent!.trim()).toBe('Jan Kowalski');
    expect(row('c3')!.querySelector('.author')!.textContent!.trim()).toBe('—');
  });

  it('hides author and the only-mine toggle without showAuthor', () => {
    const { el, row } = setup();
    expect(row('c1')!.querySelector('.author')).toBeNull();
    expect(el.querySelector('mat-slide-toggle')).toBeNull();
  });

  it('only-mine toggle hides other authors conversations', async () => {
    const { fixture, row, el } = setup({ showAuthor: true });
    expect(el.querySelector('mat-slide-toggle')!.textContent).toContain('Tylko moje');
    fixture.componentRef.setInput('onlyMine', true);
    fixture.detectChanges();
    expect(row('c1')).not.toBeNull();
    expect(row('c2')).toBeNull();
  });

  it('emits select with id on row click and create on button', () => {
    const { fixture, row, el } = setup();
    const selected: string[] = [];
    let created = 0;
    fixture.componentInstance.select.subscribe((id: string) => selected.push(id));
    fixture.componentInstance.create.subscribe(() => created++);
    row('c2')!.click();
    expect(selected).toEqual(['c2']);
    const create = [...el.querySelectorAll('button')].find((b) => b.textContent!.includes('Nowa rozmowa'))!;
    create.click();
    expect(created).toBe(1);
  });

  it('marks selected row with aria-current', () => {
    const { row } = setup({ selectedId: 'c2' });
    expect(row('c2')!.getAttribute('aria-current')).toBe('true');
    expect(row('c1')!.hasAttribute('aria-current')).toBe(false);
  });

  it('shows empty state', () => {
    const { el } = setup({ conversations: [] });
    expect(el.textContent).toContain('Brak rozmów.');
  });

  it('shows loading state without empty text', () => {
    const { el } = setup({ conversations: [], loading: true });
    expect(el.querySelector('mat-progress-bar')).not.toBeNull();
    expect(el.textContent).not.toContain('Brak rozmów.');
  });

  it('shows error with retry button', () => {
    const { fixture, el } = setup({ conversations: [], error: 'Błąd serwera' });
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Błąd serwera');
    expect(el.textContent).not.toContain('Brak rozmów.');
    let retried = 0;
    fixture.componentInstance.retry.subscribe(() => retried++);
    [...el.querySelectorAll('button')].find((b) => b.textContent!.includes('Spróbuj ponownie'))!.click();
    expect(retried).toBe(1);
  });

  it('renders script in preview as text', () => {
    const html = '<script>alert(1)</script>';
    const { el, row } = setup({ conversations: [conv('c1', 'ume', { preview: html })] });
    expect(el.querySelector('script')).toBeNull();
    expect(row('c1')!.textContent).toContain(html);
  });
});
