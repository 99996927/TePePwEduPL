import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS, MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDialogRef } from '@angular/material/dialog';
import { provideApiConfiguration } from '../../api/api-configuration';
import { PayrollChangeDto } from '../../api/models/payroll-change-dto';
import { EmployeeDirectory } from '../../core/employee-directory';
import { SessionService } from '../../core/session';
import { NewChangeDialog } from './new-change-dialog';

const created: PayrollChangeDto = {
  id: 'n1', employeeId: 'e1', component: 'BaseSalary', value: 4500.5, effectiveFrom: '2026-11-01', reason: 'Awans',
  status: 'Draft', createdByUserId: 'uh', decidedByUserId: null, decisionReason: null, createdAtUtc: '2026-10-17T00:00:00Z',
};

describe('NewChangeDialog', () => {
  let http: HttpTestingController;
  const close = vi.fn();
  const ref = { close, disableClose: false };

  async function setup() {
    close.mockReset();
    ref.disableClose = false;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: MAT_DATE_LOCALE, useValue: 'pl-PL' },
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: MatDialogRef, useValue: ref },
        { provide: SessionService, useValue: { user: () => ({ userId: 'uh' }) } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const load = TestBed.inject(EmployeeDirectory).load();
    http.expectOne('/api/employees').flush([
      { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'ua' },
    ]);
    await load;
    const fixture = TestBed.createComponent(NewChangeDialog);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const save = () => el.querySelector('[data-save]') as HTMLButtonElement;
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const typeIn = (field: string, text: string) => {
      const input = el.querySelector(`[data-field="${field}"]`) as HTMLInputElement;
      input.value = text;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const fill = (value: string, reason = 'Awans') => {
      fixture.componentInstance.form.controls.employeeId.setValue('e1');
      fixture.componentInstance.form.controls.date.setValue(new Date(2026, 10, 1));
      typeIn('value', value);
      typeIn('reason', reason);
    };
    return { fixture, el, save, settle, typeIn, fill };
  }

  afterEach(() => http.verify());

  it.each(['4500,50', '4500.5'])('sends_parsed_number_for_%s', async (text) => {
    const { save, fill, settle } = await setup();
    fill(text);
    expect(save().disabled).toBe(false);
    save().click();
    const post = http.expectOne('/api/payroll-changes');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({
      employeeId: 'e1', component: 'BaseSalary', value: 4500.5, effectiveFrom: '2026-11-01', reason: 'Awans',
    });
    post.flush(created);
    await settle();
    expect(close).toHaveBeenCalledWith(created);
  });

  it('maps_fixed_allowance_component', async () => {
    const { fixture, save, fill, settle } = await setup();
    fill('300');
    fixture.componentInstance.form.controls.component.setValue('FixedAllowance');
    save().click();
    const post = http.expectOne('/api/payroll-changes');
    expect(post.request.body.component).toBe('FixedAllowance');
    expect(post.request.body.value).toBe(300);
    post.flush(created);
    await settle();
  });

  it.each(['', '-1', 'abc', '1e9', '12,345', '1000000,01', 'NaN', 'Infinity'])(
    'invalid_value_%j_blocks_save_with_field_message',
    async (text) => {
      const { el, save, fill, fixture } = await setup();
      fill(text);
      fixture.componentInstance.form.controls.value.markAsTouched();
      fixture.detectChanges();
      expect(save().disabled).toBe(true);
      save().click();
      http.expectNone('/api/payroll-changes');
      expect(el.querySelector('mat-error')!.textContent).toContain('kwotę');
    },
  );

  it('accepts_the_upper_limit_and_zero', async () => {
    const { save, fill } = await setup();
    fill('1000000');
    expect(save().disabled).toBe(false);
    fill('0');
    expect(save().disabled).toBe(false);
  });

  it('blank_reason_blocks_save', async () => {
    const { save, fill } = await setup();
    fill('100', '   ');
    expect(save().disabled).toBe(true);
    save().click();
    http.expectNone('/api/payroll-changes');
  });

  it('missing_employee_or_date_blocks_save', async () => {
    const { save, fill, fixture } = await setup();
    fill('100');
    fixture.componentInstance.form.controls.date.setValue(null);
    fixture.detectChanges();
    expect(save().disabled).toBe(true);
  });

  it('api_422_shows_message_in_dialog_and_reenables', async () => {
    const { el, save, fill, settle } = await setup();
    fill('100');
    save().click();
    expect(ref.disableClose).toBe(true);
    expect(save().disabled).toBe(false);
    TestBed.inject(HttpTestingController)
      .expectOne('/api/payroll-changes')
      .flush({ title: 'x', detail: 'Zmiana koliduje.' }, { status: 422, statusText: 'Unprocessable' });
    await settle();
    expect(close).not.toHaveBeenCalled();
    expect(ref.disableClose).toBe(false);
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Zmiana koliduje.');
    expect(save().disabled).toBe(false);
  });

  it('double_click_sends_one_request', async () => {
    const { fixture, save, fill, settle } = await setup();
    fill('100');
    save().click();
    fixture.detectChanges();
    expect(save().disabled).toBe(true);
    save().click();
    http.expectOne('/api/payroll-changes').flush(created);
    await settle();
    expect(close).toHaveBeenCalledTimes(1);
  });
});
