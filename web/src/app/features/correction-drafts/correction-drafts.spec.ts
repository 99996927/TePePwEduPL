import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { provideApiConfiguration } from '../../api/api-configuration';
import { CorrectionDraftDto } from '../../api/models/correction-draft-dto';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { CorrectionDrafts } from './correction-drafts';

const employees = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'ua' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'uj' },
];

function draft(id: string, employeeId: string, workEntryId: string, start: string, end: string, status: string, author: string, note: string | null = null): CorrectionDraftDto {
  return {
    id, employeeId, workEntryId, proposedStart: start, proposedEnd: end, proposedBreakMinutes: 30, reason: `Powód ${id}`, status,
    createdByUserId: author, decidedByUserId: null, createdAtUtc: '2026-10-01T00:00:00Z', decidedAtUtc: null, decisionNote: note, conversationId: null,
  };
}

const drafts = [
  draft('d1', 'e1', 'w1', '2026-09-29T12:00:00', '2026-09-29T20:30:00', 'Pending', 'ud'),
  draft('d2', 'e2', 'w2', '2026-10-02T22:00:00', '2026-10-03T06:00:00', 'Pending', 'unknown'),
  draft('d3', 'e2', 'w9', '2026-09-30T08:00:00', '2026-09-30T16:00:00', 'Approved', 'uj', 'Ok'),
];
const sept: WorkEntryDto[] = [{ id: 'w1', employeeId: 'e1', start: '2026-09-29T14:00:00', end: '2026-09-29T22:30:00', breakMinutes: 30 }];
const oct: WorkEntryDto[] = [];

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

