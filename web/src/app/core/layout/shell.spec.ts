import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { Notifier } from '../notifier';
import { SessionService } from '../session';
import { Shell } from './shell';

@Component({ template: '<p>content</p>' })
class Blank {}

describe('Shell', () => {
  const logout = vi.fn();
  const error = vi.fn();
  const user = signal<CurrentUserDto | null>(null);

  async function open(...roles: string[]) {
    user.set({ userId: 'u', employeeId: 'e', userName: 'x', displayName: 'Ewa Wiśniewska', roles });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: '', component: Shell, children: [{ path: '', component: Blank }] },
          { path: 'logowanie', component: Blank },
        ]),
        {
          provide: SessionService,
          useValue: {
            user: user.asReadonly(),
            hasRole: (...r: string[]) => r.some((x) => roles.includes(x)),
            logout,
          },
        },
        { provide: Notifier, useValue: { error } },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/', Shell);
    harness.detectChanges();
    return { harness, el: harness.routeNativeElement as HTMLElement };
  }

  const labels = (el: HTMLElement) =>
    Array.from(el.querySelectorAll('nav a')).map((a) => a.textContent!.trim());

  beforeEach(() => {
    logout.mockReset();
    error.mockReset();
  });

  it.each([
    ['Employee', ['Pulpit', 'Urlopy', 'Czas pracy', 'Naliczenia', 'Agent']],
    ['Manager', ['Pulpit', 'Urlopy', 'Czas pracy', 'Naliczenia', 'Agent', 'Zatwierdzanie urlopów', 'Przegląd zespołu']],
    ['HR', ['Pulpit', 'Urlopy', 'Czas pracy', 'Naliczenia', 'Agent', 'Zatwierdzanie urlopów', 'Przegląd zespołu', 'Zmiany płacowe', 'Szkice korekt', 'Audyt']],
    ['HRApprover', ['Pulpit', 'Urlopy', 'Czas pracy', 'Naliczenia', 'Agent', 'Zatwierdzanie urlopów', 'Przegląd zespołu', 'Zmiany płacowe', 'Szkice korekt', 'Audyt']],
  ])('%s_sees_expected_menu_items', async (role, expected) => {
    const { el } = await open(role);

    expect(labels(el)).toEqual(expected);
  });

  it('toolbar_shows_title_name_and_role', async () => {
    const { el } = await open('Manager');
    const toolbar = el.querySelector('mat-toolbar')!.textContent!;

    expect(toolbar).toContain('Asystent kadrowo-płacowy');
    expect(toolbar).toContain('Ewa Wiśniewska');
    expect(toolbar).toContain('Kierownik');
    expect(el.textContent).toContain('content');
  });

  it('logout_calls_session_and_navigates_to_login', async () => {
    logout.mockResolvedValue(undefined);
    const { harness, el } = await open('Employee');

    el.querySelector<HTMLButtonElement>('button.logout')!.click();
    await harness.fixture.whenStable();

    expect(logout).toHaveBeenCalled();
    expect(TestBed.inject(Router).url).toBe('/logowanie');
    expect(error).not.toHaveBeenCalled();
  });

  it('logout_failure_still_navigates_and_notifies', async () => {
    logout.mockRejectedValue({ status: 500, title: 'boom' });
    const { harness, el } = await open('Employee');

    el.querySelector<HTMLButtonElement>('button.logout')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/logowanie');
    expect(error).toHaveBeenCalledTimes(1);
  });

  it('logout_401_still_navigates_without_notification', async () => {
    logout.mockRejectedValue({ status: 401, title: 'x' });
    const { harness, el } = await open('Employee');

    el.querySelector<HTMLButtonElement>('button.logout')!.click();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/logowanie');
    expect(error).not.toHaveBeenCalled();
  });

  it.each([
    [['Employee'], 'Pracownik'],
    [['Manager'], 'Kierownik'],
    [['HR'], 'Kadry'],
    [['HRApprover'], 'Zatwierdzający kadr'],
    [['Manager', 'Auditor'], 'Kierownik, Auditor'],
  ])('toolbar_shows_polish_role_label_for_%j', async (roles, label) => {
    const { el } = await open(...roles);

    expect(el.querySelector('.role')!.textContent!.trim()).toBe(label);
  });
});

describe('Shell look', () => {
  const user = signal<CurrentUserDto | null>(null);
  const FULL_MENU_ICONS = [
    'dashboard',
    'beach_access',
    'schedule',
    'payments',
    'smart_toy',
    'task_alt',
    'groups',
    'request_quote',
    'edit_note',
    'fact_check',
  ];

  async function openAt(url: string, ...roles: string[]) {
    user.set({ userId: 'u', employeeId: 'e', userName: 'x', displayName: 'Ewa Wiśniewska', roles });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '', component: Shell, children: [{ path: '**', component: Blank }] }]),
        {
          provide: SessionService,
          useValue: {
            user: user.asReadonly(),
            hasRole: (...r: string[]) => r.some((x) => roles.includes(x)),
            logout: vi.fn(),
          },
        },
        { provide: Notifier, useValue: { error: vi.fn() } },
      ],
    });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    harness.detectChanges();
    return harness.fixture.nativeElement as HTMLElement;
  }

  it.each([
    ['Employee', 5],
    ['Manager', 7],
    ['HR', 10],
  ])('%s_menu_items_have_icons_in_menu_order', async (role, count) => {
    const el = await openAt('/', role);
    const icons = Array.from(el.querySelectorAll('nav a mat-icon')).map((i) => i.getAttribute('fonticon'));

    expect(icons).toEqual(FULL_MENU_ICONS.slice(0, 5).concat(FULL_MENU_ICONS.slice(5, count)));
  });

  it.each([
    ['/', 'Pulpit'],
    ['/urlopy', 'Urlopy'],
    ['/urlopy/zatwierdzanie', 'Zatwierdzanie urlopów'],
    ['/agent/abc', 'Agent'],
    ['/nieznana', 'Asystent kadrowo-płacowy'],
  ])('title_for_%s_is_%s', async (url, title) => {
    const el = await openAt(url, 'Manager');

    expect(el.querySelector('.title')!.textContent!.trim()).toBe(title);
  });

  it('toolbar_exposes_user_name_and_role_for_the_current_user', async () => {
    const el = await openAt('/', 'HR');

    expect(el.querySelector('[data-user-name]')!.textContent!.trim()).toBe('Ewa Wiśniewska');
    expect(el.querySelector('[data-user-role]')!.textContent!.trim()).toBe('Kadry');
  });
});

