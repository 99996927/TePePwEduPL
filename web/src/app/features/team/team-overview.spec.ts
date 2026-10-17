import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { provideApiConfiguration } from '../../api/api-configuration';
import { EmployeeDto } from '../../api/models/employee-dto';
import { TimesheetAnalysisDto } from '../../api/models/timesheet-analysis-dto';
import { TeamOverview } from './team-overview';

const people: EmployeeDto[] = Array.from({ length: 7 }, (_, i) => ({
  id: `e${i + 1}`,
  firstName: `Imie${i + 1}`,
  lastName: `Nazwisko${i + 1}`,
  hireDate: '2020-01-01',
  teamId: 't1',
  teamName: 'Platforma',
  userId: null,
}));

function analysis(patch: Partial<TimesheetAnalysisDto> = {}): TimesheetAnalysisDto {
  return {
    normHours: 160,
    workedHours: 152.5,
    overtime: { hours50: 3, hours100: 1, total: 4, ruleIds: [] },
    restViolations: [],
    weeklyAverage: { averageWeeklyHours: 40, exceedsLimit: false, ruleId: 'KP-131' },
    overtimeYearToDate: 20,
    annualOvertimeLimitExceeded: false,
    ...patch,
  };
}

describe('TeamOverview', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<TeamOverview>;
  let el: HTMLElement;

  async function setup(list: EmployeeDto[] = people) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideRouter([]),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TeamOverview);
    el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
    http.expectOne('/api/employees').flush(list);
    await settle();
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  const pending = (): TestRequest[] => http.match((r) => r.url === '/api/timesheet/analysis');
  const idOf = (r: TestRequest) => r.request.params.get('employeeId');

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('fetches_analysis_for_each_person_with_at_most_four_in_flight', async () => {
    await setup();
    let inFlight: TestRequest[] = pending();
    let maxInFlight = inFlight.length;
    expect(inFlight).toHaveLength(4);
    const seen: string[] = [];
    while (inFlight.length > 0) {
      const done = inFlight[0];
      seen.push(idOf(done)!);
      expect(done.request.params.get('from')).toBe('2026-10-01');
      expect(done.request.params.get('to')).toBe('2026-10-31');
      done.flush(analysis());
      await settle();
      // match() removes what it returns; keep still-unflushed ones in the list
      inFlight = [...inFlight.slice(1), ...pending()];
      maxInFlight = Math.max(maxInFlight, inFlight.length);
    }
    expect(maxInFlight).toBe(4);
    expect(seen.sort()).toEqual(people.map((p) => p.id).sort());
    expect(seen).toHaveLength(7);
  });

  it('shows_worked_over_norm_and_overtime_total', async () => {
    await setup(people.slice(0, 1));
    pending()[0].flush(analysis());
    await settle();
    const row = el.querySelector('[data-row="e1"]')!.textContent!;
    expect(row).toContain('Imie1 Nazwisko1');
    expect(row).toContain('152,5 / 160,0 h');
    expect(row).toContain('4,0 h');
  });

  it('badges_match_responses', async () => {
    await setup(people.slice(0, 3));
    const [a, b, c] = pending();
    const byId = (id: string) => [a, b, c].find((r) => idOf(r) === id)!;
    byId('e1').flush(
      analysis({
        restViolations: [
          { ruleId: 'KP-132-1', from: '2026-10-05T22:00:00', to: '2026-10-06T07:00:00', restHours: 9, requiredHours: 11 },
          { ruleId: 'KP-133-1', from: '2026-10-05T22:00:00', to: '2026-10-07T07:00:00', restHours: 30, requiredHours: 35 },
        ],
      }),
    );
    byId('e2').flush(analysis({ weeklyAverage: { averageWeeklyHours: 50, exceedsLimit: true, ruleId: 'KP-131' } }));
    byId('e3').flush(analysis());
    await settle();
    const row = (id: string) => el.querySelector(`[data-row="${id}"]`)!;
    expect(row('e1').querySelector('[data-badge="rest"]')!.textContent).toContain('Odpoczynek');
    expect(row('e1').querySelector('[data-badge="rest"]')!.textContent).toContain('2');
    expect(row('e1').querySelector('[data-badge="average"]')).toBeNull();
    expect(row('e2').querySelector('[data-badge="average"]')!.textContent).toContain('Średnia >48 h');
    expect(row('e2').querySelector('[data-badge="rest"]')).toBeNull();
    expect(row('e3').querySelector('[data-badge]')).toBeNull();
  });

  it('one_failure_shows_row_retry_and_does_not_affect_others', async () => {
    await setup(people.slice(0, 2));
    const [a, b] = pending();
    const fail = idOf(a) === 'e1' ? a : b;
    const ok = fail === a ? b : a;
    fail.flush({ title: 'Błąd', status: 500, detail: 'Nie udało się pobrać analizy.' }, { status: 500, statusText: 'Error' });
    ok.flush(analysis());
    await settle();
    const failedRow = el.querySelector('[data-row="e1"]')!;
    const okRow = el.querySelector('[data-row="e2"]')!;
    expect(failedRow.textContent).toContain('Spróbuj ponownie');
    expect(failedRow.textContent).toContain('Nie udało się pobrać analizy.');
    expect(okRow.textContent).not.toContain('Spróbuj ponownie');
    expect(okRow.textContent).toContain('152,5 / 160,0 h');
    (failedRow.querySelector('[data-retry]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const retry = pending();
    expect(retry).toHaveLength(1);
    expect(idOf(retry[0])).toBe('e1');
    retry[0].flush(analysis());
    await settle();
    expect(el.querySelector('[data-row="e1"]')!.textContent).not.toContain('Spróbuj ponownie');
    expect(el.querySelector('[data-row="e1"]')!.textContent).toContain('152,5 / 160,0 h');
  });

  it('retry_click_does_not_navigate', async () => {
    await setup(people.slice(0, 1));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    pending()[0].flush({ title: 'x', status: 500 }, { status: 500, statusText: 'Error' });
    await settle();
    (el.querySelector('[data-retry]') as HTMLButtonElement).click();
    expect(navigate).not.toHaveBeenCalled();
    pending().forEach((r) => r.flush(analysis()));
  });

  it('clicking_row_navigates_to_timesheet_with_employee_and_month', async () => {
    await setup(people.slice(0, 2));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    pending().forEach((r) => r.flush(analysis()));
    await settle();
    (el.querySelector('[data-row="e2"]') as HTMLElement).click();
    expect(navigate).toHaveBeenCalledWith(['/czas-pracy'], {
      queryParams: { pracownik: 'e2', miesiac: '2026-10' },
    });
  });

  it('changing_month_refetches_all_for_previous_month', async () => {
    await setup(people.slice(0, 2));
    pending().forEach((r) => r.flush(analysis()));
    await settle();
    (el.querySelector('[aria-label="Poprzedni miesiąc"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const reqs = pending();
    expect(reqs).toHaveLength(2);
    expect(reqs.every((r) => r.request.params.get('from') === '2026-09-01')).toBe(true);
    reqs.forEach((r) => r.flush(analysis()));
  });

  it('shows_empty_message_without_people', async () => {
    await setup([]);
    expect(el.textContent).toContain('Brak pracowników');
  });

  it('destroying_the_component_cancels_pending_requests_and_issues_no_more', async () => {
    await setup();
    const inFlight = pending();
    expect(inFlight).toHaveLength(4);
    fixture.destroy();
    expect(inFlight.every((r) => r.cancelled)).toBe(true);
    await vi.waitFor(() => expect(pending()).toHaveLength(0));
  });

  it('enter_on_retry_button_retries_instead_of_navigating', async () => {
    await setup(people.slice(0, 1));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    pending()[0].flush({ title: 'x', status: 500 }, { status: 500, statusText: 'Error' });
    await settle();
    const button = el.querySelector('[data-retry]') as HTMLButtonElement;
    button.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    fixture.detectChanges();
    expect(navigate).not.toHaveBeenCalled();
    // browsers turn Enter on a button into a click; synthetic keydown does not, so emulate it
    button.click();
    expect(navigate).not.toHaveBeenCalled();
    expect(pending()).toHaveLength(1);
  });
});
