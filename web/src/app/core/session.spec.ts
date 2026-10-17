import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { TestBed } from '@angular/core/testing';
import { ApiProblem } from './api-problem';
import { SessionService } from './session';

const user = { userId: 'u1', employeeId: 'e1', userName: 'anna', displayName: 'Anna Nowak', roles: ['Employee'] };

describe('SessionService', () => {
  let http: HttpTestingController;
  let session: SessionService;

  beforeEach(() => {
    document.cookie = 'XSRF-TOKEN=test-token';
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionService);
  });

  afterEach(() => {
    document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT';
    http.verify();
  });

  it('load_401_sets_null_user_and_loaded', async () => {
    const loading = session.load();
    http.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await loading;

    expect(session.user()).toBeNull();
    expect(session.loaded()).toBe(true);
    expect(session.isAuthenticated()).toBe(false);
  });

  it('load_200_sets_user', async () => {
    const loading = session.load();
    http.expectOne('/api/auth/me').flush(user);
    await loading;

    expect(session.user()).toEqual(user);
    expect(session.isAuthenticated()).toBe(true);
    expect(session.hasRole('Manager', 'Employee')).toBe(true);
    expect(session.hasRole('HR')).toBe(false);
  });

  it('login_calls_antiforgery_then_login_with_xsrf_header', async () => {
    const logging = session.login('anna', 'pw');
    http.expectNone({ method: 'POST', url: '/api/auth/login' });
    http.expectOne({ method: 'GET', url: '/api/auth/antiforgery' }).flush(null, { status: 204, statusText: 'No Content' });
    await vi.waitFor(() => http.expectOne({ method: 'POST', url: '/api/auth/login' }).flush(user));
    await logging;

    expect(session.user()).toEqual(user);
  });

  it('login_sends_x_xsrf_token_from_cookie', async () => {
    const logging = session.login('anna', 'pw');
    http.expectOne('/api/auth/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
    let header: string | null = null;
    await vi.waitFor(() => {
      const req = http.expectOne('/api/auth/login');
      header = req.request.headers.get('X-XSRF-TOKEN');
      expect(req.request.body).toEqual({ userName: 'anna', password: 'pw' });
      req.flush(user);
    });
    await logging;

    expect(header).toBe('test-token');
  });

  it('login_423_rejects_with_locked_out_problem', async () => {
    const logging = session.login('anna', 'bad');
    const outcome = logging.then(
      () => null,
      (e: ApiProblem) => e,
    );
    http.expectOne('/api/auth/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
    await vi.waitFor(() =>
      http
        .expectOne('/api/auth/login')
        .flush({ title: 'Konto zablokowane', code: 'LockedOut' }, { status: 423, statusText: 'Locked' }),
    );
    const problem = await outcome;

    expect(problem?.status).toBe(423);
    expect(problem?.code).toBe('LockedOut');
    expect(session.user()).toBeNull();
  });

  it('logout_clears_user', async () => {
    const loading = session.load();
    http.expectOne('/api/auth/me').flush(user);
    await loading;

    const out = session.logout();
    http.expectOne({ method: 'POST', url: '/api/auth/logout' }).flush(null, { status: 204, statusText: 'No Content' });
    await out;

    expect(session.user()).toBeNull();
  });
});
