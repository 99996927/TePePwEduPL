import { provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { By } from '@angular/platform-browser';
import { Router, UrlMatcher, UrlSegment, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideApiConfiguration } from '../../api/api-configuration';
import { ConversationDetailDto } from '../../api/models/conversation-detail-dto';
import { ConversationSummaryDto } from '../../api/models/conversation-summary-dto';
import { CurrentUserDto } from '../../api/models/current-user-dto';
import { MessageDto } from '../../api/models/message-dto';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { Agent } from './agent';

// Same shape as the app matcher: /agent and /agent/<id> share one route entry (one component instance).
const matcher: UrlMatcher = (segments: UrlSegment[]) =>
  segments[0]?.path === 'agent' && segments.length <= 2
    ? { consumed: segments, posParams: segments.length === 2 ? { id: segments[1] } : undefined }
    : null;

const employees = [
  { id: 'e1', firstName: 'Anna', lastName: 'Nowak', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'ume' },
  { id: 'e2', firstName: 'Jan', lastName: 'Kowalski', hireDate: '2020-01-01', teamId: 't1', teamName: 'A', userId: 'uo' },
];

function summary(id: string, userId: string): ConversationSummaryDto {
  return { id, userId, startedAtUtc: '2026-10-05T10:30:00Z', preview: `Pytanie ${id}`, messageCount: 2 };
}

function msg(seq: number, role: string, content: string): MessageDto {
  return { seq, role, content, createdAtUtc: '2026-10-05T10:30:00Z', toolName: null, toolParams: null };
}

function detail(id: string, userId: string, text = `Treść ${id}`): ConversationDetailDto {
  return { id, userId, startedAtUtc: '2026-10-05T10:30:00Z', messages: [msg(1, 'User', text), msg(2, 'Assistant', `Odpowiedź ${id}`)] };
}

const list = [summary('c1', 'ume'), summary('c2', 'uo')];

describe('Agent', () => {
  let http: HttpTestingController;

  async function setup(roles: string[] = ['Employee'], url = '/agent', conversations: ConversationSummaryDto[] | 'error' = list) {
    const user = signal<CurrentUserDto | null>({ userId: 'ume', employeeId: 'e1', userName: 'anna', displayName: 'Anna', roles });
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ matcher, component: Agent }, { path: '**', children: [] }]),
        provideHttpClient(withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' })),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
        { provide: SessionService, useValue: { user: user.asReadonly(), hasRole: (...r: string[]) => r.some((x) => roles.includes(x)) } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const error = vi.spyOn(TestBed.inject(Notifier), 'error').mockImplementation(() => undefined);
    const router = TestBed.inject(Router);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    http.expectOne('/api/employees').flush(employees);
    const listRequest = http.expectOne('/api/conversations');
    if (conversations === 'error') listRequest.flush({ title: 'Awaria listy', status: 500 }, { status: 500, statusText: 'x' });
    else listRequest.flush(conversations);
    const settle = async () => {
      await harness.fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve));
      harness.detectChanges();
    };
    await settle();
    const el = harness.routeNativeElement as HTMLElement;
    const row = (id: string) => el.querySelector<HTMLButtonElement>(`[data-conversation="${id}"]`);
    const agent = () => harness.fixture.debugElement.query(By.directive(Agent)).componentInstance as Agent;
    const go = async (target: string) => {
      await harness.navigateByUrl(target);
      await settle();
    };
    return { harness, el, settle, row, agent, go, error, router };
  }

  afterEach(() => {
    http.verify();
  });

  it('employee_loads_list_once_without_author_or_toggle_and_shows_empty_hint', async () => {
    const { el, row } = await setup();
    http.expectNone('/api/conversations');
    expect(row('c1')).not.toBeNull();
    expect(el.querySelector('mat-slide-toggle')).toBeNull();
    expect(el.querySelector('.author')).toBeNull();
    expect(el.textContent).toContain('Zadaj pytanie agentowi, np. „Ile mam dni urlopu?”.');
    expect(el.querySelector('[data-privacy-note]')!.textContent).toContain('Kadry mogą czytać rozmowy z agentem.');
    expect(el.querySelector('[data-composer-slot]')).not.toBeNull();
    expect(el.querySelector('[data-readonly-banner]')).toBeNull();
  });

  it('selecting_a_row_navigates_and_loads_the_conversation', async () => {
    const { el, row, settle, router } = await setup();
    row('c1')!.click();
    await settle();
    expect(router.url).toBe('/agent/c1');
    http.expectOne('/api/conversations/c1').flush(detail('c1', 'ume'));
    await settle();
    expect(el.textContent).toContain('Treść c1');
    expect(row('c1')!.getAttribute('aria-current')).toBe('true');
    expect(el.querySelector('[data-privacy-note]')).not.toBeNull();
    expect(el.querySelector('[data-readonly-banner]')).toBeNull();
  });

  it('new_conversation_button_navigates_to_agent', async () => {
    const { el, settle, router } = await setup(['Employee'], '/agent/c1');
    http.expectOne('/api/conversations/c1').flush(detail('c1', 'ume'));
    await settle();
    const create = [...el.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'Nowa rozmowa')!;
    create.click();
    await settle();
    expect(router.url).toBe('/agent');
    expect(el.textContent).not.toContain('Treść c1');
    expect(el.textContent).toContain('Zadaj pytanie agentowi');
  });

  it('hr_sees_authors_and_only_mine_hides_others', async () => {
    const { el, row, settle } = await setup(['HR']);
    expect(row('c2')!.textContent).toContain('Jan Kowalski');
    const toggle = el.querySelector('mat-slide-toggle button') as HTMLButtonElement;
    toggle.click();
    await settle();
    expect(row('c1')).not.toBeNull();
    expect(row('c2')).toBeNull();
  });

  it('hr_opening_someone_elses_conversation_is_read_only', async () => {
    const { el, settle } = await setup(['HR'], '/agent/c2');
    http.expectOne('/api/conversations/c2').flush(detail('c2', 'uo'));
    await settle();
    const banner = el.querySelector('[data-readonly-banner]')!;
    expect(banner.getAttribute('role')).toBe('status');
    expect(banner.textContent).toContain('Czytasz rozmowę: Jan Kowalski. Odczyt jest zapisany w audycie.');
    expect(el.querySelector('textarea')).toBeNull();
    expect(el.querySelector('[data-send]')).toBeNull();
    expect(el.querySelector('[data-composer-slot]')).toBeNull();
    expect(el.querySelector('[data-privacy-note]')).toBeNull();
  });

  it('hr_opening_own_conversation_shows_privacy_note', async () => {
    const { el, settle } = await setup(['HR'], '/agent/c1');
    http.expectOne('/api/conversations/c1').flush(detail('c1', 'ume'));
    await settle();
    expect(el.querySelector('[data-privacy-note]')).not.toBeNull();
    expect(el.querySelector('[data-composer-slot]')).not.toBeNull();
    expect(el.querySelector('[data-readonly-banner]')).toBeNull();
  });

  it('not_found_shows_snackbar_and_navigates_to_agent', async () => {
    const { settle, error, router } = await setup(['Employee'], '/agent/obca');
    http.expectOne('/api/conversations/obca').flush({ title: 'Nie znaleziono', status: 404 }, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(error).toHaveBeenCalledWith('Nie znaleziono rozmowy.');
    expect(router.url).toBe('/agent');
  });

  it('other_conversation_error_shows_message_with_retry', async () => {
    const { el, settle } = await setup(['Employee'], '/agent/c1');
    http.expectOne('/api/conversations/c1').flush({ title: 'Awaria', status: 500 }, { status: 500, statusText: 'x' });
    await settle();
    const alert = el.querySelector('[data-conversation-error]')!;
    expect(alert.textContent).toContain('Awaria');
    (alert.querySelector('button') as HTMLButtonElement).click();
    http.expectOne('/api/conversations/c1').flush(detail('c1', 'ume'));
    await settle();
    expect(el.querySelector('[data-conversation-error]')).toBeNull();
    expect(el.textContent).toContain('Treść c1');
  });

  it('list_load_error_shows_retry_that_repeats_the_request', async () => {
    const { el, settle, row } = await setup(['Employee'], '/agent', 'error');
    const listEl = el.querySelector('app-conversation-list')!;
    expect(listEl.querySelector('[role="alert"]')!.textContent).toContain('Awaria listy');
    const retry = [...listEl.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'Spróbuj ponownie')!;
    retry.click();
    http.expectOne('/api/conversations').flush(list);
    await settle();
    expect(listEl.querySelector('[role="alert"]')).toBeNull();
    expect(row('c1')).not.toBeNull();
  });

  it('fast_switch_ignores_the_stale_response', async () => {
    const { el, go } = await setup(['HR']);
    await go('/agent/c1');
    const first = http.expectOne('/api/conversations/c1');
    await go('/agent/c2');
    const second = http.expectOne('/api/conversations/c2');
    second.flush(detail('c2', 'uo'));
    first.flush(detail('c1', 'ume'));
    await go('/agent/c2'); // no-op navigation, just settles
    expect(el.textContent).toContain('Treść c2');
    expect(el.textContent).not.toContain('Treść c1');
    expect(el.querySelector('[data-readonly-banner]')).not.toBeNull();
  });

  it('route_change_to_a_locally_loaded_id_does_not_fetch_it_again', async () => {
    const { el, go, settle, agent } = await setup();
    agent()['openLocal']('c9', [msg(1, 'User', 'Lokalne pytanie'), msg(2, 'Assistant', 'Lokalna odpowiedź')], 'ume');
    await settle();
    await go('/agent/c9');
    http.expectNone('/api/conversations/c9');
    expect(el.textContent).toContain('Lokalna odpowiedź');
    expect(el.querySelector('[data-privacy-note]')).not.toBeNull();
  });
});
