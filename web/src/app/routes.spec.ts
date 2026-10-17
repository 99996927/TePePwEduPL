import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideApiConfiguration } from './api/api-configuration';
import { CurrentUserDto } from './api/models/current-user-dto';
import { routes } from './app.routes';
import { Notifier } from './core/notifier';
import { SessionService } from './core/session';

function userWith(...roles: string[]): CurrentUserDto {
  return { userId: 'u', employeeId: 'e', userName: 'x', displayName: 'X', roles };
}

describe('routes', () => {
  const info = vi.fn();

  // Mirrors the app initializer: the session is resolved before the first navigation.
  async function start(user: CurrentUserDto | null) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: Notifier, useValue: { info, error: vi.fn() } },
      ],
    });
    const loading = TestBed.inject(SessionService).load();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/auth/me');
    if (user) request.flush(user);
    else request.flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await loading;
    return RouterTestingHarness.create();
  }

  beforeEach(() => info.mockReset());

  it.each(['/zespol', '/urlopy/zatwierdzanie'])('employee_on_%s_is_redirected_to_root_with_message', async (url) => {
    const harness = await start(userWith('Employee'));

    await harness.navigateByUrl(url);

    expect(TestBed.inject(Router).url).toBe('/');
    expect(info).toHaveBeenCalledWith('Brak dostępu do tej strony.');
  });

  it.each(['/zespol', '/urlopy/zatwierdzanie'])('manager_on_%s_stays', async (url) => {
    const harness = await start(userWith('Manager'));

    await harness.navigateByUrl(url);

    expect(TestBed.inject(Router).url).toBe(url);
    expect(info).not.toHaveBeenCalled();
  });

  it.each(['/zmiany-platowe', '/szkice-korekt', '/audyt'])('%s_redirects_employee_and_manager_and_admits_hr', async (url) => {
    for (const role of ['Employee', 'Manager']) {
      info.mockReset();
      TestBed.resetTestingModule();
      const harness = await start(userWith(role));
      await harness.navigateByUrl(url);
      expect(TestBed.inject(Router).url).toBe('/');
      expect(info).toHaveBeenCalledWith('Brak dostępu do tej strony.');
    }
    for (const role of ['HR', 'HRApprover']) {
      info.mockReset();
      TestBed.resetTestingModule();
      const harness = await start(userWith(role));
      await harness.navigateByUrl(url);
      expect(TestBed.inject(Router).url).toBe(url);
      expect(info).not.toHaveBeenCalled();
    }
  });

  it('employee_on_payroll_stays', async () => {
    const harness = await start(userWith('Employee'));

    await harness.navigateByUrl('/naliczenia');

    expect(TestBed.inject(Router).url).toBe('/naliczenia');
  });

  it('anonymous_on_leave_list_lands_on_login', async () => {
    const harness = await start(null);

    await harness.navigateByUrl('/urlopy');

    expect(TestBed.inject(Router).url).toBe('/logowanie?returnUrl=%2Furlopy');
  });

  it('reload_on_timesheet_with_valid_session_stays', async () => {
    const harness = await start(userWith('Employee'));

    await harness.navigateByUrl('/czas-pracy');

    expect(TestBed.inject(Router).url).toBe('/czas-pracy');
  });

  it('unknown_path_redirects_to_root', async () => {
    const harness = await start(userWith('Employee'));

    await harness.navigateByUrl('/nie-ma-takiej');

    expect(TestBed.inject(Router).url).toBe('/');
  });
});
