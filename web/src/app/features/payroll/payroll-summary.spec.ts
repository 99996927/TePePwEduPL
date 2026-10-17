import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideApiConfiguration } from '../../api/api-configuration';
import { EmployeeDto } from '../../api/models/employee-dto';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { formatMoney } from '../../core/format';
import { PayrollSummary } from './payroll-summary';

const people: EmployeeDto[] = Array.from({ length: 7 }, (_, i) => ({
  id: `e${i + 1}`,
  firstName: `Imie${i + 1}`,
  lastName: `Nazwisko${i + 1}`,
  hireDate: '2020-01-01',
  teamId: 't1',
  teamName: 'Platforma',
  userId: null,
}));

function result(employeeId: string, gross = 9000): PayrollResultDto {
  return {
    employeeId,
    year: 2026,
    month: 10,
    baseSalary: 8000,
    fixedAllowance: 500,
    normHours: 184,
    overtime50Hours: 4,
    overtime100Hours: 2,
    normalOvertimePay: 300,
    allowance50: 120,
    allowance100: 80,
    gross,
    ruleIds: [],
    calculatedAtUtc: '2026-10-16T10:30:00Z',
  };
}

describe('PayrollSummary', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<PayrollSummary>;
  let el: HTMLElement;

  async function setup(list: EmployeeDto[] = people) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(PayrollSummary);
    fixture.componentRef.setInput('month', new Date(2026, 9, 1));
    fixture.componentRef.setInput('employees', list);
    el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    await settle();
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const pending = (): TestRequest[] => http.match((r) => r.url === '/api/payroll/results');
  const idOf = (r: TestRequest) => r.request.params.get('employeeId');

  afterEach(() => http.verify());

  it('fetches_each_person_with_at_most_four_in_flight', async () => {
    await setup();
    let inFlight = pending();
    let max = inFlight.length;
    expect(inFlight).toHaveLength(4);
    const seen: string[] = [];
    while (inFlight.length > 0) {
      const done = inFlight[0];
      seen.push(idOf(done)!);
      expect(done.request.params.get('year')).toBe('2026');
      expect(done.request.params.get('month')).toBe('10');
      done.flush(result(idOf(done)!));
      await settle();
      inFlight = [...inFlight.slice(1), ...pending()];
      max = Math.max(max, inFlight.length);
    }
    expect(max).toBe(4);
    expect(seen.sort()).toEqual(people.map((p) => p.id).sort());
  });

  it('shows_gross_and_total_overtime', async () => {
    await setup(people.slice(0, 1));
    pending()[0].flush(result('e1', 9123.5));
    await settle();
    const row = el.querySelector('[data-row="e1"]')!.textContent!;
    expect(row).toContain('Imie1 Nazwisko1');
    expect(row).toContain('Platforma');
    expect(row).toContain(formatMoney(9123.5));
    expect(row).toContain('6,0 h');
  });

  it('404_shows_dash', async () => {
    await setup(people.slice(0, 1));
    pending()[0].flush({ title: 'Nie znaleziono' }, { status: 404, statusText: 'Not Found' });
    await settle();
    const row = el.querySelector('[data-row="e1"]')!;
    expect(row.textContent).toContain('—');
    expect(row.querySelector('[data-retry]')).toBeNull();
  });

  it('one_failure_shows_retry_without_affecting_others_and_retry_sends_one_request', async () => {
    await setup(people.slice(0, 2));
    const [a, b] = pending();
    const fail = idOf(a) === 'e1' ? a : b;
    const ok = fail === a ? b : a;
    fail.flush({ title: 'Błąd', status: 500, detail: 'Nie udało się pobrać.' }, { status: 500, statusText: 'Error' });
    ok.flush(result('e2'));
    await settle();
    expect(el.querySelector('[data-row="e1"]')!.textContent).toContain('Spróbuj ponownie');
    expect(el.querySelector('[data-row="e1"]')!.textContent).toContain('Nie udało się pobrać.');
    expect(el.querySelector('[data-row="e2"]')!.textContent).toContain(formatMoney(9000));
    (el.querySelector('[data-retry]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const retry = pending();
    expect(retry).toHaveLength(1);
    expect(idOf(retry[0])).toBe('e1');
    retry[0].flush(result('e1'));
    await settle();
    expect(el.querySelector('[data-row="e1"]')!.textContent).toContain(formatMoney(9000));
  });

  it('row_click_and_enter_emit_selected_but_retry_does_not', async () => {
    await setup(people.slice(0, 2));
    const emitted: string[] = [];
    fixture.componentInstance.selected.subscribe((id) => emitted.push(id));
    const [a, b] = pending();
    const fail = idOf(a) === 'e1' ? a : b;
    const ok = fail === a ? b : a;
    fail.flush({ title: 'x' }, { status: 500, statusText: 'Error' });
    ok.flush(result('e2'));
    await settle();
    (el.querySelector('[data-row="e2"]') as HTMLElement).click();
    expect(emitted).toEqual(['e2']);
    el.querySelector('[data-row="e2"]')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(emitted).toEqual(['e2', 'e2']);
    const retry = el.querySelector('[data-retry]') as HTMLButtonElement;
    retry.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    expect(emitted).toEqual(['e2', 'e2']);
    pending().forEach((r) => r.flush(result('e1')));
  });

  it('destroying_cancels_pending_requests_and_issues_no_more', async () => {
    await setup();
    const inFlight = pending();
    expect(inFlight).toHaveLength(4);
    fixture.destroy();
    expect(inFlight.every((r) => r.cancelled)).toBe(true);
    await vi.waitFor(() => expect(pending()).toHaveLength(0));
  });

  it('run_result_replaces_rows', async () => {
    await setup(people.slice(0, 2));
    pending().forEach((r) => r.flush(result(idOf(r)!, 1000)));
    await settle();
    fixture.componentRef.setInput('runResult', [result('e1', 7777), result('e2', 8888)]);
    await settle();
    expect(pending()).toHaveLength(0);
    expect(el.querySelector('[data-row="e1"]')!.textContent).toContain(formatMoney(7777));
    expect(el.querySelector('[data-row="e2"]')!.textContent).toContain(formatMoney(8888));
  });
});
