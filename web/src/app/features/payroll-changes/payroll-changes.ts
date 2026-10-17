import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Observable, firstValueFrom } from 'rxjs';
import { PayrollChangeDto } from '../../api/models/payroll-change-dto';
import { PayrollChangesService } from '../../api/services/payroll-changes.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { formatMoney } from '../../core/format';
import { payrollChangeStatusLabel, payrollComponentLabel } from '../../core/labels';
import { Notifier } from '../../core/notifier';
import { Roles } from '../../core/roles';
import { SessionService } from '../../core/session';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { EmployeePicker } from '../../shared/employee-picker';
import { NewChangeDialog } from './new-change-dialog';
import { RejectDialog, RejectDialogData, RejectDialogResult } from './reject-dialog';

/** The change was decided, removed or is no longer permitted meanwhile, so the list is out of date. */
const isStale = (status: number | undefined): boolean => status === 404 || status === 403 || status === 409;

const STATUSES = ['Draft', 'Pending', 'Approved', 'Rejected'] as const;

@Component({
  selector: 'app-payroll-changes',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule, MatTableModule, EmployeePicker],
  templateUrl: './payroll-changes.html',
  styleUrl: './payroll-changes.scss',
})
export class PayrollChanges {
  private readonly api = inject(PayrollChangesService);
  private readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly columns = ['employee', 'component', 'value', 'from', 'status', 'author', 'decider', 'reason', 'actions'];
  protected readonly statuses = STATUSES;
  protected readonly statusLabel = payrollChangeStatusLabel;
  protected readonly componentLabel = payrollComponentLabel;
  protected readonly formatMoney = formatMoney;
  protected readonly formatDate = formatDate;
  protected readonly nameOf = (id: string): string => this.directory.nameOf(id);
  protected readonly userLabel = (id: string | null): string => this.directory.userLabel(id);

  protected readonly isHr = this.session.hasRole(Roles.HR);
  private readonly isApprover = this.session.hasRole(Roles.HRApprover);
  private readonly ownUserId = this.session.user()?.userId ?? null;

  protected readonly changes = signal<PayrollChangeDto[]>([]);
  protected readonly status = signal<string | null>(null);
  protected readonly employeeId = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  /** Id of the change with an action in progress; its buttons stay disabled. */
  protected readonly busyId = signal<string | null>(null);

  protected readonly rows = computed(() => {
    const status = this.status();
    return this.changes()
      .filter((c) => !status || c.status === status)
      .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc));
  });

  private requestSeq = 0;

  constructor() {
    this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    void this.refresh();
  }

  protected retry(): void {
    void this.refresh();
  }

  protected selectEmployee(id: string | null): void {
    this.employeeId.set(id);
    void this.refresh();
  }

  protected newChange(): void {
    this.dialog
      .open<NewChangeDialog, void, PayrollChangeDto>(NewChangeDialog)
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((created) => {
        if (created) void this.refresh();
      });
  }

  protected canSubmit(c: PayrollChangeDto): boolean {
    return this.isHr && c.status === 'Draft' && c.createdByUserId === this.ownUserId;
  }

  protected canDecide(c: PayrollChangeDto): boolean {
    return this.isApprover && c.status === 'Pending' && c.createdByUserId !== this.ownUserId;
  }

  protected ownPending(c: PayrollChangeDto): boolean {
    return this.isApprover && c.status === 'Pending' && c.createdByUserId === this.ownUserId;
  }

  protected submit(c: PayrollChangeDto): void {
    this.confirmAndRun(c, {
      title: 'Wysłanie do zatwierdzenia',
      message: 'Czy wysłać zmianę płacową do zatwierdzenia? Po wysłaniu nie można jej edytować.',
      confirmLabel: 'Wyślij do zatwierdzenia',
    }, () => this.api.payrollChangesSubmit({ id: c.id }), 'Zmiana wysłana do zatwierdzenia.');
  }

  protected approve(c: PayrollChangeDto): void {
    this.confirmAndRun(c, {
      title: 'Zatwierdzenie zmiany płacowej',
      message: 'Czy zatwierdzić zmianę płacową?',
      confirmLabel: 'Zatwierdź',
    }, () => this.api.payrollChangesApprove({ id: c.id }), 'Zmiana zatwierdzona.');
  }

  protected reject(change: PayrollChangeDto): void {
    if (this.busyId()) return;
    this.busyId.set(change.id);
    this.dialog
      .open<RejectDialog, RejectDialogData, RejectDialogResult>(RejectDialog, { data: { change } })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        if (!result) {
          this.busyId.set(null);
          return;
        }
        if ('failure' in result) {
          this.notifier.error(result.failure);
          if (isStale(result.failure.status)) void this.refresh();
        } else {
          this.notifier.info('Zmiana odrzucona.');
          void this.refresh();
        }
        this.busyId.set(null);
      });
  }

  private confirmAndRun(
    c: PayrollChangeDto,
    data: ConfirmDialogData,
    call: () => Observable<PayrollChangeDto>,
    done: string,
  ): void {
    if (this.busyId()) return;
    this.busyId.set(c.id);
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed) => {
        if (confirmed) void this.run(call, done);
        else this.busyId.set(null);
      });
  }

  private async run(call: () => Observable<PayrollChangeDto>, done: string): Promise<void> {
    try {
      await firstValueFrom(call());
      this.notifier.info(done);
      await this.refresh();
    } catch (e) {
      const problem = problemOf(e);
      this.notifier.error(problem);
      if (isStale(problem.status)) await this.refresh();
    } finally {
      this.busyId.set(null);
    }
  }

  private async refresh(): Promise<void> {
    const seq = ++this.requestSeq;
    const employeeId = this.employeeId();
    this.loading.set(true);
    try {
      const list = await firstValueFrom(this.api.payrollChangesList(employeeId ? { employeeId } : undefined));
      if (seq !== this.requestSeq) return;
      this.changes.set(list);
      this.error.set(null);
    } catch (e) {
      if (seq !== this.requestSeq) return;
      this.error.set(messageOf(problemOf(e)));
    } finally {
      if (seq === this.requestSeq) this.loading.set(false);
    }
  }
}
