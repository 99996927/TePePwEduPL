import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Subscription } from 'rxjs';
import { TimesheetAnalysisDto } from '../../api/models/timesheet-analysis-dto';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate, formatHours, formatTime, monthRange } from '../../core/date-format';

type State =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: TimesheetAnalysisDto };

const RULE_LABELS: Record<string, string> = {
  'KP-132-1': 'Odpoczynek dobowy',
  'KP-133-1': 'Odpoczynek tygodniowy',
};

@Component({
  selector: 'app-analysis-panel',
  imports: [MatButtonModule, MatProgressBarModule],
  templateUrl: './analysis-panel.html',
  styleUrl: './analysis-panel.scss',
})
export class AnalysisPanel {
  private readonly api = inject(TimesheetService);

  readonly employeeId = input.required<string | null>();
  readonly month = input.required<Date>();

  protected readonly state = signal<State>({ status: 'loading' });
  protected readonly formatHours = formatHours;
  protected readonly formatDate = formatDate;
  protected readonly formatTime = formatTime;
  protected readonly weeklyLimit = 48;
  protected readonly annualLimit = 150;

  private subscription: Subscription | null = null;

  constructor() {
    effect(() => {
      this.employeeId();
      this.month();
      untracked(() => this.load());
    });
    inject(DestroyRef).onDestroy(() => this.subscription?.unsubscribe());
  }

  protected label(ruleId: string): string {
    return RULE_LABELS[ruleId] ?? 'Naruszenie odpoczynku';
  }

  protected load(): void {
    // Unsubscribing cancels the superseded request, so a stale response can never be applied.
    this.subscription?.unsubscribe();
    this.state.set({ status: 'loading' });
    const employeeId = this.employeeId();
    this.subscription = this.api
      .timesheetAnalyze({ ...(employeeId ? { employeeId } : {}), ...monthRange(this.month()) })
      .subscribe({
        next: (data) => this.state.set({ status: 'ready', data }),
        error: (e: unknown) => this.state.set({ status: 'error', message: messageOf(problemOf(e)) }),
      });
  }
}
