import { Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

@Component({
  selector: 'app-month-switcher',
  imports: [MatButtonModule],
  template: `
    <div class="switcher">
      <button mat-icon-button type="button" aria-label="Poprzedni miesiąc" (click)="shift(-1)">‹</button>
      <span class="label" aria-live="polite">{{ label() }}</span>
      <button mat-icon-button type="button" aria-label="Następny miesiąc" [disabled]="isCurrent()" (click)="shift(1)">›</button>
    </div>
  `,
  styles: `
    .switcher { display: inline-flex; align-items: center; gap: 8px; }
    .label { min-width: 9em; text-align: center; text-transform: capitalize; }
  `,
})
export class MonthSwitcher {
  readonly month = input.required<Date>();
  readonly monthChange = output<Date>();

  protected readonly label = computed(() =>
    this.month().toLocaleDateString('pl-PL', { month: 'long', year: 'numeric' }),
  );

  protected readonly isCurrent = computed(() => {
    const m = this.month();
    const now = new Date();
    return m.getFullYear() === now.getFullYear() && m.getMonth() === now.getMonth();
  });

  protected shift(delta: number): void {
    const m = this.month();
    this.monthChange.emit(new Date(m.getFullYear(), m.getMonth() + delta, 1));
  }
}
