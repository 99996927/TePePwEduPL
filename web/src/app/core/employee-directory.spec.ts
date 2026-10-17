import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { TestBed } from '@angular/core/testing';
import { EmployeeDirectory } from './employee-directory';

describe('EmployeeDirectory', () => {
  let http: HttpTestingController;
  let directory: EmployeeDirectory;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    http = TestBed.inject(HttpTestingController);
    directory = TestBed.inject(EmployeeDirectory);
  });

  afterEach(() => http.verify());

  const employee = { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't', teamName: 'T' };

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
});
