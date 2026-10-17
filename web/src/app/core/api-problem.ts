import { HttpErrorResponse } from '@angular/common/http';

export interface ApiProblem {
  status: number;
  title: string;
  detail?: string;
  code?: string;
  ruleId?: string;
}

function isApiProblem(value: unknown): value is ApiProblem {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as ApiProblem).status === 'number' &&
    typeof (value as ApiProblem).title === 'string'
  );
}

function text(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

/** Converts any thrown value to an ApiProblem, reading the raw ProblemDetails body (code/ruleId are extensions). */
export function problemOf(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return isApiProblem(error) ? error : { status: 0, title: 'Wystąpił nieoczekiwany błąd.' };
  }
  if (error.status === 0) {
    return { status: 0, title: 'Brak połączenia z serwerem' };
  }
  const body: unknown = error.error;
  if (typeof body === 'object' && body !== null) {
    const raw = body as Record<string, unknown>;
    const title = text(raw['title']);
    if (title !== undefined) {
      const problem: ApiProblem = { status: error.status, title };
      const detail = text(raw['detail']);
      const code = text(raw['code']);
      const ruleId = text(raw['ruleId']);
      if (detail !== undefined) problem.detail = detail;
      if (code !== undefined) problem.code = code;
      if (ruleId !== undefined) problem.ruleId = ruleId;
      return problem;
    }
  }
  return {
    status: error.status,
    title: error.status >= 500 ? 'Wystąpił błąd serwera.' : 'Żądanie nie powiodło się.',
  };
}

export function messageOf(problem: ApiProblem): string {
  return problem.detail ?? problem.title;
}
