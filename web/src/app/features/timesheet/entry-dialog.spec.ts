import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS, MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideApiConfiguration } from '../../api/api-configuration';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { Notifier } from '../../core/notifier';
import { EntryDialog, EntryDialogData } from './entry-dialog';

const saved: WorkEntryDto = {
  id: 'w1', employeeId: 'e1', start: '2026-10-12T08:00:00', end: '2026-10-12T16:00:00', breakMinutes: 30,
};

describe('EntryDialog', () => {
  let http: HttpTestingController;
  const close = vi.fn();
  const ref = { close, disableClose: false };

  function setup(data: EntryDialogData = { mode: 'add' }) {
    close.mockReset();
    ref.disableClose = false;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: MAT_DATE_LOCALE, useValue: 'pl-PL' },
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: MatDialogRef, useValue: ref },
        { provide: MAT_DIALOG_DATA, useValue: data },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(EntryDialog);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const save = () => el.querySelector('[data-save]') as HTMLButtonElement;
    const fill = (v: { date?: Date; start?: string; end?: string; breakMinutes?: number; reason?: string }) => {
      fixture.componentInstance.form.patchValue(v);
      fixture.detectChanges();
    };
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, el, save, fill, settle };
  }

  afterEach(() => http.verify());

  const problem = (status: number, detail: string) => [
    { title: 'Błąd', status, detail },
    { status, statusText: 'Error' },
  ] as const;

  it('add_posts_local_times_without_zone', async () => {
    const { save, fill, settle } = setup();
    vi.spyOn(TestBed.inject(Notifier), 'info').mockImplementation(() => undefined);
    fill({ date: new Date(2026, 9, 12), start: '08:00', end: '16:00', breakMinutes: 30 });
    save().click();
    const post = http.expectOne('/api/timesheet/entries');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({
      start: '2026-10-12T08:00:00', end: '2026-10-12T16:00:00', breakMinutes: 30,
    });
    post.flush(saved);
    await settle();
    expect(close).toHaveBeenCalledWith(saved);
  });

  it('end_not_after_start_goes_to_next_day_and_shows_hint', async () => {
    const { el, save, fill } = setup();
    expect(el.textContent).not.toContain('Wpis zakończy się następnego dnia');
    fill({ date: new Date(2026, 9, 12), start: '22:00', end: '06:00', breakMinutes: 0 });
    expect(el.textContent).toContain('Wpis zakończy się następnego dnia');
    save().click();
    const post = http.expectOne('/api/timesheet/entries');
    expect(post.request.body).toEqual({
      start: '2026-10-12T22:00:00', end: '2026-10-13T06:00:00', breakMinutes: 0,
    });
  });

  it('equal_start_and_end_means_next_day', () => {
    const { el, save, fill } = setup();
    fill({ date: new Date(2026, 9, 12), start: '08:00', end: '08:00', breakMinutes: 0 });
    expect(el.textContent).toContain('Wpis zakończy się następnego dnia');
    save().click();
    expect(http.expectOne('/api/timesheet/entries').request.body.end).toBe('2026-10-13T08:00:00');
  });

  it('month_end_rolls_over_to_next_month', () => {
    const { save, fill } = setup();
    fill({ date: new Date(2026, 9, 31), start: '22:00', end: '06:00', breakMinutes: 0 });
    save().click();
    expect(http.expectOne('/api/timesheet/entries').request.body.end).toBe('2026-11-01T06:00:00');
  });

  it('dst_change_day_still_sends_plain_local_values', () => {
    const { save, fill } = setup();
    fill({ date: new Date(2026, 9, 25), start: '01:30', end: '04:30', breakMinutes: 0 });
    save().click();
    const body = http.expectOne('/api/timesheet/entries').request.body;
    expect(body).toEqual({ start: '2026-10-25T01:30:00', end: '2026-10-25T04:30:00', breakMinutes: 0 });
    expect(JSON.stringify(body)).not.toContain('Z');
  });

  it('dst_change_day_overnight_goes_to_next_calendar_day', () => {
    const { save, fill } = setup();
    fill({ date: new Date(2026, 9, 25), start: '22:00', end: '06:00', breakMinutes: 0 });
    save().click();
    expect(http.expectOne('/api/timesheet/entries').request.body).toEqual({
      start: '2026-10-25T22:00:00', end: '2026-10-26T06:00:00', breakMinutes: 0,
    });
  });

  it('shows_422_keeps_dialog_open_and_blocks_close_and_double_submit_while_sending', async () => {
    const { el, save, fill, settle } = setup();
    fill({ date: new Date(2026, 9, 12), start: '08:00', end: '16:00', breakMinutes: 30 });
    save().click();
    save().click();
    await settle();
    expect(ref.disableClose).toBe(true);
    expect(save().disabled).toBe(true);
    http.expectOne('/api/timesheet/entries').flush(...problem(422, 'Wpis nie może kończyć się w przyszłości.'));
    await settle();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Wpis nie może kończyć się w przyszłości.');
    expect(close).not.toHaveBeenCalled();
    expect(ref.disableClose).toBe(false);
    expect(save().disabled).toBe(false);
  });

  it('shows_409_overlap_message', async () => {
    const { el, save, fill, settle } = setup();
    fill({ date: new Date(2026, 9, 12), start: '08:00', end: '16:00', breakMinutes: 0 });
    save().click();
    http.expectOne('/api/timesheet/entries').flush(...problem(409, 'Wpis nakłada się na istniejący wpis.'));
    await settle();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Wpis nakłada się na istniejący wpis.');
    expect(close).not.toHaveBeenCalled();
  });

  it('add_mode_has_no_reason_field', () => {
    const { el } = setup();
    expect(el.querySelector('[data-reason]')).toBeNull();
  });

  describe('correct mode', () => {
    const entry: WorkEntryDto = {
      id: 'w9', employeeId: 'e1', start: '2026-10-12T08:00:00', end: '2026-10-12T16:00:00', breakMinutes: 30,
    };

    it('prefills_from_entry_and_does_not_send_without_reason', () => {
      const { el, save, fill } = setup({ mode: 'correct', entry });
      const f = (n: string) => (el.querySelector(`[data-field="${n}"]`) as HTMLInputElement).value;
      expect(f('start')).toBe('08:00');
      expect(f('end')).toBe('16:00');
      expect(f('breakMinutes')).toBe('30');
      expect(save().disabled).toBe(true);
      fill({ reason: '   ' });
      save().click();
      http.expectNone('/api/timesheet/entries/w9');
    });

    it('prefills_next_day_entry_with_hint', () => {
      const { el } = setup({
        mode: 'correct',
        entry: { ...entry, start: '2026-10-12T22:00:00', end: '2026-10-13T06:00:00' },
      });
      expect(el.textContent).toContain('Wpis zakończy się następnego dnia');
    });

    it('puts_entry_with_reason', async () => {
      const { save, fill, settle } = setup({ mode: 'correct', entry });
      vi.spyOn(TestBed.inject(Notifier), 'info').mockImplementation(() => undefined);
      fill({ end: '17:00', reason: 'Pomyłka w godzinie' });
      save().click();
      const put = http.expectOne('/api/timesheet/entries/w9');
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        start: '2026-10-12T08:00:00', end: '2026-10-12T17:00:00', breakMinutes: 30, reason: 'Pomyłka w godzinie',
      });
      put.flush({ ...entry, end: '2026-10-12T17:00:00' });
      await settle();
      expect(close).toHaveBeenCalled();
    });
  });
});
