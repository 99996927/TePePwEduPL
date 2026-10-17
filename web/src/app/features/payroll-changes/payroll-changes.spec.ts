import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS, provideNativeDateAdapter } from '@angular/material/core';
import { MatDialog } from '@angular/material/dialog';
import { provideApiConfiguration } from '../../api/api-configuration';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { PayrollChangeDto } from '../../api/models/payroll-change-dto';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { PayrollChanges } from './payroll-changes';

const employees = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'ua' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'uj' },
  { id: 'e3', firstName: 'Tomasz', lastName: 'Wis', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'ut' },
  { id: 'e4', firstName: 'Piotr', lastName: 'Zet', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'up' },
];

function change(id: string, employeeId: string, component: string, value: number, from: string, status: string, author: string, created: string, decider: string | null = null): PayrollChangeDto {
  return {
    id, employeeId, component, value, effectiveFrom: from, reason: `Powód ${id}`, status,
    createdByUserId: author, decidedByUserId: decider, decisionReason: decider ? `Decyzja ${id}` : null, createdAtUtc: created,
  };
}

const changes = [
  change('c1', 'e1', 'BaseSalary', 7800, '2026-11-01', 'Pending', 'up', '2026-10-10T00:00:00Z'),
  change('c2', 'e2', 'FixedAllowance', 400, '2026-06-01', 'Approved', 'unknown-user', '2026-05-01T00:00:00Z', 'up'),
  change('c3', 'e3', 'FixedAllowance', 300, '2026-09-01', 'Rejected', 'up', '2026-08-01T00:00:00Z', 'ud'),
  change('c4', 'e2', 'BaseSalary', 5000, '2026-12-01', 'Draft', 'ud', '2026-10-15T00:00:00Z'),
];

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

