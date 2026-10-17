import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import localePl from '@angular/common/locales/pl';
import {
  ApplicationConfig,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';
import { provideIconFont } from './core/icon-font';
import { SessionService } from './core/session';
import { unauthorizedInterceptor } from './core/unauthorized.interceptor';

registerLocaleData(localePl);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([unauthorizedInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideApiConfiguration(''),
    provideAppInitializer(() => inject(SessionService).load()),
    provideNativeDateAdapter(),
    provideIconFont(),
    { provide: MAT_DATE_LOCALE, useValue: 'pl-PL' },
    { provide: LOCALE_ID, useValue: 'pl-PL' },
  ],
};
