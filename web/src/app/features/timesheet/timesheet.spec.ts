import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS, provideNativeDateAdapter } from '@angular/material/core';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideApiConfiguration } from '../../api/api-configuration';
import { EmployeeDto } from '../../api/models/employee-dto';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { SessionService } from '../../core/session';
import { Timesheet } from './timesheet';

const employees: EmployeeDto[] = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'Platforma' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2021-01-01', teamId: 't1', teamName: 'Platforma' },
];

function entry(id: string, employeeId: string, start: string, end: string, breakMinutes = 30): WorkEntryDto {
  return { id, employeeId, start, end, breakMinutes };
}

const octoberEntries = [
  entry('w2', 'e1', '2026-10-06T08:00:00', '2026-10-06T16:30:00'),
  entry('w1', 'e1', '2026-10-05T22:00:00', '2026-10-06T06:00:00', 0),
  entry('w3', 'e2', '2026-10-07T09:00:00', '2026-10-07T17:00:00'),
];

describe('Timesheet', () => {
  let http: HttpTestingController;

  async function setup(
    roles: string[],
    list: WorkEntryDto[] = octoberEntries,
    query: Record<string, string> = {},
    from = '2026-10-01',
  ) {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
    const user = signal({ userId: 'u1', employeeId: 'e1', userName: 'x', displayName: 'X', roles });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } },
        {
          provide: SessionService,
          useValue: { user: user.asReadonly(), hasRole: (...r: string[]) => r.some((x) => roles.includes(x)) },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Timesheet);
    fixture.detectChanges();
    if (roles.some((r) => r !== 'Employee')) http.expectOne('/api/employees').flush(employees);
    http
      .expectOne((r) => r.url === '/api/timesheet/entries' && r.params.get('from') === from)
      .flush(list);
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    return { fixture, el, loader };
  }

  async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => {
    // The embedded analysis panel is covered by its own spec; discard its requests here.
    http.match((r) => r.url === '/api/timesheet/analysis');
    http.verify();
    vi.useRealTimers();
  });

  const rows = (el: HTMLElement) => Array.from(el.querySelectorAll('tr[data-row]'));
  const ids = (el: HTMLElement) => rows(el).map((r) => r.getAttribute('data-row'));

  it('requests_october_range_and_shows_only_selected_employee_sorted_by_start', async () => {
    const { el } = await setup(['Employee']);
    expect(ids(el)).toEqual(['w1', 'w2']);
    expect(el.textContent).not.toContain('09:00');
  });

  it('request_has_from_and_to_query', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        { provide: SessionService, useValue: { user: signal({ employeeId: 'e1', roles: ['Employee'] }), hasRole: () => false } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.createComponent(Timesheet).detectChanges();
    const req = http.expectOne('/api/timesheet/entries?from=2026-10-01&to=2026-10-31');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('renders_weekday_date_times_break_and_worked_hours', async () => {
    const { el } = await setup(['Employee']);
    const row = el.querySelector('[data-row="w2"]')!.textContent!;
    expect(row).toContain('wt.');
    expect(row).toContain('06.10.2026');
    expect(row).toContain('08:00');
    expect(row).toContain('16:30');
    expect(row).toContain('30');
    expect(row).toContain('8,0');
  });

  it('overnight_entry_is_marked_plus_one_day_and_others_are_not', async () => {
    const { el } = await setup(['Employee']);
    const w1 = el.querySelector('[data-row="w1"]')!.textContent!;
    expect(w1).toContain('22:00');
    expect(w1).toContain('06:00');
    expect(w1).toContain('+1 dzień');
    expect(w1).toContain('8,0');
    expect(el.querySelector('[data-row="w2"]')!.textContent).not.toContain('+1 dzień');
  });

  it('worked_hours_use_wall_clock_on_the_dst_change_day_regardless_of_browser_timezone', async () => {
    const { el } = await setup(['Employee'], [entry('d1', 'e1', '2026-10-25T00:00:00', '2026-10-25T08:00:00')]);
    expect(el.querySelector('[data-row="d1"]')!.textContent).toContain('7,5');
  });

  it('shows_empty_state', async () => {
    const { el } = await setup(['Employee'], []);
    expect(el.textContent).toContain('Brak wpisów w tym miesiącu');
    expect(rows(el)).toHaveLength(0);
  });

  it('employee_does_not_see_picker_manager_does', async () => {
    const a = await setup(['Employee']);
    expect(a.el.querySelector('app-employee-picker')).toBeNull();
    TestBed.resetTestingModule();
    const b = await setup(['Manager']);
    expect(b.el.querySelector('app-employee-picker')).toBeTruthy();
  });

  it('manager_picker_defaults_to_self_and_switching_filters_client_side', async () => {
    const { el, loader, fixture } = await setup(['Manager']);
    const select = await loader.getHarness(MatSelectHarness);
    expect(await select.getValueText()).toBe('Anna Nowak (Platforma)');
    expect(ids(el)).toEqual(['w1', 'w2']);
    await select.open();
    await select.clickOptions({ text: 'Jan Kowalski (Platforma)' });
    await settle(fixture);
    expect(ids(el)).toEqual(['w3']);
  });

  it('manager_reads_employee_and_month_from_query_params', async () => {
    const { el } = await setup(
      ['Manager'],
      [entry('q1', 'e2', '2026-09-08T09:00:00', '2026-09-08T17:00:00')],
      { pracownik: 'e2', miesiac: '2026-09' },
      '2026-09-01',
    );
    expect(ids(el)).toEqual(['q1']);
    expect(el.querySelector('app-month-switcher')!.textContent).toContain('wrzesień 2026');
    expect(el.querySelector('mat-select')!.textContent).toContain('Jan Kowalski');
  });

  it('employee_role_ignores_pracownik_query_param', async () => {
    const { el } = await setup(['Employee'], octoberEntries, { pracownik: 'e2' });
    expect(ids(el)).toEqual(['w1', 'w2']);
  });

  it.each(['2027-01', 'abc'])('invalid_or_future_month_query_%s_falls_back_to_current_month', async (miesiac) => {
    const { el } = await setup(['Employee'], octoberEntries, { miesiac });
    expect(ids(el)).toEqual(['w1', 'w2']);
  });

  it('embeds_analysis_panel_for_selected_employee', async () => {
    const { el, loader, fixture } = await setup(['Manager']);
    const first = http.expectOne((r) => r.url === '/api/timesheet/analysis');
    expect(first.request.params.get('employeeId')).toBe('e1');
    expect(first.request.params.get('from')).toBe('2026-10-01');
    first.flush({
      normHours: 160, workedHours: 1, overtime: { hours50: 0, hours100: 0, total: 0, ruleIds: [] },
      restViolations: [], weeklyAverage: { averageWeeklyHours: 0, exceedsLimit: false, ruleId: 'x' },
      overtimeYearToDate: 0, annualOvertimeLimitExceeded: false,
    });
    expect(el.querySelector('app-analysis-panel')).toBeTruthy();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Jan Kowalski (Platforma)' });
    await settle(fixture);
    expect(http.expectOne((r) => r.url === '/api/timesheet/analysis').request.params.get('employeeId')).toBe('e2');
  });

  it('add_button_only_for_own_time', async () => {
    const { el, loader, fixture } = await setup(['Manager']);
    expect(el.querySelector('[data-add]')).toBeTruthy();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Jan Kowalski (Platforma)' });
    await settle(fixture);
    expect(el.querySelector('[data-add]')).toBeNull();
  });

  it('correct_button_for_own_entries_but_not_for_manager_on_foreign_ones', async () => {
    const { el, loader, fixture } = await setup(['Manager']);
    expect(el.querySelector('[data-row="w1"] [data-correct]')).toBeTruthy();
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Jan Kowalski (Platforma)' });
    await settle(fixture);
    expect(el.querySelector('[data-row="w3"]')).toBeTruthy();
    expect(el.querySelector('[data-row="w3"] [data-correct]')).toBeNull();
  });

  it.each(['HR', 'HRApprover'])('correct_button_for_foreign_entries_as_%s', async (role) => {
    const { el, loader, fixture } = await setup([role]);
    const select = await loader.getHarness(MatSelectHarness);
    await select.open();
    await select.clickOptions({ text: 'Jan Kowalski (Platforma)' });
    await settle(fixture);
    expect(el.querySelector('[data-row="w3"] [data-correct]')).toBeTruthy();
  });

  it('next_month_button_requests_next_range', async () => {
    const { el, fixture } = await setup(['Employee']);
    (el.querySelector('[aria-label="Poprzedni miesiąc"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    http
      .expectOne('/api/timesheet/entries?from=2026-09-01&to=2026-09-30')
      .flush([entry('s1', 'e1', '2026-09-10T08:00:00', '2026-09-10T16:00:00')]);
    await settle(fixture);
    expect(ids(el)).toEqual(['s1']);
    (el.querySelector('[aria-label="Następny miesiąc"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    http.expectOne('/api/timesheet/entries?from=2026-10-01&to=2026-10-31').flush(octoberEntries);
    await settle(fixture);
    expect(ids(el)).toEqual(['w1', 'w2']);
  });

  it('forward_navigation_requests_the_following_month', async () => {
    const { el, fixture } = await setup(['Employee']);
    // current month is the maximum: the next button is disabled
    expect((el.querySelector('[aria-label="Następny miesiąc"]') as HTMLButtonElement).disabled).toBe(true);
    (el.querySelector('[aria-label="Poprzedni miesiąc"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    http.expectOne('/api/timesheet/entries?from=2026-09-01&to=2026-09-30').flush([]);
    await settle(fixture);
    expect(el.textContent).toContain('Brak wpisów w tym miesiącu');
    expect((el.querySelector('[aria-label="Następny miesiąc"]') as HTMLButtonElement).disabled).toBe(false);
  });

  it('shows_error_with_retry_and_recovers', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
        { provide: SessionService, useValue: { user: signal({ employeeId: 'e1', roles: ['Employee'] }), hasRole: () => false } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(Timesheet);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/timesheet/entries').flush(
      { title: 'Błąd serwera', status: 500, detail: 'Nie udało się pobrać wpisów.' },
      { status: 500, statusText: 'Error' },
    );
    await settle(fixture);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Nie udało się pobrać wpisów.');
    (el.querySelector('[data-retry]') as HTMLButtonElement).click();
    http.expectOne((r) => r.url === '/api/timesheet/entries').flush(octoberEntries);
    await settle(fixture);
    expect(el.querySelector('[role="alert"]')).toBeNull();
    expect(ids(el)).toEqual(['w1', 'w2']);
  });

  it('add_dialog_opens_and_saving_refreshes_list', async () => {
    const { el, loader, fixture } = await setup(['Employee']);
    (el.querySelector('[data-add]') as HTMLButtonElement).click();
    await settle(fixture);
    expect(await loader.getAllHarnesses(MatDialogHarness)).toHaveLength(1);
    const dlg = document.querySelector('app-entry-dialog')!;
    expect(dlg.textContent).toContain('Dodaj wpis');
    const start = dlg.querySelector('[data-field="start"]') as HTMLInputElement;
    const end = dlg.querySelector('[data-field="end"]') as HTMLInputElement;
    start.value = '08:00'; start.dispatchEvent(new Event('input'));
    end.value = '16:00'; end.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (dlg.querySelector('[data-save]') as HTMLButtonElement).click();
    const post = await vi.waitFor(() => http.expectOne('/api/timesheet/entries'));
    expect(post.request.method).toBe('POST');
    post.flush(entry('w9', 'e1', '2026-10-16T08:00:00', '2026-10-16T16:00:00'));
    const reload = await vi.waitFor(() =>
      http.expectOne((r) => r.method === 'GET' && r.url === '/api/timesheet/entries'),
    );
    reload.flush([...octoberEntries, entry('w9', 'e1', '2026-10-16T08:00:00', '2026-10-16T16:00:00')]);
    await settle(fixture);
    expect(ids(el)).toEqual(['w1', 'w2', 'w9']);
  });

  it('correct_dialog_opens_for_the_row_entry', async () => {
    const { el, loader, fixture } = await setup(['Employee']);
    (el.querySelector('[data-row="w2"] [data-correct]') as HTMLButtonElement).click();
    await settle(fixture);
    expect(await loader.getAllHarnesses(MatDialogHarness)).toHaveLength(1);
    const dlg = document.querySelector('app-entry-dialog')!;
    expect(dlg.textContent).toContain('Powód');
    expect((dlg.querySelector('[data-field="end"]') as HTMLInputElement).value).toBe('16:30');
  });
});
