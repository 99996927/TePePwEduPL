import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Notifier } from './notifier';
import { Role } from './roles';
import { SessionService } from './session';

export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  return session.isAuthenticated() ? true : inject(Router).createUrlTree(['/logowanie'], { queryParams: { returnUrl: state.url } });
};

export function roleGuard(...roles: Role[]): CanActivateFn {
  return () => {
    if (inject(SessionService).hasRole(...roles)) {
      return true;
    }
    inject(Notifier).info('Brak dostępu do tej strony.');
    return inject(Router).createUrlTree(['/']);
  };
}
