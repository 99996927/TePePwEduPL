import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { PayrollService } from '../../api/services/payroll.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { HR_ROLES } from '../../core/roles';
import { SessionService } from '../../core/session';
import { EmployeePicker } from '../../shared/employee-picker';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { MonthSwitcher } from '../../shared/month-switcher';
import { PayrollCard } from './payroll-card';
import { PayrollSummary } from './payroll-summary';

@Component({
  selector: 'app-payroll',
  imports: [MatButtonModule, MonthSwitcher, EmployeePicker, PayrollCard, PayrollSummary],
  templateUrl: './payroll.html',
  styleUrl: './payroll.scss',
})
export class Payroll {
  private readonly api = inject(PayrollService);
  private readonly session = inject(SessionService);
  private readonly notifier = inject(Notifier);
  private readonly dialog = inject(MatDialog);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly isHr = this.session.hasRole(...HR_ROLES);
  private readonly ownId = this.session.user()?.employeeId ?? null;
  private readonly ownName = this.session.user()?.displayName ?? '—';

  protected readonly month = signal(this.currentMonth());
  protected readonly selectedId = signal<string | null>(this.ownId);
  protected readonly result = signal<PayrollResultDto | null>(null);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly runResult = signal<PayrollResultDto[] | null>(null);
  protected readonly runError = signal<string | null>(null);
  /** True from opening the confirmation until the run request settles. */
  protected readonly running = signal(false);

  protected readonly name = computed(() => {
    const id = this.selectedId();
    return this.isHr && id ? this.directory.nameOf(id) : this.ownName;
  });

  private requestSeq = 0;

  constructor() {
    if (this.isHr) {
      this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    }
    void this.load();
  }

  protected setMonth(month: Date): void {
    this.month.set(month);
    this.runResult.set(null);
    this.runError.set(null);
    void this.load();
  }

  protected select(id: string | null): void {
    this.selectedId.set(id);
    void this.load();
  }

  protected retry(): void {
    void this.load();
  }

  protected run(): void {
    if (this.running()) return;
    this.running.set(true);
    this.runError.set(null);
    const month = this.month();
    const now = new Date();
    const ongoing = month.getFullYear() * 12 + month.getMonth() >= now.getFullYear() * 12 + now.getMonth();
    const data: ConfirmDialogData = {
      title: 'Uruchomienie naliczenia',
      message:
        `Czy uruchomić naliczenie za ${String(month.getMonth() + 1).padStart(2, '0')}.${month.getFullYear()}?` +
        (ongoing ? ' Miesiąc jeszcze trwa. Naliczenie będzie wstępne i można je powtórzyć.' : ''),
      confirmLabel: 'Uruchom naliczenie',
    };
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) void this.doRun(month);
        else this.running.set(false);
      });
  }

  private async doRun(month: Date): Promise<void> {
    try {
      const results = await firstValueFrom(
        this.api.payrollRun({ body: { year: month.getFullYear(), month: month.getMonth() + 1 } }),
      );
      if (month === this.month()) {
        this.runResult.set(results);
        void this.load();
      }
      this.notifier.info(`Naliczono ${results.length} pracowników.`);
    } catch (e) {
      this.runError.set(messageOf(problemOf(e)));
    } finally {
      this.running.set(false);
    }
  }

  private currentMonth(): Date {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  }

  private async load(): Promise<void> {
    const seq = ++this.requestSeq;
    const month = this.month();
    const id = this.selectedId();
    this.error.set(null);
    this.notFound.set(false);
    this.result.set(null);
    try {
      const result = await firstValueFrom(
        this.api.payrollGet({
          year: month.getFullYear(),
          month: month.getMonth() + 1,
          ...(this.isHr && id && id !== this.ownId ? { employeeId: id } : {}),
        }),
      );
      if (seq !== this.requestSeq) return;
      this.result.set(result);
    } catch (e) {
      if (seq !== this.requestSeq) return;
      if (e instanceof HttpErrorResponse && e.status === 404) {
        this.notFound.set(true);
      } else {
        this.error.set(messageOf(problemOf(e)));
      }
    }
  }
}
