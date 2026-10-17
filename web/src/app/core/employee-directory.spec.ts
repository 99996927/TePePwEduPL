import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { CurrentUserDto } from '../api/models/current-user-dto';
import { provideApiConfiguration } from '../api/api-configuration';
import { TestBed } from '@angular/core/testing';
import { EmployeeDirectory } from './employee-directory';
import { SessionService } from './session';

const userOf = (userId: string): CurrentUserDto => ({
  userId, employeeId: 'e', userName: 'x', displayName: 'X', roles: ['Employee'],
});

describe('EmployeeDirectory', () => {
  let http: HttpTestingController;
  let directory: EmployeeDirectory;
  const current = signal<CurrentUserDto | null>(null);

  beforeEach(() => {
    current.set(userOf('u1'));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: SessionService, useValue: { user: current.asReadonly() } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    directory = TestBed.inject(EmployeeDirectory);
  });

  afterEach(() => http.verify());

  const employee = { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't', teamName: 'T', userId: null };

  it('load_second_call_does_not_hit_api', async () => {
    const first = directory.load();
    http.expectOne('/api/employees').flush([employee]);
    await first;
    await directory.load();

    http.expectNone('/api/employees');
    expect(directory.employees().length).toBe(1);
  });

  it('nameOf_known_returns_full_name_and_unknown_returns_dash', async () => {
    const loading = directory.load();
    http.expectOne('/api/employees').flush([employee]);
    await loading;

    expect(directory.nameOf('e1')).toBe('Anna Nowak');
    expect(directory.nameOf('missing')).toBe('—');
  });

  it('load_after_failure_retries_the_api_call', async () => {
    const failing = directory.load().then(
      () => null,
      (e: unknown) => e,
    );
    http.expectOne('/api/employees').flush(null, { status: 500, statusText: 'Server Error' });
    expect(await failing).not.toBeNull();

    const retry = directory.load();
    http.expectOne('/api/employees').flush([employee]);
    await retry;

    expect(directory.nameOf('e1')).toBe('Anna Nowak');
  });

  it('load_for_another_user_after_relogin_fetches_again_and_replaces_the_list', async () => {
    const other = { ...employee, id: 'e2', firstName: 'Jan', lastName: 'Kowalski' };
    const first = directory.load();
    http.expectOne('/api/employees').flush([employee, other]);
    await first;
    expect(directory.employees().length).toBe(2);

    current.set(null);
    current.set(userOf('u2'));
    const second = directory.load();
    expect(directory.employees()).toEqual([]);
    http.expectOne('/api/employees').flush([other]);
    await second;

    expect(directory.employees()).toEqual([other]);
    expect(directory.nameOf('e1')).toBe('—');
  });

  it('load_for_the_same_user_stays_cached', async () => {
    const first = directory.load();
    http.expectOne('/api/employees').flush([employee]);
    await first;

    current.set(userOf('u1'));
    await directory.load();

    http.expectNone('/api/employees');
  });

  describe('user lookups', () => {
    const withUser = { ...employee, userId: 'u7' };

    beforeEach(async () => {
      const loading = directory.load();
      http.expectOne('/api/employees').flush([withUser, { ...employee, id: 'e2', userId: null }]);
      await loading;
    });

    it('nameOfUser_known_returns_name_and_missing_returns_dash', () => {
      expect(directory.nameOfUser('u7')).toBe('Anna Nowak');
      expect(directory.nameOfUser(null)).toBe('—');
      expect(directory.nameOfUser('zzz')).toBe('—');
      expect(directory.nameOfUser(undefined)).toBe('—');
    });

    it('userLabel_own_user_is_Ty_otherwise_name', () => {
      expect(directory.userLabel('u1')).toBe('Ty');
      expect(directory.userLabel('u7')).toBe('Anna Nowak');
      expect(directory.userLabel(null)).toBe('—');
    });

    it('userIdOf_returns_user_id_or_null', () => {
      expect(directory.userIdOf('e1')).toBe('u7');
      expect(directory.userIdOf('e2')).toBeNull();
      expect(directory.userIdOf('missing')).toBeNull();
    });
  });
});
