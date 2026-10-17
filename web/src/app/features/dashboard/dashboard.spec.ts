import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../../api/api-configuration';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { TimesheetAnalysisDto } from '../../api/models/timesheet-analysis-dto';
import { SessionService } from '../../core/session';
import { Dashboard } from './dashboard';

const balance = { entitlementDays: 20, approvedDays: 10, pendingDays: 5, remainingDays: 5 };

function analysis(overrides: Partial<TimesheetAnalysisDto> = {}): TimesheetAnalysisDto {
  return {
    annualOvertimeLimitExceeded: false,
    normHours: 112,
    overtime: { hours50: 3, hours100: 2, ruleIds: [], total: 5 },
    overtimeYearToDate: 5,
    restViolations: [],
    weeklyAverage: { averageWeeklyHours: 40, exceedsLimit: false, ruleId: 'R' },
    workedHours: 120.5,
    ...overrides,
  };
}

function leave(id: string, employeeId: string, status: string): LeaveRequestDto {
  return {
    id, employeeId, status, days: 1, from: '2026-11-02', to: '2026-11-02',
    createdAtUtc: '2026-10-01T00:00:00Z', decidedByEmployeeId: null, decisionReason: null,
  };
}

describe('Dashboard', () => {
  let http: HttpTestingController;

  function setup(...roles: string[]) {
    const user = signal<CurrentUserDto | null>({
      userId: 'u1', employeeId: 'e1', userName: 'x', displayName: 'X', roles,
    });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideRouter([]),
        {
          provide: SessionService,
          useValue: { user: user.asReadonly(), hasRole: (...r: string[]) => r.some((x) => roles.includes(x)) },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, el, settle };
  }

  const analysisReq = (): TestRequest =>
    http.expectOne((r) => r.url === '/api/timesheet/analysis' && r.params.get('from') === '2026-10-01' && r.params.get('to') === '2026-10-16');

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
  });

  afterEach(() => {
    vi.useRealTimers();
    http.verify();
  });

  it('employee_renders_balance_month_and_alerts', async () => {
    const { el, settle } = setup('Employee');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(
      analysis({ restViolations: [
        { from: '2026-10-05T20:00:00', to: '2026-10-06T06:00:00', requiredHours: 11, restHours: 10, ruleId: 'R1' },
        { from: '2026-10-07T20:00:00', to: '2026-10-08T06:00:00', requiredHours: 11, restHours: 9, ruleId: 'R1' },
        { from: '2026-10-09T20:00:00', to: '2026-10-10T06:00:00', requiredHours: 11, restHours: 8, ruleId: 'R1' },
      ] }),
    );
    await settle();

    const tile = (name: string) => el.querySelector(`[data-tile="${name}"]`)!.textContent!;
    const bal = tile('balance');
    expect(bal).toContain('Saldo urlopu');
    for (const v of ['20', '10', '5']) expect(bal).toContain(v);
    expect(el.querySelector('[data-value="remaining"]')!.textContent!.trim()).toBe('5');
    expect(el.querySelector('[data-value="entitlement"]')!.textContent!.trim()).toBe('20');
    expect(el.querySelector('[data-value="approved"]')!.textContent!.trim()).toBe('10');
    expect(el.querySelector('[data-value="pending"]')!.textContent!.trim()).toBe('5');
    expect(tile('month')).toContain('120,5');
    expect(tile('month')).toContain('112,0');
    expect(el.querySelector('[data-value="overtime50"]')!.textContent).toContain('3,0');
    expect(el.querySelector('[data-value="overtime100"]')!.textContent).toContain('2,0');
    expect(el.querySelector('[data-value="rest-violations"]')!.textContent!.trim()).toBe('3');
    expect(tile('alerts')).not.toContain('Brak alertów');
    expect(el.querySelector('[data-tile="approvals"]')).toBeNull();
    http.expectNone('/api/leave/requests');
  });

  it('alerts_show_none_when_zero', async () => {
    const { el, settle } = setup('Employee');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(analysis());
    await settle();
    expect(el.querySelector('[data-tile="alerts"]')!.textContent).toContain('Brak alertów');
  });

  it('alerts_flag_weekly_average_and_annual_limit', async () => {
    const { el, settle } = setup('Employee');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(
      analysis({
        annualOvertimeLimitExceeded: true,
        weeklyAverage: { averageWeeklyHours: 50, exceedsLimit: true, ruleId: 'R' },
      }),
    );
    await settle();
    const text = el.querySelector('[data-tile="alerts"]')!.textContent!;
    expect(text).not.toContain('Brak alertów');
    expect(text).toContain('48 h');
    expect(text).toContain('150 h');
  });

  it('manager_counts_only_pending_requests_of_others', async () => {
    const { el, settle } = setup('Manager');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(analysis());
    http.expectOne('/api/leave/requests').flush([
      leave('1', 'e2', 'Pending'),
      leave('2', 'e3', 'Pending'),
      leave('3', 'e1', 'Pending'),
      leave('4', 'e2', 'Approved'),
    ]);
    await settle();
    const tile = el.querySelector('[data-tile="approvals"]')!;
    expect(tile.textContent).toContain('Do zatwierdzenia: 2');
    expect(tile.querySelector('a')!.getAttribute('href')).toBe('/urlopy/zatwierdzanie');
  });

  it('shows_loading_state_before_responses', () => {
    const { el } = setup('Manager');
    expect(el.querySelectorAll('mat-progress-bar, [role="progressbar"]').length).toBeGreaterThanOrEqual(3);
    http.expectOne('/api/leave/balance');
    analysisReq();
    http.expectOne('/api/leave/requests');
  });

  it('failing_tile_does_not_block_others_and_retry_refetches_only_it', async () => {
    const { el, settle } = setup('Employee');
    http.expectOne('/api/leave/balance').flush({ title: 'boom' }, { status: 500, statusText: 'Server Error' });
    analysisReq().flush(analysis());
    await settle();

    expect(el.querySelector('[data-tile="month"]')!.textContent).toContain('120,5');
    const balanceTile = el.querySelector('[data-tile="balance"]')!;
    const retry = Array.from(balanceTile.querySelectorAll('button')).find((b) => b.textContent!.includes('Spróbuj ponownie'))!;
    expect(retry).toBeTruthy();
    expect(el.querySelector('[data-tile="month"] button')).toBeNull();

    retry.click();
    http.expectOne('/api/leave/balance').flush(balance);
    await settle();
    expect(el.querySelector('[data-tile="balance"]')!.textContent).toContain('Saldo urlopu');
    expect(el.querySelector('[data-value="remaining"]')!.textContent!.trim()).toBe('5');
    expect(el.querySelector('[data-tile="balance"] button')).toBeNull();
  });

  it.each([
    ['balance', 'beach_access'],
    ['month', 'schedule'],
    ['alerts', 'warning'],
    ['approvals', 'task_alt'],
  ])('tile_%s_has_icon_%s', async (tile, icon) => {
    const { el, settle } = setup('Manager');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(analysis());
    http.expectOne((r) => r.url === '/api/leave/requests').flush([]);
    await settle();

    const found = el.querySelector(`[data-tile="${tile}"] .page-card__icon mat-icon`)!;
    expect(found.getAttribute('fonticon')).toBe(icon);
  });

  it('alerts_icon_is_green_without_alerts_and_red_with_them', async () => {
    const quiet = setup('Employee');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(analysis());
    await quiet.settle();
    expect(quiet.el.querySelector('[data-tile="alerts"] .page-card__icon')!.classList).toContain('page-card__icon--success');
    TestBed.resetTestingModule();

    const noisy = setup('Employee');
    http.expectOne('/api/leave/balance').flush(balance);
    analysisReq().flush(analysis({ annualOvertimeLimitExceeded: true }));
    await noisy.settle();
    expect(noisy.el.querySelector('[data-tile="alerts"] .page-card__icon')!.classList).toContain('page-card__icon--danger');
  });
});
