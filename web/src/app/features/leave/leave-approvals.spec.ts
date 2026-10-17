import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../../api/api-configuration';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { LeaveApprovals } from './leave-approvals';

const employees = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'Zespół A' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2020-01-01', teamId: 't1', teamName: 'Zespół B' },
  { id: 'e3', firstName: 'Ewa', lastName: 'Lis', hireDate: '2020-01-01', teamId: 't1', teamName: 'Zespół C' },
];

function req(id: string, employeeId: string, status: string, from: string): LeaveRequestDto {
  return {
    id, employeeId, status, from, to: from, days: 1,
    createdAtUtc: '2026-10-01T00:00:00Z', decidedByEmployeeId: null, decisionReason: null,
  };
}

const requests = [
  req('r1', 'e1', 'Pending', '2026-11-02'),
  req('r2', 'e2', 'Approved', '2026-11-03'),
  req('r3', 'e3', 'Pending', '2026-11-09'),
  req('r4', 'e2', 'Pending', '2026-11-10'), // own request of the logged-in user (e2)
];

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

describe('LeaveApprovals', () => {
  let http: HttpTestingController;

  async function setup(list: LeaveRequestDto[] = requests) {
    const user = signal<CurrentUserDto | null>({
      userId: 'u2', employeeId: 'e2', userName: 'x', displayName: 'X', roles: ['Manager'],
    });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: SessionService, useValue: { user: user.asReadonly(), hasRole: () => true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const info = vi.spyOn(TestBed.inject(Notifier), 'info').mockImplementation(() => undefined);
    const error = vi.spyOn(TestBed.inject(Notifier), 'error').mockImplementation(() => undefined);
    const fixture = TestBed.createComponent(LeaveApprovals);
    fixture.detectChanges();
    http.expectOne('/api/employees').flush(employees);
    http.expectOne('/api/leave/requests').flush(list);
    await flush(fixture);
    const el = fixture.nativeElement as HTMLElement;
    // The dialog's afterClosed emits after its exit animation, so poll until it settles.
    const settle = async () => {
      await flush(fixture);
      await new Promise((resolve) => setTimeout(resolve, 300));
      await flush(fixture);
    };
    const row = (id: string) => el.querySelector(`tr[data-row="${id}"]`) as HTMLElement | null;
    const rowIds = () => Array.from(el.querySelectorAll('tr[data-row]')).map((x) => x.getAttribute('data-row'));
    const dialogEl = () => document.querySelector('app-decision-dialog') as HTMLElement | null;
    const save = () => document.querySelector('[data-save]') as HTMLButtonElement;
    const type = (text: string) => {
      const area = document.querySelector('textarea') as HTMLTextAreaElement;
      area.value = text;
      area.dispatchEvent(new Event('input'));
    };
    return { fixture, el, settle, row, rowIds, dialogEl, save, type, info, error };
  }

  afterEach(() => {
    http.verify();
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
  });

  it('lists_only_pending_requests_of_other_people_with_team_dates_and_days', async () => {
    const { rowIds, row } = await setup();
    expect(rowIds()).toEqual(['r1', 'r3']);
    const text = row('r1')!.textContent!;
    expect(text).toContain('Anna Nowak');
    expect(text).toContain('Zespół A');
    expect(text).toContain('02.11.2026');
    expect(text).toContain('01.10.2026');
  });

  it('approve_sends_null_reason_and_removes_row', async () => {
    const { row, rowIds, settle, save, info, fixture } = await setup();
    (row('r1')!.querySelector('[data-approve]') as HTMLButtonElement).click();
    await settle();
    save().click();
    const post = http.expectOne('/api/leave/requests/r1/decision');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ approve: true, reason: null });
    post.flush({ ...requests[0], status: 'Approved' });
    await settle();
    fixture.detectChanges();
    expect(rowIds()).toEqual(['r3']);
    expect(info).toHaveBeenCalled();
  });

  it('approve_sends_trimmed_optional_comment', async () => {
    const { row, settle, save, type } = await setup();
    (row('r1')!.querySelector('[data-approve]') as HTMLButtonElement).click();
    await settle();
    type('  Miłego wypoczynku  ');
    save().click();
    const post = http.expectOne('/api/leave/requests/r1/decision');
    expect(post.request.body).toEqual({ approve: true, reason: 'Miłego wypoczynku' });
    post.flush({ ...requests[0], status: 'Approved' });
    await settle();
  });

  it('reject_without_reason_sends_nothing_and_shows_message', async () => {
    const { row, settle, save, type, dialogEl } = await setup();
    (row('r1')!.querySelector('[data-reject]') as HTMLButtonElement).click();
    await settle();
    type('   ');
    save().click();
    await settle();
    http.expectNone('/api/leave/requests/r1/decision');
    expect(dialogEl()!.textContent).toContain('Podaj uzasadnienie odrzucenia.');
  });

  it('reject_with_reason_sends_trimmed_reason_and_removes_row', async () => {
    const { row, rowIds, settle, save, type } = await setup();
    (row('r3')!.querySelector('[data-reject]') as HTMLButtonElement).click();
    await settle();
    type(' Brak obsady ');
    save().click();
    const post = http.expectOne('/api/leave/requests/r3/decision');
    expect(post.request.body).toEqual({ approve: false, reason: 'Brak obsady' });
    post.flush({ ...requests[2], status: 'Rejected' });
    await settle();
    expect(rowIds()).toEqual(['r1']);
  });

  it.each([403, 404])('error_%i_shows_snackbar_and_keeps_row', async (status) => {
    const { row, rowIds, settle, save, error, dialogEl } = await setup();
    (row('r1')!.querySelector('[data-approve]') as HTMLButtonElement).click();
    await settle();
    save().click();
    http.expectOne('/api/leave/requests/r1/decision').flush(
      { title: 'Błąd', status, detail: 'Brak dostępu' },
      { status, statusText: 'Err' },
    );
    await settle();
    expect(error).toHaveBeenCalledTimes(1);
    expect(rowIds()).toEqual(['r1', 'r3']);
    expect(dialogEl()).toBeNull();
  });

  it('error_422_is_shown_in_dialog_which_stays_open_and_save_disabled_while_sending', async () => {
    const { row, rowIds, settle, save, dialogEl } = await setup();
    (row('r1')!.querySelector('[data-approve]') as HTMLButtonElement).click();
    await settle();
    save().click();
    save().click();
    await settle();
    expect(save().disabled).toBe(true);
    http.expectOne('/api/leave/requests/r1/decision').flush(
      { title: 'Błąd walidacji', status: 422, detail: 'Niewystarczające saldo urlopu: brakuje 2 dni.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await settle();
    expect(dialogEl()!.querySelector('[role="alert"]')!.textContent).toContain(
      'Niewystarczające saldo urlopu: brakuje 2 dni.',
    );
    expect(save().disabled).toBe(false);
    expect(rowIds()).toEqual(['r1', 'r3']);
  });

  it('shows_empty_state', async () => {
    const { el, rowIds } = await setup([requests[1], requests[3]]);
    expect(el.textContent).toContain('Brak wniosków do zatwierdzenia');
    expect(rowIds()).toEqual([]);
  });
});
