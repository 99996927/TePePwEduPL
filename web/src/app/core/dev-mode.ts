import { InjectionToken, isDevMode } from '@angular/core';

/** True in development builds; specs override it to toggle dev-only UI. */
export const IS_DEV_MODE = new InjectionToken<boolean>('IS_DEV_MODE', {
  providedIn: 'root',
  factory: () => isDevMode(),
});
