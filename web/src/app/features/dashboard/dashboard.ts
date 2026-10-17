import { Component, WritableSignal, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { LeaveBalanceDto } from '../../api/models/leave-balance-dto';
import { TimesheetAnalysisDto } from '../../api/models/timesheet-analysis-dto';
import { LeaveService } from '../../api/services/leave.service';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { toDateOnly, formatHours } from '../../core/date-format';
import { APPROVER_ROLES } from '../../core/roles';
import { SessionService } from '../../core/session';

type Tile<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: T };

const loading = { status: 'loading' } as const;

@Component({
  selector: 'app-dashboard',
  imports: [MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly leave = inject(LeaveService);
  private readonly timesheet = inject(TimesheetService);
  private readonly session = inject(SessionService);

  protected readonly formatHours = formatHours;
  protected readonly isApprover = this.session.hasRole(...APPROVER_ROLES);

  protected readonly balance = signal<Tile<LeaveBalanceDto>>(loading);
  protected readonly analysis = signal<Tile<TimesheetAnalysisDto>>(loading);
  protected readonly approvals = signal<Tile<number>>(loading);

  protected readonly alerts = computed(() => {
    const tile = this.analysis();
    if (tile.status !== 'ready') return null;
    const { restViolations, weeklyAverage, annualOvertimeLimitExceeded } = tile.data;
    return {
      restViolations: restViolations.length,
      weeklyExceeded: weeklyAverage.exceedsLimit,
      annualExceeded: annualOvertimeLimitExceeded,
      none: restViolations.length === 0 && !weeklyAverage.exceedsLimit && !annualOvertimeLimitExceeded,
    };
  });

  constructor() {
    this.loadBalance();
    this.loadAnalysis();
    if (this.isApprover) this.loadApprovals();
  }

  protected loadBalance(): void {
    void this.run(this.balance, this.leave.leaveBalance(), (data) => data);
  }

  protected loadAnalysis(): void {
    const now = new Date();
    const from = toDateOnly(new Date(now.getFullYear(), now.getMonth(), 1));
    void this.run(this.analysis, this.timesheet.timesheetAnalyze({ from, to: toDateOnly(now) }), (data) => data);
  }

  protected loadApprovals(): void {
    const own = this.session.user()?.employeeId;
    void this.run(this.approvals, this.leave.leaveList(), (requests) =>
      requests.filter((r) => r.status === 'Pending' && r.employeeId !== own).length,
    );
  }

  private async run<S, T>(target: WritableSignal<Tile<T>>, source: Observable<S>, map: (value: S) => T): Promise<void> {
    target.set(loading);
    try {
      target.set({ status: 'ready', data: map(await firstValueFrom(source)) });
    } catch (error) {
      target.set({ status: 'error', message: messageOf(problemOf(error)) });
    }
  }
}
