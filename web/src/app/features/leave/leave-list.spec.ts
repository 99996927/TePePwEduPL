import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { provideNativeDateAdapter } from '@angular/material/core';
import { provideApiConfiguration } from '../../api/api-configuration';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { SessionService } from '../../core/session';
import { LeaveList } from './leave-list';

const balance = { entitlementDays: 26, approvedDays: 10, pendingDays: 3, remainingDays: 13 };
const employees = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski' },
];

function req(id: string, employeeId: string, status: string, from: string, reason: string | null = null): LeaveRequestDto {
  return {
    id, employeeId, status, from, to: from, days: 1,
    createdAtUtc: '2026-10-01T00:00:00Z', decidedByEmployeeId: null, decisionReason: reason,
  };
}

const requests = [
  req('r1', 'e1', 'Pending', '2026-11-02'),
  req('r2', 'e1', 'Approved', '2026-12-07'),
  req('r3', 'e2', 'Pending', '2026-11-09'),
  req('r4', 'e1', 'Rejected', '2026-10-05', 'Brak obsady'),
  req('r5', 'e1', 'Cancelled', '2026-09-01'),
];

/** Lets chained promises in the component settle, then renders. */
async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

describe('LeaveList', () => {
  let http: HttpTestingController;

  async function setup(list: LeaveRequestDto[] = requests) {
    const user = signal<CurrentUserDto | null>({
      userId: 'u1', employeeId: 'e1', userName: 'x', displayName: 'X', roles: ['Employee'],
    });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: SessionService, useValue: { user: user.asReadonly(), hasRole: () => false } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(LeaveList);
    fixture.detectChanges();
    http.expectOne('/api/employees').flush(employees);
    http.expectOne('/api/leave/balance').flush(balance);
    http.expectOne('/api/leave/requests').flush(list);
    await flush(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    const settle = () => flush(fixture);
    return { fixture, el, loader, settle };
  }

  afterEach(() => http.verify());

  const rows = (el: HTMLElement) => Array.from(el.querySelectorAll('tr[data-row]'));

  it('renders_rows_newest_first_with_polish_statuses_and_names', async () => {
    const { el } = await setup();
    const r = rows(el);
    expect(r.map((x) => x.getAttribute('data-row'))).toEqual(['r2', 'r3', 'r1', 'r4', 'r5']);
    const first = r[0].textContent!;
    expect(first).toContain('Anna Nowak');
    expect(first).toContain('07.12.2026');
    expect(first).toContain('Zatwierdzony');
    expect(r[1].textContent).toContain('Jan Kowalski');
    expect(r[1].textContent).toContain('Oczekuje');
    expect(r[3].textContent).toContain('Odrzucony');
    expect(r[3].textContent).toContain('Brak obsady');
    expect(r[4].textContent).toContain('Anulowany');
  });

  it('shows_balance_in_header', async () => {
    const { el } = await setup();
    const header = el.querySelector('[data-balance]')!.textContent!;
    expect(header).toContain('Pozostałe');
    expect(el.querySelector('[data-value="remaining"]')!.textContent!.trim()).toBe('13');
  });

  it('shows_empty_state_without_requests', async () => {
    const { el } = await setup([]);
    expect(el.textContent).toContain('Brak wniosków');
    expect(rows(el).length).toBe(0);
  });

  it('status_filter_hides_other_statuses', async () => {
    const { el, loader, settle } = await setup();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Oczekuje' });
    await settle();
    expect(rows(el).map((x) => x.getAttribute('data-row'))).toEqual(['r3', 'r1']);
    expect(el.textContent).not.toContain('Zatwierdzony');
  });

  it('cancel_button_only_for_own_pending_requests', async () => {
    const { el } = await setup();
    const withCancel = rows(el).filter((x) => x.querySelector('[data-cancel]')).map((x) => x.getAttribute('data-row'));
    expect(withCancel).toEqual(['r1']);
  });

  it('confirming_cancel_posts_and_refreshes_list', async () => {
    const { el, fixture, settle } = await setup();
    (el.querySelector('[data-row="r1"] [data-cancel]') as HTMLButtonElement).click();
    await settle();
    const dialog = document.querySelector('app-confirm-dialog')!;
    expect(dialog).toBeTruthy();
    const confirm = Array.from(dialog.querySelectorAll('button')).find((b) => b.textContent!.includes('Anuluj wniosek'))!;
    confirm.click();
    const post = await vi.waitFor(() => http.expectOne('/api/leave/requests/r1/cancel'));
    expect(post.request.method).toBe('POST');
    post.flush(req('r1', 'e1', 'Cancelled', '2026-11-02'));
    await flush(fixture);
    http.expectOne('/api/leave/requests').flush([req('r1', 'e1', 'Cancelled', '2026-11-02')]);
    http.expectOne('/api/leave/balance').flush(balance);
    await settle();
    expect(rows(el).length).toBe(1);
    expect(el.querySelector('[data-cancel]')).toBeNull();
    expect(el.textContent).toContain('Anulowany');
  });

  it('declining_confirmation_sends_nothing', async () => {
    const { el, loader, settle } = await setup();
    (el.querySelector('[data-row="r1"] [data-cancel]') as HTMLButtonElement).click();
    await settle();
    const dialog = document.querySelector('app-confirm-dialog')!;
    const no = Array.from(dialog.querySelectorAll('button')).find((b) => b.textContent!.trim() === 'Anuluj')!;
    no.click();
    await vi.waitFor(async () => expect(await loader.getAllHarnesses(MatDialogHarness)).toHaveLength(0));
    http.expectNone((r) => r.method === 'POST');
    expect(rows(el).length).toBe(5);
  });

  it('new_request_button_opens_dialog', async () => {
    const { el, loader, settle } = await setup();
    (el.querySelector('[data-new]') as HTMLButtonElement).click();
    await settle();
    expect(await loader.getAllHarnesses(MatDialogHarness)).toHaveLength(1);
    expect(document.querySelector('app-new-leave-dialog')).toBeTruthy();
  });

  it('status_cells_render_chips_with_matching_tones', async () => {
    const { el } = await setup();
    const tones = rows(el).map((x) => x.querySelector('app-status-chip .status-chip')!.getAttribute('data-tone'));

    expect(tones).toEqual(['success', 'warning', expect.any(String), 'danger', 'neutral']);
    expect(rows(el)[0].querySelector('app-status-chip')!.textContent!.trim()).toBe('Zatwierdzony');
  });
});
