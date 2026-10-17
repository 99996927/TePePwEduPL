import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { provideNativeDateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { provideApiConfiguration } from '../../api/api-configuration';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { Notifier } from '../../core/notifier';
import { NewLeaveDialog } from './new-leave-dialog';

const created: LeaveRequestDto = {
  id: 'n1', employeeId: 'e1', status: 'Pending', from: '2026-11-02', to: '2026-11-06', days: 5,
  createdAtUtc: '2026-10-17T00:00:00Z', decidedByEmployeeId: null, decisionReason: null,
};

describe('NewLeaveDialog', () => {
  let http: HttpTestingController;
  const close = vi.fn();
  const ref = { close, disableClose: false };

  function setup() {
    close.mockReset();
    ref.disableClose = false;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideNativeDateAdapter(),
        { provide: MAT_DATE_LOCALE, useValue: 'pl-PL' },
        { provide: MatDialogRef, useValue: ref },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(NewLeaveDialog);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const save = () => el.querySelector('[data-save]') as HTMLButtonElement;
    const pick = (from: Date | null, to: Date | null) => {
      fixture.componentInstance.range.controls.start.setValue(from);
      fixture.componentInstance.range.controls.end.setValue(to);
      fixture.detectChanges();
    };
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, el, save, pick, settle };
  }

  afterEach(() => http.verify());

  it('save_disabled_until_both_dates_are_chosen', () => {
    const { save, pick } = setup();
    expect(save().disabled).toBe(true);
    pick(new Date(2026, 10, 2), null);
    expect(save().disabled).toBe(true);
    pick(new Date(2026, 10, 2), new Date(2026, 10, 6));
    expect(save().disabled).toBe(false);
  });

  it('range_is_bound_to_a_date_range_picker', () => {
    const { el } = setup();
    expect(el.querySelector('mat-date-range-input')).toBeTruthy();
    expect(el.querySelector('mat-date-range-picker, mat-datepicker-toggle')).toBeTruthy();
  });

  it('submits_local_dates_without_timezone_shift_and_closes_with_result', async () => {
    const { save, pick, settle } = setup();
    const info = vi.spyOn(TestBed.inject(Notifier), 'info').mockImplementation(() => undefined);
    pick(new Date(2026, 10, 2), new Date(2026, 10, 6));
    save().click();
    const post = http.expectOne('/api/leave/requests');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ from: '2026-11-02', to: '2026-11-06' });
    post.flush(created);
    await settle();
    expect(close).toHaveBeenCalledWith(created);
    expect(info).toHaveBeenCalledWith('Wniosek złożony: 5 dni roboczych.');
  });

  it('shows_422_detail_keeps_dialog_open_and_ignores_second_click_while_sending', async () => {
    const { el, save, pick, settle } = setup();
    pick(new Date(2026, 10, 2), new Date(2026, 10, 6));
    save().click();
    save().click();
    await settle();
    expect(save().disabled).toBe(true);
    expect(ref.disableClose).toBe(true);
    const post = http.expectOne('/api/leave/requests');
    post.flush(
      { title: 'Błąd walidacji', status: 422, detail: 'Niewystarczające saldo urlopu: brakuje 3 dni.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await settle();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Niewystarczające saldo urlopu: brakuje 3 dni.');
    expect(close).not.toHaveBeenCalled();
    expect(ref.disableClose).toBe(false);
    expect(save().disabled).toBe(false);
  });

  it('shows_api_message_for_year_crossing_range_and_409', async () => {
    const { el, pick, save, settle } = setup();
    pick(new Date(2026, 11, 28), new Date(2027, 0, 5));
    save().click();
    http.expectOne('/api/leave/requests').flush(
      { title: 'Zakres przez granicę roku', status: 422, detail: 'Wniosek nie może obejmować dwóch lat.' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await settle();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Wniosek nie może obejmować dwóch lat.');

    save().click();
    http.expectOne('/api/leave/requests').flush(
      { title: 'Konflikt', status: 409, detail: 'Wniosek nakłada się na istniejący.' },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Wniosek nakłada się na istniejący.');
    expect(close).not.toHaveBeenCalled();
  });
});
