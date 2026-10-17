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
import { AskResponse } from '../../api/models/ask-response';
import { Agent, MAX_QUESTION_LENGTH } from './agent';

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
    const textarea = () => el.querySelector<HTMLTextAreaElement>('textarea');
    const sendButton = () => el.querySelector<HTMLButtonElement>('[data-send]');
    const type = async (text: string) => {
      const t = textarea()!;
      t.value = text;
      t.dispatchEvent(new Event('input'));
      await settle();
    };
    const key = async (shiftKey = false) => {
      textarea()!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', shiftKey, bubbles: true, cancelable: true }));
      await settle();
    };
    const click = async () => {
      sendButton()!.click();
      await settle();
    };
    return { harness, el, settle, row, agent, go, error, router, textarea, sendButton, type, key, click };
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

  describe('sending', () => {
    const ask = { method: 'POST', url: '/api/conversations/messages' };

    function answer(conversationId: string, messages: MessageDto[], ungroundedNumbers: string[] = []): AskResponse {
      return { conversationId, text: messages[messages.length - 1].content, messages, ungroundedNumbers };
    }

    async function openOwn(c = 'c1') {
      const ctx = await setup(['Employee'], `/agent/${c}`);
      http.expectOne(`/api/conversations/${c}`).flush(detail(c, 'ume'));
      await ctx.settle();
      return ctx;
    }

    const userBubbles = (el: HTMLElement) => [...el.querySelectorAll('[data-role="User"]')].map((b) => b.textContent);
    const sendError = (el: HTMLElement) => el.querySelector('[data-send-error]');

    it('exports_the_question_limit', () => {
      expect(MAX_QUESTION_LENGTH).toBe(4000);
    });

    it('first_question_of_a_new_conversation_shows_pending_then_answer_and_switches_url_without_refetch', async () => {
      const { el, settle, router, agent, textarea, sendButton, type, key, click } = await setup();
      const instance = agent();
      expect(el.querySelector('mat-label')!.textContent).toContain('Pytanie do agenta');
      await type('  Ile mam dni urlopu?  ');
      await click();
      expect(userBubbles(el)).toEqual(['Ile mam dni urlopu?']);
      expect(el.querySelector('[data-role="User"]')!.getAttribute('data-message')).toBe('1');
      expect(el.textContent).toContain('Agent pisze…');
      expect(textarea()!.disabled).toBe(true);
      expect(sendButton()!.disabled).toBe(true);
      await click();
      await key();
      const request = http.expectOne(ask);
      expect(request.request.body).toEqual({ conversationId: null, text: 'Ile mam dni urlopu?' });
      request.flush(answer('n1', [msg(1, 'User', 'Ile mam dni urlopu?'), msg(2, 'Assistant', 'Masz 20 dni.')], ['20']));
      await settle();
      http.expectOne('/api/conversations').flush([summary('n1', 'ume'), ...list]);
      await settle();
      expect(router.url).toBe('/agent/n1');
      http.expectNone('/api/conversations/n1');
      expect(agent()).toBe(instance);
      expect(userBubbles(el)).toEqual(['Ile mam dni urlopu?']);
      expect(el.textContent).toContain('Masz 20 dni.');
      expect(el.textContent).not.toContain('Agent pisze…');
      expect(textarea()!.value).toBe('');
      expect(textarea()!.disabled).toBe(false);
      expect(el.querySelector('[data-conversation="n1"]')!.getAttribute('aria-current')).toBe('true');
      expect(el.textContent).toContain('Liczby nie pochodzą z danych systemu: 20.');
      expect(el.querySelector('[data-privacy-note]')).not.toBeNull();
    });

    it('follow_up_in_own_conversation_sends_its_id_and_appends_messages', async () => {
      const { el, settle, router, type, click } = await openOwn();
      await type('Dopytanie');
      await click();
      expect(el.querySelector('[data-message="3"]')!.textContent).toContain('Dopytanie');
      const request = http.expectOne(ask);
      expect(request.request.body).toEqual({ conversationId: 'c1', text: 'Dopytanie' });
      request.flush(answer('c1', [msg(3, 'User', 'Dopytanie'), msg(4, 'Assistant', 'Odpowiedź 2')]));
      await settle();
      http.expectNone('/api/conversations');
      http.expectNone('/api/conversations/c1');
      expect(router.url).toBe('/agent/c1');
      expect(el.querySelectorAll('[data-message]').length).toBe(4);
      expect(userBubbles(el)).toEqual(['Treść c1', 'Dopytanie']);
      expect(el.textContent).toContain('Odpowiedź 2');
    });

    it('enter_sends_and_shift_enter_does_not', async () => {
      const { settle, type, key } = await openOwn();
      await type('Pytanie');
      await key(true);
      http.expectNone(ask);
      await key();
      http.expectOne(ask).flush(answer('c1', [msg(3, 'User', 'Pytanie'), msg(4, 'Assistant', 'Ok')]));
      await settle();
    });

    it('empty_or_whitespace_text_does_not_send', async () => {
      const { sendButton, type, key, click } = await openOwn();
      expect(sendButton()!.disabled).toBe(true);
      await key();
      await type('   \n  ');
      expect(sendButton()!.disabled).toBe(true);
      await click();
      await key();
      http.expectNone(ask);
    });

    it('text_over_the_limit_blocks_sending_and_the_limit_itself_passes', async () => {
      const { el, settle, sendButton, type, key, click } = await openOwn();
      await type('a'.repeat(4001));
      const counter = el.querySelector('[data-counter]')!;
      expect(counter.textContent).toContain('4001/4000');
      expect(counter.classList).toContain('over');
      expect(sendButton()!.disabled).toBe(true);
      await click();
      await key();
      http.expectNone(ask);
      await type('a'.repeat(4000));
      expect(el.querySelector('[data-counter]')!.textContent).toContain('4000/4000');
      expect(el.querySelector('[data-counter]')!.classList).not.toContain('over');
      await click();
      const request = http.expectOne(ask);
      expect((request.request.body as { text: string }).text.length).toBe(4000);
      request.flush(answer('c1', [msg(3, 'User', 'a'.repeat(4000)), msg(4, 'Assistant', 'Ok')]));
      await settle();
    });

    it('ungrounded_numbers_warning_shows_after_answer_and_disappears_on_next_send', async () => {
      const { el, settle, type, click } = await openOwn();
      await type('Ile?');
      await click();
      http.expectOne(ask).flush(answer('c1', [msg(3, 'User', 'Ile?'), msg(4, 'Assistant', 'Masz 12 dni.')], ['12']));
      await settle();
      expect(el.textContent).toContain('Liczby nie pochodzą z danych systemu: 12.');
      await type('A teraz?');
      await click();
      expect(el.textContent).not.toContain('Liczby nie pochodzą z danych systemu');
      http.expectOne(ask).flush(answer('c1', [msg(5, 'User', 'A teraz?'), msg(6, 'Assistant', 'Dobrze.')]));
      await settle();
      expect(el.textContent).not.toContain('Liczby nie pochodzą z danych systemu');
    });

    it('validation_error_keeps_text_and_removes_local_bubble', async () => {
      const { el, settle, textarea, type, click } = await openOwn();
      await type('Pytanie');
      await click();
      http.expectOne(ask).flush({ title: 'Błąd walidacji', detail: 'Pytanie jest nieprawidłowe.', status: 422 }, { status: 422, statusText: 'x' });
      await settle();
      expect(sendError(el)!.textContent).toContain('Pytanie jest nieprawidłowe.');
      expect(textarea()!.value).toBe('Pytanie');
      expect(textarea()!.disabled).toBe(false);
      expect(el.querySelector('[data-message="3"]')).toBeNull();
      expect(el.textContent).not.toContain('Agent pisze…');
    });

    it('conflict_keeps_text_and_reloads_the_conversation', async () => {
      const { el, settle, textarea, type, click } = await openOwn();
      await type('Pytanie');
      await click();
      http.expectOne(ask).flush({ title: 'Rozmowa została zmieniona.', status: 409, conversationId: 'c1' }, { status: 409, statusText: 'x' });
      await settle();
      http.expectOne('/api/conversations/c1').flush({
        ...detail('c1', 'ume'),
        messages: [...detail('c1', 'ume').messages, msg(3, 'User', 'Inne pytanie'), msg(4, 'Assistant', 'Inna odpowiedź')],
      });
      await settle();
      expect(el.textContent).toContain('Inna odpowiedź');
      expect(sendError(el)!.textContent).toContain('Rozmowa została zmieniona.');
      expect(textarea()!.value).toBe('Pytanie');
      expect(textarea()!.disabled).toBe(false);
    });

    it('unavailable_agent_in_new_conversation_reloads_it_switches_url_and_next_send_uses_its_id', async () => {
      const { el, settle, router, textarea, type, click } = await setup();
      await type('Pytanie');
      await click();
      http.expectOne(ask).flush({ title: 'Agent jest chwilowo niedostępny.', status: 503, conversationId: 'n2' }, { status: 503, statusText: 'x' });
      await settle();
      http.expectOne('/api/conversations/n2').flush({ id: 'n2', userId: 'ume', startedAtUtc: '2026-10-05T10:30:00Z', messages: [msg(1, 'User', 'Pytanie')] });
      await settle();
      http.expectOne('/api/conversations').flush([summary('n2', 'ume'), ...list]);
      await settle();
      expect(router.url).toBe('/agent/n2');
      http.expectNone('/api/conversations/n2');
      expect(userBubbles(el)).toEqual(['Pytanie']);
      expect(sendError(el)!.textContent).toContain('Agent jest chwilowo niedostępny.');
      expect(textarea()!.value).toBe('Pytanie');
      expect(textarea()!.disabled).toBe(false);
      await click();
      const retry = http.expectOne(ask);
      expect(retry.request.body).toEqual({ conversationId: 'n2', text: 'Pytanie' });
      retry.flush(answer('n2', [msg(2, 'User', 'Pytanie'), msg(3, 'Assistant', 'Odpowiedź')]));
      await settle();
      expect(sendError(el)).toBeNull();
      expect(textarea()!.value).toBe('');
      expect(el.textContent).toContain('Odpowiedź');
    });

    it('not_found_shows_snackbar_and_navigates_to_agent', async () => {
      const { settle, router, error, type, click } = await openOwn();
      await type('Pytanie');
      await click();
      http.expectOne(ask).flush({ title: 'Nie znaleziono', status: 404 }, { status: 404, statusText: 'x' });
      await settle();
      expect(error).toHaveBeenCalledWith('Nie znaleziono rozmowy.');
      expect(router.url).toBe('/agent');
    });

    it('network_error_keeps_text_and_shows_message', async () => {
      const { el, settle, textarea, type, click } = await openOwn();
      await type('Pytanie');
      await click();
      http.expectOne(ask).error(new ProgressEvent('error'));
      await settle();
      expect(sendError(el)!.textContent).toContain('Brak połączenia z serwerem');
      expect(textarea()!.value).toBe('Pytanie');
      expect(textarea()!.disabled).toBe(false);
      expect(el.querySelector('[data-message="3"]')).toBeNull();
    });

    it('switching_conversation_while_sending_ignores_the_late_answer', async () => {
      const { el, settle, router, row, textarea, type, click } = await setup();
      await type('Pytanie');
      await click();
      const request = http.expectOne(ask);
      row('c1')!.click();
      await settle();
      http.expectOne('/api/conversations/c1').flush(detail('c1', 'ume'));
      await settle();
      expect(textarea()!.disabled).toBe(false);
      expect(el.textContent).not.toContain('Agent pisze…');
      request.flush(answer('n1', [msg(1, 'User', 'Pytanie'), msg(2, 'Assistant', 'Spóźniona odpowiedź')]));
      await settle();
      http.expectNone('/api/conversations');
      expect(router.url).toBe('/agent/c1');
      expect(el.textContent).toContain('Treść c1');
      expect(el.textContent).not.toContain('Spóźniona odpowiedź');
    });

    it('leaving_the_screen_while_sending_ignores_the_late_answer', async () => {
      const { settle, router, go, type, click } = await setup();
      await type('Pytanie');
      await click();
      const request = http.expectOne(ask);
      await go('/inne');
      request.flush(answer('n1', [msg(1, 'User', 'Pytanie'), msg(2, 'Assistant', 'Odpowiedź')]));
      await settle();
      http.expectNone('/api/conversations');
      expect(router.url).toBe('/inne');
    });

    it('new_conversation_while_sending_ignores_the_late_error', async () => {
      const { el, settle, router, textarea, type, click } = await openOwn();
      await type('Pytanie');
      await click();
      const request = http.expectOne(ask);
      const create = [...el.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'Nowa rozmowa')!;
      create.click();
      await settle();
      expect(router.url).toBe('/agent');
      expect(textarea()!.disabled).toBe(false);
      expect(el.textContent).not.toContain('Agent pisze…');
      expect(userBubbles(el)).toEqual([]);
      request.flush({ title: 'Agent jest chwilowo niedostępny.', status: 503, conversationId: 'c1' }, { status: 503, statusText: 'x' });
      await settle();
      http.expectNone('/api/conversations/c1');
      expect(sendError(el)).toBeNull();
      expect(router.url).toBe('/agent');
    });
  });
});
