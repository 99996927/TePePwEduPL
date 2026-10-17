import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { formatHours } from '../../core/date-format';
import { formatInstantDateTime } from '../../core/date-format';
import { formatMoney } from '../../core/format';
import { PayrollCard } from './payroll-card';

const result: PayrollResultDto = {
  employeeId: 'e1',
  year: 2026,
  month: 10,
  baseSalary: 8000,
  fixedAllowance: 500,
  normHours: 184,
  overtime50Hours: 4.5,
  overtime100Hours: 2,
  normalOvertimePay: 300.25,
  allowance50: 120,
  allowance100: 80,
  gross: 9000.5,
  ruleIds: ['R-12', 'R-34'],
  calculatedAtUtc: '2026-10-16T10:30:00Z',
};

describe('PayrollCard', () => {
  let fixture: ComponentFixture<PayrollCard>;
  let text: string;

  beforeEach(async () => {
    fixture = TestBed.createComponent(PayrollCard);
    fixture.componentRef.setInput('result', result);
    fixture.componentRef.setInput('name', 'Anna Nowak');
    fixture.detectChanges();
    await fixture.whenStable();
    text = (fixture.nativeElement as HTMLElement).textContent ?? '';
  });

  it('shows_name_and_labelled_components', () => {
    for (const label of [
      'Anna Nowak',
      'Pensja zasadnicza',
      'Dodatek stały',
      'Norma czasu (h)',
      'Nadgodziny 50% (h)',
      'Nadgodziny 100% (h)',
      'Wynagrodzenie za nadgodziny',
      'Dodatek 50%',
      'Dodatek 100%',
      'Brutto',
    ]) {
      expect(text).toContain(label);
    }
  });

  it('formats_money_and_hours', () => {
    for (const v of [8000, 500, 300.25, 120, 80, 9000.5]) expect(text).toContain(formatMoney(v));
    for (const v of [184, 4.5, 2]) expect(text).toContain(formatHours(v));
  });

  it('shows_rule_ids_and_calculation_time', () => {
    expect(text).toContain('R-12');
    expect(text).toContain('R-34');
    expect(text).toContain(`Obliczono: ${formatInstantDateTime(result.calculatedAtUtc)}`);
  });
});
