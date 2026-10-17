import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Subject, Subscription, catchError, map, mergeMap, of } from 'rxjs';
import { EmployeeDto } from '../../api/models/employee-dto';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { PayrollService } from '../../api/services/payroll.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatHours } from '../../core/date-format';
import { formatMoney } from '../../core/format';

type RowState =
  | { status: 'loading' }
  | { status: 'notFound' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: PayrollResultDto };

/** Maximum number of payroll requests in flight at once. */
const CONCURRENCY = 4;

@Component({
  selector: 'app-payroll-summary',
  imports: [MatButtonModule, MatProgressBarModule, MatTableModule],
  templateUrl: './payroll-summary.html',
  styleUrl: './payroll-summary.scss',
})
export class PayrollSummary {
  private readonly api = inject(PayrollService);

  readonly month = input.required<Date>();
  readonly employees = input.required<readonly EmployeeDto[]>();
  readonly runResult = input<PayrollResultDto[] | null>(null);
  readonly selected = output<string>();

  protected readonly columns = ['name', 'team', 'gross', 'overtime'];
  protected readonly formatMoney = formatMoney;
  protected readonly formatHours = formatHours;
  protected readonly rows = signal<Record<string, RowState>>({});

  private queue: Subject<string> | null = null;
  private subscription: Subscription | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.close());
    effect(() => {
      const month = this.month();
      const employees = this.employees();
      untracked(() => this.start(month, employees));
    });
    effect(() => {
      const results = this.runResult();
      if (results) untracked(() => this.apply(results));
    });
  }

  protected stateOf(id: string): RowState {
    return this.rows()[id] ?? { status: 'loading' };
  }

  protected overtimeOf(r: PayrollResultDto): number {
    return r.overtime50Hours + r.overtime100Hours;
  }

  protected retry(event: Event, id: string): void {
    event.stopPropagation();
    this.rows.update((rows) => ({ ...rows, [id]: { status: 'loading' } }));
    this.queue?.next(id);
  }

  private close(): void {
    this.subscription?.unsubscribe();
    this.queue?.complete();
    this.subscription = null;
    this.queue = null;
  }

  /** (Re)creates the request queue; pending requests of the previous one are cancelled. */
  private open(month: Date): void {
    this.close();
    const year = month.getFullYear();
    const monthNo = month.getMonth() + 1;
    const queue = new Subject<string>();
    this.queue = queue;
    this.subscription = queue
      .pipe(
        mergeMap(
          (id) =>
            this.api.payrollGet({ year, month: monthNo, employeeId: id }).pipe(
              map((data): [string, RowState] => [id, { status: 'ready', data }]),
              catchError((e: unknown) =>
                of<[string, RowState]>([
                  id,
                  e instanceof HttpErrorResponse && e.status === 404
                    ? { status: 'notFound' }
                    : { status: 'error', message: messageOf(problemOf(e)) },
                ]),
              ),
            ),
          CONCURRENCY,
        ),
      )
      .subscribe(([id, state]) => this.rows.update((rows) => ({ ...rows, [id]: state })));
  }

  private start(month: Date, employees: readonly EmployeeDto[]): void {
    this.open(month);
    this.rows.set(Object.fromEntries(employees.map((e) => [e.id, { status: 'loading' } as RowState])));
    employees.forEach((e) => this.queue?.next(e.id));
  }

  /** Replaces rows with the run response; people missing from it have no result. */
  private apply(results: PayrollResultDto[]): void {
    this.open(this.month());
    const byId = new Map(results.map((r) => [r.employeeId, r]));
    this.rows.set(
      Object.fromEntries(
        this.employees().map((e): [string, RowState] => {
          const data = byId.get(e.id);
          return [e.id, data ? { status: 'ready', data } : { status: 'notFound' }];
        }),
      ),
    );
  }
}
