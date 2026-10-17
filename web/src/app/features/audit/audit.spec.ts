import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { MATERIAL_ANIMATIONS, MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { provideApiConfiguration } from '../../api/api-configuration';
import { AuditEntryDto } from '../../api/models/audit-entry-dto';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { formatInstantDateTime } from '../../core/date-format';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { Audit } from './audit';

const GUID = '3f2c9a10-1b2c-4d5e-8f90-a1b2c3d4e5f6';

const employees = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'ua' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'uj' },
  { id: 'e3', firstName: 'Bez', lastName: 'Konta', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: null },
];

function entry(id: number, actor: string | null, over: Partial<AuditEntryDto> = {}): AuditEntryDto {
  return {
    id, actorUserId: actor, atUtc: '2026-10-05T10:30:00Z', eventType: 'Modified', entityType: 'PayrollChange',
    entityId: GUID, beforeJson: '{"a":1}', afterJson: '{"a":2}', ...over,
  };
}

interface Internals {
  range: FormGroup<{ start: FormControl<Date | null>; end: FormControl<Date | null> }>;
}

async function flush(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

describe('Audit', () => {
  let http: HttpTestingController;

  async function setup(items: AuditEntryDto[] = [], total = items.length) {
    const user = signal<CurrentUserDto | null>({ userId: 'ume', employeeId: 'e9', userName: 'x', displayName: 'X', roles: ['HRApprover'] });
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: MAT_DATE_LOCALE, useValue: 'pl-PL' },
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: SessionService, useValue: { user: user.asReadonly(), hasRole: () => true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const error = vi.spyOn(TestBed.inject(Notifier), 'error').mockImplementation(() => undefined);
    const fixture = TestBed.createComponent(Audit);
    fixture.detectChanges();
    http.expectOne('/api/employees').flush(employees);
    http.expectOne('/api/audit?skip=0&take=25').flush({ total, items });
    await flush(fixture);
    const el = fixture.nativeElement as HTMLElement;
    const settle = () => flush(fixture);
    const q = <T extends HTMLElement>(sel: string) => el.querySelector(sel) as T | null;
    const rows = () => Array.from(el.querySelectorAll('tr[data-row]')) as HTMLElement[];
    const click = async (sel: string) => {
      (el.querySelector(sel) as HTMLElement).click();
      await settle();
    };
    const pick = async (selectSel: string, label: string) => {
      (el.querySelector(selectSel) as HTMLElement).click();
      await settle();
      const option = Array.from(document.querySelectorAll('mat-option')).find((o) => o.textContent?.trim() === label) as HTMLElement;
      option.click();
      await settle();
    };
    const typeId = async (value: string) => {
      const input = q<HTMLInputElement>('[data-entity-id]')!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await settle();
    };
    const setRange = async (start: Date, end: Date) => {
      (fixture.componentInstance as unknown as Internals).range.setValue({ start, end });
      await settle();
    };
    return { fixture, el, q, rows, click, pick, typeId, setRange, settle, error };
  }

  afterEach(() => http.verify());

  it('sends the first load without filters', async () => {
    const { rows } = await setup([entry(1, 'ua')]);
    expect(rows().length).toBe(1);
  });

  it('applies all filters, with the date range as local start and end of day instants', async () => {
    const t = await setup();
    await t.pick('[data-entity-type]', 'Zmiana płacowa');
    await t.typeId(GUID);
    await t.pick('[data-actor]', 'Anna Nowak');
    await t.setRange(new Date(2026, 9, 1), new Date(2026, 9, 2));
    await t.click('[data-search]');
    const from = new Date(2026, 9, 1).toISOString();
    const to = new Date(2026, 9, 2, 23, 59, 59, 999).toISOString();
    const req = http.expectOne((r) => r.url === '/api/audit');
    expect(req.request.params.get('entityType')).toBe('PayrollChange');
    expect(req.request.params.get('entityId')).toBe(GUID);
    expect(req.request.params.get('actorUserId')).toBe('ua');
    expect(req.request.params.get('from')).toBe(from);
    expect(req.request.params.get('to')).toBe(to);
    expect(req.request.params.get('skip')).toBe('0');
    expect(req.request.params.get('take')).toBe('25');
    req.flush({ total: 0, items: [] });
    await t.settle();
  });

  it('lists only people with a user id in the user filter', async () => {
    const t = await setup();
    (t.el.querySelector('[data-actor]') as HTMLElement).click();
    await t.settle();
    const labels = Array.from(document.querySelectorAll('mat-option')).map((o) => o.textContent?.trim());
    expect(labels).toContain('Anna Nowak');
    expect(labels).toContain('Jan Kowalski');
    expect(labels).not.toContain('Bez Konta');
  });

  it('blocks searching with an invalid GUID and shows a message', async () => {
    const t = await setup();
    await t.typeId('nie-guid');
    await t.click('[data-search]');
    http.expectNone((r) => r.url === '/api/audit');
    expect(t.el.textContent).toContain('Podaj poprawny identyfikator (GUID).');
  });

  it('changes page with skip and resets to the first page on page size change', async () => {
    const t = await setup([entry(1, 'ua')], 80);
    await t.click('.mat-mdc-paginator-navigation-next');
    http.expectOne('/api/audit?skip=25&take=25').flush({ total: 80, items: [entry(2, 'ua')] });
    await t.settle();
    await t.pick('.mat-mdc-paginator-page-size .mat-mdc-select', '50');
    http.expectOne('/api/audit?skip=0&take=50').flush({ total: 80, items: [entry(3, 'ua')] });
    await t.settle();
    expect(t.rows().length).toBe(1);
  });

  it('clears filters back to the unfiltered query', async () => {
    const t = await setup();
    await t.pick('[data-entity-type]', 'Zmiana płacowa');
    await t.click('[data-search]');
    http.expectOne((r) => r.url === '/api/audit' && r.params.get('entityType') === 'PayrollChange').flush({ total: 0, items: [] });
    await t.settle();
    await t.click('[data-clear]');
    http.expectOne('/api/audit?skip=0&take=25').flush({ total: 0, items: [] });
    await t.settle();
  });

  it('renders actor, labels, time and shortened id with the full id in the title', async () => {
    const t = await setup([
      entry(1, null, { eventType: 'Added', entityType: 'Employee' }),
      entry(2, 'ume'),
      entry(3, 'uj', { eventType: 'Weird', entityType: 'Strange' }),
      entry(4, 'nobody'),
    ]);
    const cells = (i: number) => Array.from(t.rows()[i].querySelectorAll('td')).map((c) => c.textContent?.trim());
    expect(cells(0)).toContain('System (seed)');
    expect(cells(0)).toContain('Dodano');
    expect(cells(0)).toContain('Pracownik');
    expect(cells(0)).toContain(formatInstantDateTime('2026-10-05T10:30:00Z'));
    expect(cells(1)).toContain('Ty');
    expect(cells(1)).toContain('Zmieniono');
    expect(cells(1)).toContain('Zmiana płacowa');
    expect(cells(2)).toContain('Jan Kowalski');
    expect(cells(2)).toContain('Weird');
    expect(cells(2)).toContain('Strange');
    expect(cells(3)).toContain('—');
    expect(cells(1)).toContain('3f2c9a10…');
    expect(t.rows()[1].querySelector(`[title="${GUID}"]`)).not.toBeNull();
  });

  it('expands a row with formatted before and after JSON and dashes for null', async () => {
    const t = await setup([entry(1, 'ua', { beforeJson: null, afterJson: '{"a":2}' })]);
    expect(t.q('[data-detail]')).toBeNull();
    await t.click('[data-toggle]');
    const before = t.q('[data-before]')!.textContent;
    const after = t.q('[data-after]')!.textContent;
    expect(before?.trim()).toBe('—');
    expect(after).toBe(JSON.stringify({ a: 2 }, null, 2));
    await t.click('[data-toggle]');
    expect(t.q('[data-detail]')).toBeNull();
  });

  it('shows invalid JSON as raw text and keeps HTML inert', async () => {
    const t = await setup([entry(1, 'ua', { beforeJson: '{broken', afterJson: '<script>alert(1)</script>' })]);
    await t.click('[data-toggle]');
    expect(t.q('[data-before]')!.textContent).toBe('{broken');
    expect(t.q('[data-after]')!.textContent).toBe('<script>alert(1)</script>');
    expect(t.el.querySelector('script')).toBeNull();
    expect(t.el.querySelector('pre')!.children.length).toBe(0);
  });

  it('shows the empty state', async () => {
    const t = await setup([]);
    expect(t.el.textContent).toContain('Brak wpisów dla wybranych filtrów');
  });

  it('shows a load error with retry', async () => {
    const t = await setup([entry(1, 'ua')]);
    await t.click('[data-search]');
    http.expectOne('/api/audit?skip=0&take=25').flush({ title: 'Awaria', status: 500 }, { status: 500, statusText: 'x' });
    await t.settle();
    expect(t.q('[role="alert"]')).not.toBeNull();
    await t.click('[data-retry]');
    http.expectOne('/api/audit?skip=0&take=25').flush({ total: 1, items: [entry(1, 'ua')] });
    await t.settle();
    expect(t.q('[role="alert"]')).toBeNull();
    expect(t.rows().length).toBe(1);
  });

  it('ignores a stale response', async () => {
    const t = await setup([]);
    await t.pick('[data-entity-type]', 'Zmiana płacowa');
    await t.click('[data-search]');
    const first = http.expectOne((r) => r.url === '/api/audit');
    await t.pick('[data-entity-type]', 'Pracownik');
    await t.click('[data-search]');
    const second = http.expectOne((r) => r.url === '/api/audit');
    expect(first.cancelled).toBe(true);
    second.flush({ total: 1, items: [entry(99, 'ua')] });
    await t.settle();
    expect(t.rows().length).toBe(1);
    expect(t.rows()[0].getAttribute('data-row')).toBe('99');
  });
});
