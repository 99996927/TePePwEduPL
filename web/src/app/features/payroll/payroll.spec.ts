import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { provideApiConfiguration } from '../../api/api-configuration';
import { EmployeeDto } from '../../api/models/employee-dto';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { formatMoney } from '../../core/format';
import { SessionService } from '../../core/session';
import { Payroll } from './payroll';

const employees: EmployeeDto[] = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'Platforma', userId: 'u1' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2021-01-01', teamId: 't1', teamName: 'Platforma', userId: 'u2' },
];

function result(employeeId: string, month: number, gross: number): PayrollResultDto {
  return {
    employeeId,
    year: 2026,
    month,
    baseSalary: 8000,
    fixedAllowance: 500,
    normHours: 184,
    overtime50Hours: 4,
    overtime100Hours: 2,
    normalOvertimePay: 300,
    allowance50: 120,
    allowance100: 80,
    gross,
    ruleIds: ['R-1'],
    calculatedAtUtc: '2026-10-16T10:30:00Z',
  };
}

describe('Payroll', () => {
  let http: HttpTestingController;

  async function setup(roles: string[]) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
    const user = signal({ userId: 'u1', employeeId: 'e1', userName: 'x', displayName: 'X', roles });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        {
          provide: SessionService,
          useValue: { user: user.asReadonly(), hasRole: (...r: string[]) => r.some((x) => roles.includes(x)) },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Payroll);
    fixture.detectChanges();
    if (roles.includes('HR')) {
      http.expectOne('/api/employees').flush(employees);
      await fixture.whenStable();
      fixture.detectChanges();
      drainSummary();
    }
    const el = fixture.nativeElement as HTMLElement;
    const loader = TestbedHarnessEnvironment.loader(fixture);
    const rootLoader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    return { fixture, el, loader, rootLoader };
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const payrollReq = (month: number, employeeId?: string) =>
    http.expectOne(
      (r) =>
        r.url === '/api/payroll/results' &&
        r.params.get('year') === '2026' &&
        r.params.get('month') === String(month) &&
        r.params.get('employeeId') === (employeeId ?? null),
    );

  /** HR page also loads one summary row per person; answer those with 404 so they stay out of the way. */
  const drainSummary = () =>
    http
      .match((r) => r.url === '/api/payroll/results' && r.params.has('employeeId'))
      .forEach((r) => r.flush({ title: 'Nie znaleziono' }, { status: 404, statusText: 'Not Found' }));

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('employee_requests_own_payroll_without_employee_id_and_shows_card', async () => {
    const { fixture, el } = await setup(['Employee']);
    const req = payrollReq(10);
    expect(req.request.params.has('employeeId')).toBe(false);
    req.flush(result('e1', 10, 9000));
    await settle(fixture);
    expect(el.querySelector('app-payroll-card')).not.toBeNull();
    expect(el.textContent).toContain('Brutto');
    expect(el.querySelector('app-employee-picker')).toBeNull();
  });

  it('shows_not_found_info_on_404_without_retry', async () => {
    const { fixture, el } = await setup(['Employee']);
    payrollReq(10).flush({ title: 'Nie znaleziono' }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);
    expect(el.textContent).toContain('Brak naliczenia za ten miesiąc.');
    expect(el.querySelector('[data-retry]')).toBeNull();
    expect(el.querySelector('[role="alert"]')).toBeNull();
    expect(el.querySelector('app-payroll-card')).toBeNull();
  });

  it('shows_error_on_500_and_retry_works', async () => {
    const { fixture, el } = await setup(['Employee']);
    payrollReq(10).flush({ title: 'Błąd serwera' }, { status: 500, statusText: 'Server Error' });
    await settle(fixture);
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Błąd serwera');
    expect(el.textContent).not.toContain('Brak naliczenia za ten miesiąc.');
    const retry = el.querySelector<HTMLButtonElement>('[data-retry]');
    expect(retry?.textContent).toContain('Spróbuj ponownie');
    retry?.click();
    payrollReq(10).flush(result('e1', 10, 9000));
    await settle(fixture);
    expect(el.querySelector('app-payroll-card')).not.toBeNull();
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  it('hr_sees_picker_and_selecting_other_employee_sends_employee_id', async () => {
    const { fixture, el, loader } = await setup(['HR']);
    payrollReq(10).flush(result('e1', 10, 9000));
    await settle(fixture);
    expect(el.querySelector('app-employee-picker')).not.toBeNull();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    const options = await select.getOptions();
    await options[1].click();
    payrollReq(10, 'e2').flush(result('e2', 10, 7000));
    await settle(fixture);
    expect(el.textContent).toContain('Jan Kowalski');
  });

  it('switching_month_requests_september', async () => {
    const { fixture, el } = await setup(['Employee']);
    payrollReq(10).flush(result('e1', 10, 9000));
    await settle(fixture);
    el.querySelector<HTMLButtonElement>('[aria-label="Poprzedni miesiąc"]')?.click();
    payrollReq(9).flush(result('e1', 9, 8500));
    await settle(fixture);
    expect(el.querySelector('app-payroll-card')).not.toBeNull();
  });

  it('ignores_stale_response_after_selection_change', async () => {
    const { fixture, el } = await setup(['Employee']);
    const first = payrollReq(10);
    await settle(fixture);
    el.querySelector<HTMLButtonElement>('[aria-label="Poprzedni miesiąc"]')?.click();
    const second = payrollReq(9);
    second.flush(result('e1', 9, 1111));
    await settle(fixture);
    first.flush(result('e1', 10, 9999));
    await settle(fixture);
    expect(el.textContent).toContain(formatMoney(1111));
    expect(el.textContent).not.toContain(formatMoney(9999));
  });

  it('always_shows_demo_disclaimer', async () => {
    const { fixture, el } = await setup(['Employee']);
    payrollReq(10).flush({ title: 'x' }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);
    expect(el.textContent).toContain('Naliczenie ma charakter wstępny i demonstracyjny.');
  });

  describe('hr summary and run', () => {
    const runReq = () => http.match((r) => r.url === '/api/payroll/runs');

    async function hrSetup() {
      const ctx = await setup(['HR']);
      payrollReq(10).flush(result('e1', 10, 9000));
      drainSummary();
      await settle(ctx.fixture);
      return ctx;
    }

    async function clickRun(ctx: Awaited<ReturnType<typeof hrSetup>>) {
      ctx.el.querySelector<HTMLButtonElement>('[data-run]')!.click();
      ctx.fixture.detectChanges();
      return ctx.rootLoader.getHarness(MatDialogHarness);
    }

    it('hr_sees_summary_and_run_button_but_employee_does_not', async () => {
      const hr = await hrSetup();
      expect(hr.el.querySelector('app-payroll-summary')).not.toBeNull();
      expect(hr.el.querySelector('[data-run]')?.textContent).toContain('Uruchom naliczenie');
      TestBed.resetTestingModule();
      const emp = await setup(['Employee']);
      payrollReq(10).flush(result('e1', 10, 9000));
      await settle(emp.fixture);
      expect(emp.el.querySelector('app-payroll-summary')).toBeNull();
      expect(emp.el.querySelector('[data-run]')).toBeNull();
    });

    it('decline_sends_nothing_and_current_month_dialog_shows_warning', async () => {
      const ctx = await hrSetup();
      const dialog = await clickRun(ctx);
      expect(await dialog.getText()).toContain('Miesiąc jeszcze trwa. Naliczenie będzie wstępne i można je powtórzyć.');
      await (await ctx.rootLoader.getHarness(MatDialogHarness)).close();
      await settle(ctx.fixture);
      expect(runReq()).toHaveLength(0);
      expect(ctx.el.querySelector<HTMLButtonElement>('[data-run]')!.disabled).toBe(false);
    });

    it('past_month_dialog_has_no_warning', async () => {
      const ctx = await hrSetup();
      ctx.el.querySelector<HTMLButtonElement>('[aria-label="Poprzedni miesiąc"]')!.click();
      ctx.fixture.detectChanges();
      payrollReq(9).flush(result('e1', 9, 8500));
      drainSummary();
      await settle(ctx.fixture);
      const dialog = await clickRun(ctx);
      expect(await dialog.getText()).not.toContain('Miesiąc jeszcze trwa');
      await dialog.close();
    });

    async function confirm(ctx: Awaited<ReturnType<typeof hrSetup>>) {
      const dialog = await clickRun(ctx);
      const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('app-confirm-dialog button'));
      buttons.find((b) => b.textContent?.includes('Uruchom'))!.click();
      await dialog.getText().catch(() => '');
    }

    it('confirm_posts_year_month_and_success_shows_info_and_refreshes_card', async () => {
      const ctx = await hrSetup();
      await confirm(ctx);
      const [req] = await vi.waitFor(() => {
        const r = runReq();
        expect(r).toHaveLength(1);
        return r;
      });
      expect(req.request.body).toEqual({ year: 2026, month: 10 });
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector<HTMLButtonElement>('[data-run]')!.disabled).toBe(true);
      req.flush(Array.from({ length: 31 }, (_, i) => result(i === 0 ? 'e1' : `x${i}`, 10, 9500)));
      await settle(ctx.fixture);
      expect(document.body.textContent).toContain('Naliczono 31 pracowników.');
      payrollReq(10).flush(result('e1', 10, 9500));
      await settle(ctx.fixture);
      expect(ctx.el.querySelector('[data-row="e1"]')!.textContent).toContain(formatMoney(9500));
    });

    it('422_shows_persistent_alert_and_button_is_active_again', async () => {
      const ctx = await hrSetup();
      await confirm(ctx);
      const [req] = await vi.waitFor(() => {
        const r = runReq();
        expect(r).toHaveLength(1);
        return r;
      });
      ctx.fixture.detectChanges();
      expect(ctx.el.querySelector<HTMLButtonElement>('[data-run]')!.disabled).toBe(true);
      req.flush(
        { title: 'Błąd', status: 422, detail: 'Nieprawidłowa ewidencja pracownika Jan Kowalski: brak wyjścia.' },
        { status: 422, statusText: 'Unprocessable' },
      );
      await settle(ctx.fixture);
      const alert = ctx.el.querySelector('[data-run-error][role="alert"]');
      expect(alert?.textContent).toContain('Nieprawidłowa ewidencja pracownika Jan Kowalski');
      expect(ctx.el.querySelector<HTMLButtonElement>('[data-run]')!.disabled).toBe(false);
    });

    it('403_shows_persistent_alert', async () => {
      const ctx = await hrSetup();
      await confirm(ctx);
      const [req] = await vi.waitFor(() => {
        const r = runReq();
        expect(r).toHaveLength(1);
        return r;
      });
      req.flush({ title: 'Brak uprawnień', status: 403 }, { status: 403, statusText: 'Forbidden' });
      await settle(ctx.fixture);
      expect(ctx.el.querySelector('[data-run-error][role="alert"]')?.textContent).toContain('Brak uprawnień');
      expect(ctx.el.querySelector<HTMLButtonElement>('[data-run]')!.disabled).toBe(false);
    });

    it('double_click_sends_one_request', async () => {
      const ctx = await hrSetup();
      const button = ctx.el.querySelector<HTMLButtonElement>('[data-run]')!;
      button.click();
      button.click();
      ctx.fixture.detectChanges();
      const dialogs = await ctx.rootLoader.getAllHarnesses(MatDialogHarness);
      expect(dialogs).toHaveLength(1);
      const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>('app-confirm-dialog button'));
      buttons.find((b) => b.textContent?.includes('Uruchom'))!.click();
      button.click();
      await vi.waitFor(() => expect(runReq().length).toBe(1));
      await settle(ctx.fixture);
      expect(runReq()).toHaveLength(0);
    });
  });
});