describe('PayrollChanges', () => {
  let http: HttpTestingController;

  async function setup(userId: string, roles: string[], list: PayrollChangeDto[] = changes) {
    const user = signal<CurrentUserDto | null>({ userId, employeeId: 'e4', userName: 'x', displayName: 'X', roles });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        {
          provide: SessionService,
          useValue: { user: user.asReadonly(), hasRole: (...r: string[]) => r.some((x) => roles.includes(x)) },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const info = vi.spyOn(TestBed.inject(Notifier), 'info').mockImplementation(() => undefined);
    const error = vi.spyOn(TestBed.inject(Notifier), 'error').mockImplementation(() => undefined);
    const fixture = TestBed.createComponent(PayrollChanges);
    fixture.detectChanges();
    http.expectOne('/api/employees').flush(employees);
    http.expectOne('/api/payroll-changes').flush(list);
    await flush(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const settle = () => flush(fixture);
    const row = (id: string) => el.querySelector(`tr[data-row="${id}"]`) as HTMLElement | null;
    const rowIds = () => Array.from(el.querySelectorAll('tr[data-row]')).map((x) => x.getAttribute('data-row'));
    const btn = (id: string, name: string) => row(id)!.querySelector(`[data-${name}]`) as HTMLButtonElement | null;
    const confirm = async () => {
      await settle();
      (document.querySelector('app-confirm-dialog button[mat-dialog-close="true"], app-confirm-dialog .mat-mdc-unelevated-button') as HTMLButtonElement).click();
    };
    const dialogEl = () => document.querySelector('app-reject-dialog') as HTMLElement | null;
    const save = () => document.querySelector('[data-save]') as HTMLButtonElement;
    const type = (text: string) => {
      const area = document.querySelector('textarea') as HTMLTextAreaElement;
      area.value = text;
      area.dispatchEvent(new Event('input'));
    };
    return { fixture, el, settle, row, rowIds, btn, confirm, dialogEl, save, type, info, error };
  }

  afterEach(() => {
    http.verify();
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
  });

  it('lists_rows_newest_first_with_labels_money_and_authors', async () => {
    const { rowIds, row } = await setup('ud', ['HR']);
    expect(rowIds()).toEqual(['c4', 'c1', 'c3', 'c2']);
    const t1 = row('c1')!.textContent!;
    expect(t1).toContain('Anna Nowak');
    expect(t1).toContain('Pensja zasadnicza');
    expect(t1).toContain('Oczekuje na zatwierdzenie');
    expect(t1).toMatch(/7\s?800,00\s?zł/);
    expect(t1).toContain('01.11.2026');
    expect(t1).toContain('Piotr Zet');
    expect(t1).toContain('Powód c1');
    expect(row('c4')!.textContent).toContain('Ty');
    expect(row('c4')!.textContent).toContain('Szkic');
    expect(row('c2')!.textContent).toContain('Dodatek stały');
    expect(row('c2')!.textContent).toContain('—'); // unknown author, no exception
    expect(row('c2')!.textContent).toContain('Decyzja c2');
    expect(row('c3')!.textContent).toContain('Odrzucona');
  });

  it('own_userId_as_decider_shows_Ty', async () => {
    const { row } = await setup('up', ['HRApprover']);
    expect(row('c2')!.textContent).toContain('Ty');
  });

  it('shows_empty_state', async () => {
    const { el } = await setup('ud', ['HR'], []);
    expect(el.textContent).toContain('Brak zmian płacowych');
  });

  it('status_filter_is_client_side', async () => {
    const { fixture, rowIds, settle } = await setup('ud', ['HR']);
    const cmp = fixture.componentInstance as unknown as { status: { set(v: string | null): void } };
    cmp.status.set('Approved');
    await settle();
    expect(rowIds()).toEqual(['c2']);
  });

  it('employee_filter_requests_with_employeeId', async () => {
    const { fixture, rowIds, settle } = await setup('ud', ['HR']);
    const cmp = fixture.componentInstance as unknown as { selectEmployee(id: string | null): void };
    cmp.selectEmployee('e2');
    http.expectOne('/api/payroll-changes?employeeId=e2').flush([changes[1], changes[3]]);
    await settle();
    expect(rowIds()).toEqual(['c4', 'c2']);
  });

  it('draft_author_can_submit_after_confirmation', async () => {
    const { btn, confirm, settle, rowIds } = await setup('ud', ['HR']);
    btn('c4', 'submit')!.click();
    await confirm();
    const post = http.expectOne('/api/payroll-changes/c4/submit');
    expect(post.request.method).toBe('POST');
    post.flush({ ...changes[3], status: 'Pending' });
    await settle();
    http.expectOne('/api/payroll-changes').flush(changes);
    await settle();
    expect(rowIds().length).toBe(4);
  });

  it('draft_of_someone_else_has_no_submit_and_hr_has_no_decision_buttons', async () => {
    const { btn } = await setup('other', ['HR']);
    expect(btn('c4', 'submit')).toBeNull();
    expect(btn('c1', 'approve')).toBeNull();
    expect(btn('c1', 'reject')).toBeNull();
  });

  it('approver_sees_decision_buttons_on_foreign_pending_only', async () => {
    const { btn, row } = await setup('ud', ['HRApprover']);
    expect(btn('c1', 'approve')).not.toBeNull();
    expect(btn('c1', 'reject')).not.toBeNull();
    expect(btn('c4', 'approve')).toBeNull();
    expect(btn('c4', 'submit')).toBeNull();
    expect(row('c2')!.querySelector('button')).toBeNull();
  });

  it('own_pending_shows_message_instead_of_buttons', async () => {
    const { btn, row } = await setup('up', ['HRApprover']);
    expect(btn('c1', 'approve')).toBeNull();
    expect(btn('c1', 'reject')).toBeNull();
    expect(row('c1')!.textContent).toContain('Autor nie może zatwierdzić własnej zmiany.');
  });

  it('approve_after_confirmation_posts_approve', async () => {
    const { btn, confirm, settle, info } = await setup('ud', ['HRApprover']);
    btn('c1', 'approve')!.click();
    await confirm();
    const post = http.expectOne('/api/payroll-changes/c1/approve');
    expect(post.request.method).toBe('POST');
    post.flush({ ...changes[0], status: 'Approved', decidedByUserId: 'ud' });
    await settle();
    http.expectOne('/api/payroll-changes').flush(changes);
    await settle();
    expect(info).toHaveBeenCalled();
  });

  it('double_click_on_approve_sends_one_request', async () => {
    const { btn, confirm, settle } = await setup('ud', ['HRApprover']);
    btn('c1', 'approve')!.click();
    btn('c1', 'approve')!.click();
    await confirm();
    expect(document.querySelectorAll('app-confirm-dialog').length).toBe(0);
    const reqs = http.match('/api/payroll-changes/c1/approve');
    expect(reqs.length).toBe(1);
    reqs[0].flush({ ...changes[0], status: 'Approved' });
    await settle();
    http.expectOne('/api/payroll-changes').flush(changes);
    await settle();
  });

  it('approve_409_shows_message_refreshes_list_and_unlocks_row', async () => {
    const { btn, confirm, settle, error, rowIds } = await setup('ud', ['HRApprover']);
    btn('c1', 'approve')!.click();
    await confirm();
    http.expectOne('/api/payroll-changes/c1/approve').flush(
      { title: 'Konflikt', status: 409, detail: 'Decyzja już zapadła.' },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/payroll-changes').flush([{ ...changes[0], status: 'Approved', decidedByUserId: 'up' }]);
    await settle();
    expect(rowIds()).toEqual(['c1']);
  });

  it('approve_404_refreshes_list', async () => {
    const { btn, confirm, settle, error, rowIds } = await setup('ud', ['HRApprover']);
    btn('c1', 'approve')!.click();
    await confirm();
    http.expectOne('/api/payroll-changes/c1/approve').flush(
      { title: 'Nie znaleziono', status: 404 },
      { status: 404, statusText: 'Not Found' },
    );
    await settle();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/payroll-changes').flush([changes[3]]);
    await settle();
    expect(rowIds()).toEqual(['c4']);
  });

  it('reject_404_refreshes_list', async () => {
    const { btn, settle, save, type, error, rowIds } = await setup('ud', ['HRApprover']);
    btn('c1', 'reject')!.click();
    await settle();
    type('x');
    save().click();
    http.expectOne('/api/payroll-changes/c1/reject').flush(
      { title: 'Nie znaleziono', status: 404 },
      { status: 404, statusText: 'Not Found' },
    );
    await settle();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/payroll-changes').flush([changes[3]]);
    await settle();
    expect(rowIds()).toEqual(['c4']);
  });

  it('submit_404_refreshes_list', async () => {
    const { btn, confirm, settle, error, rowIds } = await setup('ud', ['HR']);
    btn('c4', 'submit')!.click();
    await confirm();
    http.expectOne('/api/payroll-changes/c4/submit').flush(
      { title: 'Nie znaleziono', status: 404 },
      { status: 404, statusText: 'Not Found' },
    );
    await settle();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/payroll-changes').flush([changes[0]]);
    await settle();
    expect(rowIds()).toEqual(['c1']);
  });

  it('approve_403_refreshes_list', async () => {
    const { btn, confirm, settle, error, rowIds } = await setup('ud', ['HRApprover']);
    btn('c1', 'approve')!.click();
    await confirm();
    http.expectOne('/api/payroll-changes/c1/approve').flush(
      { title: 'Brak uprawnień', status: 403 },
      { status: 403, statusText: 'Forbidden' },
    );
    await settle();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/payroll-changes').flush([changes[3]]);
    await settle();
    expect(rowIds()).toEqual(['c4']);
  });

  it('load_error_shows_retry_that_reloads', async () => {
    const { el, settle, fixture } = await setup('ud', ['HR']);
    fixture.componentInstance['selectEmployee']('e1');
    http.expectOne('/api/payroll-changes?employeeId=e1').flush({ title: 'Awaria', status: 500 }, { status: 500, statusText: 'x' });
    await settle();
    expect(el.querySelector('[role="alert"]')).not.toBeNull();
    (el.querySelector('[data-retry]') as HTMLButtonElement).click();
    http.expectOne('/api/payroll-changes?employeeId=e1').flush([changes[0]]);
    await settle();
    expect(el.querySelector('[role="alert"]')).toBeNull();
    expect(el.querySelector('tr[data-row="c1"]')).not.toBeNull();
  });

  it('reject_without_reason_sends_nothing', async () => {
    const { btn, settle, save, type, dialogEl } = await setup('ud', ['HRApprover']);
    btn('c1', 'reject')!.click();
    await settle();
    type('   ');
    save().click();
    await settle();
    http.expectNone('/api/payroll-changes/c1/reject');
    expect(dialogEl()!.textContent).toContain('Podaj uzasadnienie odrzucenia.');
  });

  it('reject_with_reason_posts_trimmed_reason_and_refreshes', async () => {
    const { btn, settle, save, type, info } = await setup('ud', ['HRApprover']);
    btn('c1', 'reject')!.click();
    await settle();
    type(' Za dużo ');
    save().click();
    const post = http.expectOne('/api/payroll-changes/c1/reject');
    expect(post.request.body).toEqual({ reason: 'Za dużo' });
    post.flush({ ...changes[0], status: 'Rejected' });
    await settle();
    http.expectOne('/api/payroll-changes').flush(changes);
    await settle();
    expect(info).toHaveBeenCalled();
  });

  it('reject_422_stays_in_dialog_and_unblocks_close', async () => {
    const { btn, settle, save, type, dialogEl } = await setup('ud', ['HRApprover']);
    btn('c1', 'reject')!.click();
    await settle();
    type('x');
    save().click();
    save().click();
    await settle();
    const dialog = TestBed.inject(MatDialog).openDialogs[0];
    expect(dialog.disableClose).toBe(true);
    http.expectOne('/api/payroll-changes/c1/reject').flush(
      { title: 'Walidacja', status: 422, detail: 'Niepoprawne uzasadnienie.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await settle();
    expect(dialogEl()!.querySelector('[role="alert"]')!.textContent).toContain('Niepoprawne uzasadnienie.');
    expect(dialog.disableClose).toBe(false);
    expect(save().disabled).toBe(false);
  });

  it('reject_409_closes_dialog_shows_snackbar_refreshes_and_row_usable', async () => {
    const { btn, settle, save, type, dialogEl, error, rowIds } = await setup('ud', ['HRApprover']);
    btn('c1', 'reject')!.click();
    await settle();
    type('x');
    save().click();
    http.expectOne('/api/payroll-changes/c1/reject').flush(
      { title: 'Konflikt', status: 409, detail: 'Decyzja już zapadła.' },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();
    expect(dialogEl()).toBeNull();
    expect(error).toHaveBeenCalledTimes(1);
    http.expectOne('/api/payroll-changes').flush(changes);
    await settle();
    expect(rowIds().length).toBe(4);
    expect(btn('c1', 'reject')!.disabled).toBe(false);
  });

  it('new_change_button_only_for_hr', async () => {
    const hr = await setup('uh', ['HR']);
    expect(hr.el.querySelector('[data-new]')).not.toBeNull();
    TestBed.resetTestingModule();
    const approver = await setup('ud', ['HRApprover']);
    expect(approver.el.querySelector('[data-new]')).toBeNull();
  });

  it('new_change_dialog_success_refreshes_list_and_is_single_instance', async () => {
    const { el, settle } = await setup('uh', ['HR']);
    (el.querySelector('[data-new]') as HTMLButtonElement).click();
    await settle();
    expect(document.querySelectorAll('app-new-change-dialog').length).toBe(1);
    const dialog = TestBed.inject(MatDialog).openDialogs[0];
    dialog.close(changes[3]);
    await settle();
    http.expectOne('/api/payroll-changes').flush(changes);
    await settle();
  });

  it('status_cells_render_chips_with_matching_tones', async () => {
    const { row } = await setup('ud', ['HR']);
    const tone = (id: string) => row(id)!.querySelector('app-status-chip .status-chip')!.getAttribute('data-tone');

    expect([tone('c1'), tone('c2'), tone('c3'), tone('c4')]).toEqual(['warning', 'success', 'danger', 'neutral']);
    expect(row('c1')!.querySelector('app-status-chip')!.textContent!.trim()).toBe('Oczekuje na zatwierdzenie');
  });

  it('money_column_is_right_aligned', async () => {
    const { row } = await setup('ud', ['HR']);
    expect(row('c1')!.querySelector('td.num')!.textContent).toMatch(/7\s?800,00\s?zł/);
  });
});
