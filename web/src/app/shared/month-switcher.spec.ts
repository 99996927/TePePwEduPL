import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MonthSwitcher } from './month-switcher';

@Component({
  imports: [MonthSwitcher],
  template: '<app-month-switcher [month]="month()" (monthChange)="changed.push($event)" />',
})
class Host {
  readonly month = signal(new Date(2026, 8, 1));
  readonly changed: Date[] = [];
}

describe('MonthSwitcher', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 16, 12));
  });

  afterEach(() => vi.useRealTimers());

  function render(month: Date) {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.month.set(month);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const buttons = Array.from(el.querySelectorAll('button'));
    const prev = buttons.find((b) => b.textContent?.includes('‹'))!;
    const next = buttons.find((b) => b.textContent?.includes('›'))!;
    return { fixture, el, prev, next };
  }

  it('shows_month_label', () => {
    const { el } = render(new Date(2026, 9, 1));
    expect(el.textContent).toContain('październik 2026');
  });

  it('next_emits_following_month', () => {
    const { fixture, next } = render(new Date(2026, 8, 1));
    next.click();
    expect(fixture.componentInstance.changed).toEqual([new Date(2026, 9, 1)]);
  });

  it('prev_emits_previous_month', () => {
    const { fixture, prev } = render(new Date(2026, 8, 1));
    prev.click();
    expect(fixture.componentInstance.changed).toEqual([new Date(2026, 7, 1)]);
  });

  it('next_disabled_for_current_month', () => {
    const { next, prev } = render(new Date(2026, 9, 1));
    expect(next.disabled).toBe(true);
    expect(prev.disabled).toBe(false);
  });

  it('next_enabled_for_past_month', () => {
    expect(render(new Date(2026, 8, 1)).next.disabled).toBe(false);
  });
});
