import { HttpErrorResponse } from '@angular/common/http';
import { messageOf, problemOf } from './api-problem';

describe('problemOf', () => {
  it('problemOf_body_with_conversationId_returns_it', () => {
    const error = new HttpErrorResponse({ status: 503, error: { title: 'Agent', detail: 'd', conversationId: 'c1' } });

    expect(problemOf(error)).toEqual({ status: 503, title: 'Agent', detail: 'd', conversationId: 'c1' });
  });

  it.each([[undefined], [5], [''], [null]])('problemOf_conversationId_%s_is_omitted', (value) => {
    const error = new HttpErrorResponse({ status: 503, error: { title: 'Agent', conversationId: value } });

    expect('conversationId' in problemOf(error)).toBe(false);
  });

  it('problemOf_422_with_body_returns_all_fields', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: {
        title: 'Walidacja',
        detail: 'Niewystarczające saldo urlopu: brakuje 3 dni.',
        code: 'Validation',
        ruleId: 'KP-154-1',
      },
    });

    expect(problemOf(error)).toEqual({
      status: 422,
      title: 'Walidacja',
      detail: 'Niewystarczające saldo urlopu: brakuje 3 dni.',
      code: 'Validation',
      ruleId: 'KP-154-1',
    });
  });

  it('problemOf_status_0_returns_connection_message', () => {
    const problem = problemOf(new HttpErrorResponse({ status: 0 }));

    expect(problem).toEqual({ status: 0, title: 'Brak połączenia z serwerem' });
  });

  it('problemOf_500_without_body_returns_server_error', () => {
    const problem = problemOf(new HttpErrorResponse({ status: 500 }));

    expect(problem).toEqual({ status: 500, title: 'Wystąpił błąd serwera.' });
  });

  it('problemOf_400_without_body_returns_generic_failure', () => {
    expect(problemOf(new HttpErrorResponse({ status: 400 })).title).toBe('Żądanie nie powiodło się.');
  });

  it('problemOf_passes_through_existing_problem', () => {
    const problem = { status: 423, title: 'x', code: 'LockedOut' };

    expect(problemOf(problem)).toBe(problem);
  });

  it('messageOf_prefers_detail_over_title', () => {
    expect(messageOf({ status: 422, title: 'T', detail: 'D' })).toBe('D');
    expect(messageOf({ status: 422, title: 'T' })).toBe('T');
  });
});
