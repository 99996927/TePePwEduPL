import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SessionService } from './session';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

const user = { userId: 'u1', employeeId: 'e1', userName: 'anna', displayName: 'Anna', roles: ['Employee'] };

describe('unauthorizedInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let session: SessionService;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    session = TestBed.inject(SessionService);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const loading = session.load();
    http.expectOne('/api/auth/me').flush(user);
    await loading;
  });

  afterEach(() => http.verify());

  it('401_from_api_clears_session_and_navigates_once', () => {
    client.get('/api/leave/requests').subscribe({ error: () => undefined });
    client.get('/api/leave/balance').subscribe({ error: () => undefined });
    const unauthorized = { status: 401, statusText: 'Unauthorized' };
    http.expectOne('/api/leave/requests').flush(null, unauthorized);
    http.expectOne('/api/leave/balance').flush(null, unauthorized);

    expect(session.user()).toBeNull();
    expect(navigate).toHaveBeenCalledTimes(1);
    expect(navigate).toHaveBeenCalledWith(['/logowanie'], { queryParams: { sesja: 'wygasla' } });
  });

  it('401_when_never_authenticated_does_not_navigate_and_keeps_session_cleared', () => {
    session.clear();

    client.get('/api/leave/requests').subscribe({ error: () => undefined });
    http.expectOne('/api/leave/requests').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
    expect(session.user()).toBeNull();
  });

  it('401_from_login_does_not_navigate', () => {
    client.post('/api/auth/login', {}).subscribe({ error: () => undefined });
    http.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
    expect(session.user()).not.toBeNull();
  });

  it('non_401_error_does_not_navigate', () => {
    client.get('/api/leave/requests').subscribe({ error: () => undefined });
    http.expectOne('/api/leave/requests').flush(null, { status: 500, statusText: 'Server Error' });

    expect(navigate).not.toHaveBeenCalled();
  });
});
