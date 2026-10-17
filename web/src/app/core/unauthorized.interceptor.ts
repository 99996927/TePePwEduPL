import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { SessionService } from './session';

const EXCLUDED = ['/api/auth/login', '/api/auth/me'];

export const unauthorizedInterceptor: HttpInterceptorFn = (req, next) => {
  const session = inject(SessionService);
  const router = inject(Router);
  return next(req).pipe(
    tap({
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 401) return;
        if (EXCLUDED.some((path) => req.url.includes(path))) return;
        // Only the first 401 of an expired session redirects; parallel ones find the session already cleared.
        const wasAuthenticated = session.isAuthenticated();
        session.clear();
        if (wasAuthenticated) {
          void router.navigate(['/logowanie'], { queryParams: { sesja: 'wygasla' } });
        }
      },
    }),
  );
};