describe('CorrectionDrafts', () => {
  let http: HttpTestingController;

  async function setup(list: CorrectionDraftDto[] = drafts.slice(0, 2), roles: string[] = ['HRApprover']) {
    const user = signal<CurrentUserDto | null>({ userId: 'ud', employeeId: 'e9', userName: 'x', displayName: 'X', roles });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: SessionService, useValue: { user: user.asReadonly(), hasRole: (...r: string[]) => r.some((x) => roles.includes(x)) } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const info = vi.spyOn(TestBed.inject(Notifier), 'info').mockImplementation(() => undefined);
    const error = vi.spyOn(TestBed.inject(Notifier), 'error').mockImplementation(() => undefined);
    const fixture = TestBed.createComponent(CorrectionDrafts);
    fixture.detectChanges();
    http.expectOne('/api/employees').flush(employees);
    http.expectOne('/api/correction-drafts?status=Pending').flush(list);
    await flush(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const settle = () => flush(fixture);
    const flushMonths = async () => {
      http.expectOne('/api/timesheet/entries?from=2026-09-01&to=2026-09-30').flush(sept);
      http.expectOne('/api/timesheet/entries?from=2026-10-01&to=2026-10-31').flush(oct);
      await settle();
    };
    const row = (id: string) => el.querySelector(`tr[data-row="${id}"]`) as HTMLElement | null;
    const btn = (id: string, name: string) => row(id)!.querySelector(`[data-${name}]`) as HTMLButtonElement | null;
    const dialogEl = () => document.querySelector('app-draft-decision-dialog') as HTMLElement | null;
    const save = () => document.querySelector('[data-save]') as HTMLButtonElement;
    const type = (text: string) => {
      const area = document.querySelector('textarea') as HTMLTextAreaElement;
      area.value = text;
      area.dispatchEvent(new Event('input'));
    };
    return { fixture, el, settle, flushMonths, row, btn, dialogEl, save, type, info, error };
  }

  afterEach(() => {
    http.verify();
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
  });

  it('default_query_is_pending_and_all_removes_the_parameter', async () => {
    const { el, settle, flushMonths } = await setup();
    await flushMonths();
    const select = el.querySelector('mat-select') as HTMLElement;
    select.click();
    await settle();
    const option = Array.from(document.querySelectorAll('mat-option')).find((o) => o.textContent!.trim() === 'Wszystkie') as HTMLElement;
    option.click();
    await settle();
    http.expectOne((r) => r.url === '/api/correction-drafts' && r.params.keys().length === 0).flush(drafts);
    await settle();
    await flushMonths();
    expect(el.querySelector('tr[data-row="d3"]')).not.toBeNull();
  });

  it('shows_proposed_and_current_hours_with_one_request_per_month', async () => {
    const { row, flushMonths } = await setup();
    await flushMonths();
    const t1 = row('d1')!.textContent!;
    expect(t1).toContain('Anna Nowak');
    expect(t1).toContain('29.09.2026');
    expect(t1).toContain('12:00–20:30');
    expect(t1).toContain('14:00–22:30');
    expect(t1).toContain('30 min');
    expect(t1).toContain('Powód d1');
    expect(t1).toContain('Ty');
    const t2 = row('d2')!.textContent!;
    expect(t2).toContain('22:00–06:00');
    expect(t2).toContain('+1 dzień');
    expect(t2).toContain('—'); // no current entry and unknown author
  });

  it('shows_decision_note_and_empty_state', async () => {
    const { el, row, settle } = await setup([drafts[2]]);
    http.expectOne('/api/timesheet/entries?from=2026-09-01&to=2026-09-30').flush([]);
    await settle();
    expect(row('d3')!.textContent).toContain('Ok');
    expect(row('d3')!.textContent).toContain('Zatwierdzony');
    expect(row('d3')!.querySelector('[data-approve]')).toBeNull();
    expect(el.textContent).not.toContain('Brak szkiców korekt');
  });

  it('hr_role_sees_decision_buttons_on_pending_draft', async () => {
    const { btn, flushMonths } = await setup(drafts.slice(0, 2), ['HR']);
    await flushMonths();
    expect(btn('d1', 'approve')).not.toBeNull();
    expect(btn('d1', 'reject')).not.toBeNull();
  });

  it('load_error_shows_retry_that_reloads', async () => {
    const { el, settle, flushMonths } = await setup();
    await flushMonths();
    (el.querySelector('mat-select') as HTMLElement).click();
    await settle();
    const option = Array.from(document.querySelectorAll('mat-option')).find((o) => o.textContent!.trim() === 'Odrzucone') as HTMLElement;
    option.click();
    await settle();
    http.expectOne('/api/correction-drafts?status=Rejected').flush({ title: 'Awaria', status: 500 }, { status: 500, statusText: 'x' });
    await settle();
    expect(el.querySelector('[role="alert"]')).not.toBeNull();
    (el.querySelector('[data-retry]') as HTMLButtonElement).click();
    http.expectOne('/api/correction-drafts?status=Rejected').flush([]);
    await settle();
    expect(el.querySelector('[role="alert"]')).toBeNull();
    expect(el.textContent).toContain('Brak szkiców korekt');
  });

  it('empty_list_shows_empty_state', async () => {
    const { el } = await setup([]);
    expect(el.textContent).toContain('Brak szkiców korekt');
  });

  it('approve_posts_null_note_then_trimmed_note_and_refreshes', async () => {
    const { btn, settle, save, flushMonths, info } = await setup();
    await flushMonths();
    btn('d1', 'approve')!.click();
    await settle();
    save().click();
    const post = http.expectOne('/api/correction-drafts/d1/decision');
    expect(post.request.body).toEqual({ approve: true, note: null });
    post.flush({ ...drafts[0], status: 'Approved' });
    await settle();
    http.expectOne('/api/correction-drafts?status=Pending').flush([drafts[1]]);
    await settle();
    http.expectOne('/api/timesheet/entries?from=2026-10-01&to=2026-10-31').flush([]);
    await settle();
    expect(info).toHaveBeenCalled();
  });

  it('approve_with_note_sends_trimmed_note', async () => {
    const { btn, settle, save, flushMonths, type } = await setup();
    await flushMonths();
    btn('d1', 'approve')!.click();
    await settle();
    type('  Ok  ');
    save().click();
    const post = http.expectOne('/api/correction-drafts/d1/decision');
    expect(post.request.body).toEqual({ approve: true, note: 'Ok' });
    post.flush({ ...drafts[0], status: 'Approved' });
    await settle();
    http.expectOne('/api/correction-drafts?status=Pending').flush([]);
    await settle();
  });

  it('reject_without_note_sends_nothing_and_with_note_sends_it', async () => {
    const { btn, settle, save, type, dialogEl, flushMonths } = await setup();
    await flushMonths();
    btn('d1', 'reject')!.click();
    await settle();
    type('   ');
    save().click();
    await settle();
    http.expectNone('/api/correction-drafts/d1/decision');
    expect(dialogEl()!.textContent).toContain('Podaj uzasadnienie odrzucenia.');
    type(' Nie ');
    save().click();
    const post = http.expectOne('/api/correction-drafts/d1/decision');
    expect(post.request.body).toEqual({ approve: false, note: 'Nie' });
    post.flush({ ...drafts[0], status: 'Rejected' });
    await settle();
    http.expectOne('/api/correction-drafts?status=Pending').flush([]);
    await settle();
  });

  it('decide_422_stays_in_dialog_double_click_sends_once_and_unblocks', async () => {
    const { btn, settle, save, dialogEl, flushMonths } = await setup();
    await flushMonths();
    btn('d1', 'approve')!.click();
    await settle();
    save().click();
    save().click();
    await settle();
    const dialog = TestBed.inject(MatDialog).openDialogs[0];
    expect(dialog.disableClose).toBe(true);
    http.expectOne('/api/correction-drafts/d1/decision').flush(
      { title: 'Walidacja', status: 422, detail: 'Wpisy nakładają się.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await settle();
    expect(dialogEl()!.querySelector('[role="alert"]')!.textContent).toContain('Wpisy nakładają się.');
    expect(dialog.disableClose).toBe(false);
    expect(save().disabled).toBe(false);
  });

  it('decide_403_closes_dialog_with_snackbar', async () => {
    const { btn, settle, save, dialogEl, flushMonths, error } = await setup();
    await flushMonths();
    btn('d1', 'approve')!.click();
    await settle();
    save().click();
    http.expectOne('/api/correction-drafts/d1/decision').flush(
      { title: 'Brak uprawnień', status: 403 },
      { status: 403, statusText: 'Forbidden' },
    );
    await settle();
    expect(dialogEl()).toBeNull();
    expect(error).toHaveBeenCalledTimes(1);
  });

  it('decide_409_unblocks_dialog_shows_message_and_refreshes_list', async () => {
    const { btn, settle, save, dialogEl, flushMonths, error, row } = await setup();
    await flushMonths();
    btn('d1', 'approve')!.click();
    await settle();
    save().click();
    http.expectOne('/api/correction-drafts/d1/decision').flush(
      { title: 'Konflikt', status: 409, detail: 'Decyzja już zapadła.' },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();
    expect(dialogEl()).toBeNull();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/correction-drafts?status=Pending').flush([drafts[1]]);
    await settle();
    http.expectOne('/api/timesheet/entries?from=2026-10-01&to=2026-10-31').flush([]);
    await settle();
    expect(row('d1')).toBeNull();
    expect(row('d2')).not.toBeNull();
  });
});
