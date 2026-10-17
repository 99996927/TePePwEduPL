import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { IS_DEV_MODE } from '../../core/dev-mode';
import { SessionService } from '../../core/session';
import { Login } from './login';

@Component({ template: '' })
class Blank {}

describe('Login', () => {
  const login = vi.fn();
  // A plain function drives the outcome: vi.fn records the returned promise and would report a rejection as unhandled.
  let outcome: () => Promise<void> = () => Promise.resolve();
  let navigate: ReturnType<typeof vi.spyOn>;

  async function open(url = '/logowanie', devMode = false) {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'logowanie', component: Login }, { path: '**', component: Blank }]),
        { provide: SessionService, useValue: { login: (u: string, p: string) => (login(u, p), outcome()) } },
        { provide: IS_DEV_MODE, useValue: devMode },
      ],
    });
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url, Login);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    harness.detectChanges();
    return { harness, component, el: harness.routeNativeElement as HTMLElement };
  }

  const input = (el: HTMLElement, name: string) =>
    el.querySelector<HTMLInputElement>(`input[formcontrolname="${name}"]`)!;

  function type(el: HTMLElement, userName: string, password: string) {
    for (const [name, value] of [['userName', userName], ['password', password]]) {
      const field = input(el, name);
      field.value = value;
      field.dispatchEvent(new Event('input'));
    }
  }

  const submit = (el: HTMLElement) => el.querySelector<HTMLButtonElement>('button[type="submit"]')!;

  beforeEach(() => {
    login.mockReset();
    outcome = () => Promise.resolve();
  });

  it('submit_success_logs_in_and_navigates_to_root', async () => {
    const { harness, el } = await open();
    type(el, 'anna.nowak', 'annanowak123');
    harness.detectChanges();

    submit(el).click();
    await harness.fixture.whenStable();

    expect(login).toHaveBeenCalledWith('anna.nowak', 'annanowak123');
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('submit_success_with_return_url_navigates_there', async () => {
    const { harness, el } = await open('/logowanie?returnUrl=%2Furlopy');
    type(el, 'a', 'b');
    harness.detectChanges();

    submit(el).click();
    await harness.fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/urlopy');
  });

  it('submit_external_return_url_is_ignored', async () => {
    const { harness, el } = await open('/logowanie?returnUrl=%2F%2Fevil.example');
    type(el, 'a', 'b');
    harness.detectChanges();

    submit(el).click();
    await harness.fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('submit_invalid_credentials_shows_message_and_keeps_login', async () => {
    outcome = () => Promise.reject({ status: 401, title: 'InvalidCredentials' });
    const { harness, el } = await open();
    type(el, 'anna.nowak', 'zle');
    harness.detectChanges();

    submit(el).click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(el.textContent).toContain('Login lub hasło są nieprawidłowe.');
    expect(input(el, 'userName').value).toBe('anna.nowak');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('submit_locked_account_shows_locked_message', async () => {
    outcome = () => Promise.reject({ status: 423, title: 'Locked' });
    const { harness, el } = await open();
    type(el, 'anna.nowak', 'x');
    harness.detectChanges();

    submit(el).click();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(el.textContent).toContain('zablokowane');
  });

  it('empty_fields_disable_submit_and_do_not_log_in', async () => {
    const { harness, el } = await open();

    expect(submit(el).disabled).toBe(true);
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
    expect(login).not.toHaveBeenCalled();

    type(el, 'anna.nowak', '');
    harness.detectChanges();
    expect(submit(el).disabled).toBe(true);

    type(el, 'anna.nowak', 'x');
    harness.detectChanges();
    expect(submit(el).disabled).toBe(false);
  });

  it('expired_session_query_shows_message', async () => {
    const { el } = await open('/logowanie?sesja=wygasla');

    expect(el.textContent).toContain('Sesja wygasła. Zaloguj się ponownie.');
  });

  it('no_expired_query_hides_message', async () => {
    const { el } = await open();

    expect(el.textContent).not.toContain('Sesja wygasła');
  });

  it('demo_panel_hidden_outside_dev_mode', async () => {
    const { el } = await open('/logowanie', false);

    expect(el.querySelector('.demo-account')).toBeNull();
    expect(el.textContent).not.toContain('Konta demo');
  });

  it('demo_panel_in_dev_mode_lists_six_accounts_and_fills_form', async () => {
    const { harness, el } = await open('/logowanie', true);
    const rows = Array.from(el.querySelectorAll<HTMLElement>('.demo-account'));

    expect(el.textContent).toContain('Konta demo');
    expect(rows.map((r) => r.textContent)).toEqual([
      expect.stringContaining('anna.nowak'),
      expect.stringContaining('jan.kowalski'),
      expect.stringContaining('tomasz.lis'),
      expect.stringContaining('ewa.wisniewska'),
      expect.stringContaining('piotr.zielinski'),
      expect.stringContaining('maria.kaminska'),
    ]);

    rows[3].click();
    harness.detectChanges();

    expect(input(el, 'userName').value).toBe('ewa.wisniewska');
    expect(input(el, 'password').value).toBe('ewawisniewska123');
  });
});
