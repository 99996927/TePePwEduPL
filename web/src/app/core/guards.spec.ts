import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { CurrentUserDto } from '../api/models/current-user-dto';
import { authGuard, roleGuard } from './guards';
import { Notifier } from './notifier';
import { SessionService } from './session';

function userWith(...roles: string[]): CurrentUserDto {
  return { userId: 'u', employeeId: 'e', userName: 'x', displayName: 'X', roles };
}

describe('guards', () => {
  const info = vi.fn();
  const current = signal<CurrentUserDto | null>(null);

  beforeEach(() => {
    info.mockReset();
    current.set(null);
    const session = {
      user: current.asReadonly(),
      isAuthenticated: () => current() !== null,
      hasRole: (...roles: string[]) => roles.some((r) => current()?.roles.includes(r) ?? false),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: session },
        { provide: Notifier, useValue: { info } },
      ],
    });
  });

  const run = (guard: CanActivateFn) =>
    TestBed.runInInjectionContext(() => guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));

  it('authGuard_unauthenticated_redirects_to_login', () => {
    const result = run(authGuard);

    expect(result instanceof UrlTree).toBe(true);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/logowanie');
  });

  it('authGuard_authenticated_allows', () => {
    current.set(userWith('Employee'));

    expect(run(authGuard)).toBe(true);
  });

  it('roleGuard_employee_gets_root_urltree_and_message', () => {
    current.set(userWith('Employee'));

    const result = run(roleGuard('Manager', 'HR', 'HRApprover'));

    expect(result instanceof UrlTree).toBe(true);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
    expect(info).toHaveBeenCalledWith('Brak dostępu do tej strony.');
  });

  it('roleGuard_manager_passes', () => {
    current.set(userWith('Manager'));

    expect(run(roleGuard('Manager', 'HR', 'HRApprover'))).toBe(true);
    expect(info).not.toHaveBeenCalled();
  });
});
