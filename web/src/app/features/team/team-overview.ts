import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { Subject, Subscription, catchError, map, mergeMap, of } from 'rxjs';
import { TimesheetAnalysisDto } from '../../api/models/timesheet-analysis-dto';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatHours, monthRange } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { MonthSwitcher } from '../../shared/month-switcher';

type RowState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: TimesheetAnalysisDto };

/** Maximum number of analysis requests in flight at once. */
const CONCURRENCY = 4;

@Component({
  selector: 'app-team-overview',
  imports: [MatButtonModule, MatProgressBarModule, MatTableModule, MonthSwitcher],
  templateUrl: './team-overview.html',
  styleUrl: './team-overview.scss',
})
export class TeamOverview {
  private readonly api = inject(TimesheetService);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly columns = ['name', 'hours', 'overtime', 'badges'];
  protected readonly formatHours = formatHours;
  protected readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  protected readonly rows = signal<Record<string, RowState>>({});

  private queue: Subject<string> | null = null;
  private subscription: Subscription | null = null;

  constructor() {
    this.directory.load().then(
      () => this.start(),
      (e: unknown) => this.notifier.error(problemOf(e)),
    );
  }

  protected setMonth(month: Date): void {
    this.month.set(month);
    this.start();
  }

  protected stateOf(id: string): RowState {
    return this.rows()[id] ?? { status: 'loading' };
  }

  protected retry(event: Event, id: string): void {
    event.stopPropagation();
    this.rows.update((rows) => ({ ...rows, [id]: { status: 'loading' } }));
    this.queue?.next(id);
  }

  protected open(id: string): void {
    const m = this.month();
    const miesiac = `${m.getFullYear()}-${String(m.getMonth() + 1).padStart(2, '0')}`;
    void this.router.navigate(['/czas-pracy'], { queryParams: { pracownik: id, miesiac } });
  }

  /** Fetches analysis for everyone in the current month, at most CONCURRENCY requests at a time. */
  private start(): void {
    this.subscription?.unsubscribe();
    const range = monthRange(this.month());
    const queue = new Subject<string>();
    this.queue = queue;
    const ids = this.directory.employees().map((e) => e.id);
    this.rows.set(Object.fromEntries(ids.map((id) => [id, { status: 'loading' } as RowState])));
    this.subscription = queue
      .pipe(
        mergeMap(
          (id) =>
            this.api.timesheetAnalyze({ employeeId: id, ...range }).pipe(
              map((data): [string, RowState] => [id, { status: 'ready', data }]),
              catchError((e: unknown) =>
                of<[string, RowState]>([id, { status: 'error', message: messageOf(problemOf(e)) }]),
              ),
            ),
          CONCURRENCY,
        ),
      )
      .subscribe(([id, state]) => this.rows.update((rows) => ({ ...rows, [id]: state })));
    ids.forEach((id) => queue.next(id));
  }
}
