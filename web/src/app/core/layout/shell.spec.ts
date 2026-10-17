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

  it('employee_sees_three_menu_items', async () => {
    const { el } = await open('Employee');

    expect(labels(el)).toEqual(['Pulpit', 'Urlopy', 'Czas pracy']);
  });

  it.each(['Manager', 'HR'])('%s_sees_five_menu_items', async (role) => {
    const { el } = await open(role);

    expect(labels(el)).toEqual(['Pulpit', 'Urlopy', 'Czas pracy', 'Zatwierdzanie urlopów', 'Przegląd zespołu']);
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
