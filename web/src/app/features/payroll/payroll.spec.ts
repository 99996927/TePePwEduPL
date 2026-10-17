import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
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
    if (roles.includes('HR')) http.expectOne('/api/employees').flush(employees);
    const el = fixture.nativeElement as HTMLElement;
    const loader = TestbedHarnessEnvironment.loader(fixture);
    return { fixture, el, loader };
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
});
