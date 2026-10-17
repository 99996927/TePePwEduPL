import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MessageDto } from '../../api/models/message-dto';
import { ConversationThread } from './conversation-thread';

function msg(seq: number, role: string, content: string, over: Partial<MessageDto> = {}): MessageDto {
  return { seq, role, content, toolName: null, toolParams: null, createdAtUtc: '2026-10-05T10:30:00Z', ...over };
}

describe('ConversationThread', () => {
  function setup(inputs: Partial<Record<string, unknown>> = {}) {
    TestBed.configureTestingModule({ providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }] });
    const fixture: ComponentFixture<ConversationThread> = TestBed.createComponent(ConversationThread);
    const base: Record<string, unknown> = { messages: [], pending: false, ungroundedNumbers: [], loading: false, emptyHint: 'Zadaj pytanie.' };
    for (const [k, v] of Object.entries({ ...base, ...inputs })) fixture.componentRef.setInput(k, v);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    return { fixture, el, bubble: (seq: number) => el.querySelector<HTMLElement>(`[data-message="${seq}"]`) };
  }

  it('renders user and assistant bubbles in order with roles', () => {
    const { el, bubble } = setup({ messages: [msg(1, 'User', 'Pytanie'), msg(2, 'Assistant', 'Odpowiedź')] });
    expect([...el.querySelectorAll('[data-message]')].map((b) => b.getAttribute('data-message'))).toEqual(['1', '2']);
    expect(bubble(1)!.getAttribute('data-role')).toBe('User');
    expect(bubble(1)!.classList).toContain('user');
    expect(bubble(1)!.textContent).toContain('Pytanie');
    expect(bubble(2)!.getAttribute('data-role')).toBe('Assistant');
    expect(bubble(2)!.classList).toContain('assistant');
  });

  it('renders tool call and result as collapsed details with JSON', () => {
    const { bubble } = setup({
      messages: [
        msg(1, 'ToolCall', 'call-1', { toolName: 'get_leave_balance', toolParams: '{"a":1}' }),
        msg(2, 'ToolResult', '{"days":5}', { toolName: 'get_leave_balance' }),
        msg(3, 'ToolCall', 'call-2', { toolName: 'x', toolParams: null }),
      ],
    });
    const call = bubble(1)!;
    expect(call.tagName).toBe('DETAILS');
    expect(call.hasAttribute('open')).toBe(false);
    expect(call.querySelector('summary')!.textContent!.trim()).toBe('Wywołanie narzędzia: get_leave_balance');
    expect(call.querySelector('pre')!.textContent).toBe('{"a":1}');
    const result = bubble(2)!;
    expect(result.tagName).toBe('DETAILS');
    expect(result.hasAttribute('open')).toBe(false);
    expect(result.querySelector('summary')!.textContent!.trim()).toBe('Wynik narzędzia: get_leave_balance');
    expect(result.querySelector('pre')!.textContent).toBe('{"days":5}');
    expect(bubble(3)!.querySelector('pre')!.textContent).toBe('{}');
  });

  it('renders HTML and long words as text only', () => {
    const content = `<script>alert(1)</script> ${'x'.repeat(500)}`;
    const { el, bubble } = setup({ messages: [msg(1, 'Assistant', content)] });
    expect(el.querySelector('script')).toBeNull();
    expect(bubble(1)!.textContent!.trim()).toBe(content);
  });

  it('shows pending indicator only when pending', () => {
    const { fixture, el } = setup({ messages: [msg(1, 'User', 'Q')], pending: true });
    expect(el.textContent).toContain('Agent pisze…');
    fixture.componentRef.setInput('pending', false);
    fixture.detectChanges();
    expect(el.textContent).not.toContain('Agent pisze…');
  });

  it('shows ungrounded warning under the last assistant bubble', () => {
    const { el } = setup({
      messages: [msg(1, 'Assistant', 'a'), msg(2, 'User', 'q'), msg(3, 'Assistant', 'b')],
      ungroundedNumbers: ['12', '3,5'],
    });
    const alert = el.querySelector('[role="alert"]')!;
    expect(alert.textContent!.trim()).toBe('Liczby nie pochodzą z danych systemu: 12, 3,5.');
    expect(alert.previousElementSibling!.getAttribute('data-message')).toBe('3');
  });

  it('shows no warning for empty list', () => {
    const { el } = setup({ messages: [msg(1, 'Assistant', 'a')] });
    expect(el.querySelector('[role="alert"]')).toBeNull();
  });

  it('shows emptyHint only for empty list', () => {
    const empty = setup();
    expect(empty.el.textContent).toContain('Zadaj pytanie.');
    TestBed.resetTestingModule();
    const filled = setup({ messages: [msg(1, 'User', 'Q')] });
    expect(filled.el.textContent).not.toContain('Zadaj pytanie.');
  });

  it('bubbles_carry_bem_role_modifiers', () => {
    const { bubble } = setup({ messages: [msg(1, 'User', 'Q'), msg(2, 'Assistant', 'A')] });
    expect(bubble(1)!.classList).toContain('bubble--user');
    expect(bubble(1)!.classList).not.toContain('bubble--assistant');
    expect(bubble(2)!.classList).toContain('bubble--assistant');
  });
});
