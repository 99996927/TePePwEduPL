import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../../api/api-configuration';
import { TimesheetAnalysisDto } from '../../api/models/timesheet-analysis-dto';
import { AnalysisPanel } from './analysis-panel';

function analysis(patch: Partial<TimesheetAnalysisDto> = {}): TimesheetAnalysisDto {
  return {
    normHours: 160,
    workedHours: 152.5,
    overtime: { hours50: 3, hours100: 1.5, total: 4.5, ruleIds: [] },
    restViolations: [],
    weeklyAverage: { averageWeeklyHours: 40, exceedsLimit: false, ruleId: 'KP-131' },
    overtimeYearToDate: 20,
    annualOvertimeLimitExceeded: false,
    ...patch,
  };
}

describe('AnalysisPanel', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<AnalysisPanel>;
  let el: HTMLElement;

  function setup(employeeId: string | null = 'e1', month = new Date(2026, 9, 1)) {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AnalysisPanel);
    fixture.componentRef.setInput('employeeId', employeeId);
    fixture.componentRef.setInput('month', month);
    el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const analysisReq = (): TestRequest => http.expectOne((r) => r.url === '/api/timesheet/analysis');

  afterEach(() => http.verify());

  it('requests_month_range_for_employee_and_shows_totals', async () => {
    setup();
    const req = analysisReq();
    expect(req.request.params.get('employeeId')).toBe('e1');
    expect(req.request.params.get('from')).toBe('2026-10-01');
    expect(req.request.params.get('to')).toBe('2026-10-31');
    req.flush(analysis());
    await settle();
    expect(el.querySelector('[data-value="norm"]')!.textContent).toContain('160,0 h');
    expect(el.querySelector('[data-value="worked"]')!.textContent).toContain('152,5 h');
    expect(el.querySelector('[data-value="overtime50"]')!.textContent).toContain('3,0 h');
    expect(el.querySelector('[data-value="overtime100"]')!.textContent).toContain('1,5 h');
    expect(el.querySelector('[data-value="ytd"]')!.textContent).toContain('20,0 h');
    expect(el.querySelector('[data-value="ytd"]')!.textContent).toContain('150,0 h');
  });

  it('omits_employee_id_when_null', () => {
    setup(null);
    const req = analysisReq();
    expect(req.request.params.has('employeeId')).toBe(false);
    req.flush(analysis());
  });

  it('shows_daily_rest_violation_with_hours_and_rule', async () => {
    setup();
    analysisReq().flush(
      analysis({
        restViolations: [
          { ruleId: 'KP-132-1', from: '2026-10-05T22:00:00', to: '2026-10-06T07:00:00', restHours: 9, requiredHours: 11 },
        ],
      }),
    );
    await settle();
    const text = el.querySelector('[data-violation]')!.textContent!;
    expect(text).toContain('Odpoczynek dobowy');
    expect(text).toContain('9,0 h');
    expect(text).toContain('11,0 h');
    expect(text).toContain('KP-132-1');
    expect(text).toContain('05.10.2026');
    expect(el.textContent).not.toContain('Brak naruszeń');
  });

  it('shows_weekly_rest_violation_label', async () => {
    setup();
    analysisReq().flush(
      analysis({
        restViolations: [
          { ruleId: 'KP-133-1', from: '2026-10-05T22:00:00', to: '2026-10-07T07:00:00', restHours: 30, requiredHours: 35 },
        ],
      }),
    );
    await settle();
    expect(el.querySelector('[data-violation]')!.textContent).toContain('Odpoczynek tygodniowy');
  });

  it('shows_no_violations_message_when_empty', async () => {
    setup();
    analysisReq().flush(analysis());
    await settle();
    expect(el.textContent).toContain('Brak naruszeń');
    expect(el.querySelector('[data-violation]')).toBeNull();
  });

  it('highlights_weekly_average_over_limit', async () => {
    setup();
    analysisReq().flush(
      analysis({ weeklyAverage: { averageWeeklyHours: 50.5, exceedsLimit: true, ruleId: 'KP-131' } }),
    );
    await settle();
    const avg = el.querySelector('[data-value="average"]')!;
    expect(avg.classList).toContain('exceeded');
    expect(avg.textContent).toContain('50,5 h');
    expect(avg.textContent).toContain('ponad limit 48 h');
  });

  it('does_not_highlight_weekly_average_within_limit', async () => {
    setup();
    analysisReq().flush(analysis());
    await settle();
    const avg = el.querySelector('[data-value="average"]')!;
    expect(avg.classList).not.toContain('exceeded');
    expect(avg.textContent).not.toContain('ponad limit 48 h');
  });

  it('warns_when_annual_overtime_limit_exceeded', async () => {
    setup();
    analysisReq().flush(analysis({ overtimeYearToDate: 151, annualOvertimeLimitExceeded: true }));
    await settle();
    expect(el.querySelector('[data-value="ytd"]')!.classList).toContain('exceeded');
    expect(el.querySelector('[data-annual-warning]')!.textContent).toContain('150 h');
  });

  it('no_annual_warning_within_limit', async () => {
    setup();
    analysisReq().flush(analysis());
    await settle();
    expect(el.querySelector('[data-annual-warning]')).toBeNull();
  });

  it('refetches_when_employee_changes', async () => {
    setup('e1');
    analysisReq().flush(analysis());
    await settle();
    fixture.componentRef.setInput('employeeId', 'e2');
    fixture.detectChanges();
    const req = analysisReq();
    expect(req.request.params.get('employeeId')).toBe('e2');
    req.flush(analysis({ workedHours: 99 }));
    await settle();
    expect(el.querySelector('[data-value="worked"]')!.textContent).toContain('99,0 h');
  });

  it('refetches_when_month_changes', async () => {
    setup('e1');
    analysisReq().flush(analysis());
    await settle();
    fixture.componentRef.setInput('month', new Date(2026, 8, 1));
    fixture.detectChanges();
    const req = analysisReq();
    expect(req.request.params.get('from')).toBe('2026-09-01');
    expect(req.request.params.get('to')).toBe('2026-09-30');
    req.flush(analysis());
  });

  it('ignores_stale_response', async () => {
    setup('e1');
    const first = analysisReq();
    fixture.componentRef.setInput('employeeId', 'e2');
    fixture.detectChanges();
    const second = http.expectOne((r) => r.url === '/api/timesheet/analysis' && r.params.get('employeeId') === 'e2');
    second.flush(analysis({ workedHours: 22 }));
    await settle();
    expect(first.cancelled).toBe(true);
    expect(el.querySelector('[data-value="worked"]')!.textContent).toContain('22,0 h');
  });

  it('shows_error_with_retry_and_recovers', async () => {
    setup();
    analysisReq().flush(
      { title: 'Błąd', status: 500, detail: 'Analiza niedostępna.' },
      { status: 500, statusText: 'Error' },
    );
    await settle();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Analiza niedostępna.');
    (el.querySelector('[data-retry]') as HTMLButtonElement).click();
    analysisReq().flush(analysis());
    await settle();
    expect(el.querySelector('[role="alert"]')).toBeNull();
    expect(el.querySelector('[data-value="worked"]')).toBeTruthy();
  });
});
