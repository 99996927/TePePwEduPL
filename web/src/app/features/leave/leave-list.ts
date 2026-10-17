import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { firstValueFrom } from 'rxjs';
import { LeaveBalanceDto } from '../../api/models/leave-balance-dto';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { LeaveService } from '../../api/services/leave.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { ConfirmDialog, ConfirmDialogData } from '../../shared/confirm-dialog';
import { NewLeaveDialog } from './new-leave-dialog';

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Oczekuje',
  Approved: 'Zatwierdzony',
  Rejected: 'Odrzucony',
  Cancelled: 'Anulowany',
};

@Component({
  selector: 'app-leave-list',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule, MatTableModule],
  templateUrl: './leave-list.html',
  styleUrl: './leave-list.scss',
})
export class LeaveList {
  private readonly api = inject(LeaveService);
  private readonly directory = inject(EmployeeDirectory);
  private readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);

  protected readonly columns = ['employee', 'from', 'to', 'days', 'status', 'reason', 'actions'];
  protected readonly statuses = Object.entries(STATUS_LABELS).map(([value, label]) => ({ value, label }));
  protected readonly formatDate = formatDate;
  protected readonly nameOf = (id: string): string => this.directory.nameOf(id);

  protected readonly balance = signal<LeaveBalanceDto | null>(null);
  protected readonly requests = signal<LeaveRequestDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly filter = signal<string>('');

  protected readonly rows = computed(() => {
    const status = this.filter();
    return this.requests()
      .filter((r) => !status || r.status === status)
      .sort((a, b) => b.from.localeCompare(a.from) || b.createdAtUtc.localeCompare(a.createdAtUtc));
  });

  constructor() {
    void this.refresh();
  }

  protected statusLabel(status: string): string {
    return STATUS_LABELS[status] ?? status;
  }

  protected canCancel(r: LeaveRequestDto): boolean {
    return r.status === 'Pending' && r.employeeId === this.session.user()?.employeeId;
  }

  protected openNew(): void {
    this.dialog
      .open<NewLeaveDialog, undefined, LeaveRequestDto>(NewLeaveDialog)
      .afterClosed()
      .subscribe((created) => {
        if (created) void this.refresh();
      });
  }

  protected cancel(r: LeaveRequestDto): void {
    const data: ConfirmDialogData = {
      title: 'Anulowanie wniosku',
      message: `Czy anulować wniosek na okres ${formatDate(r.from)} – ${formatDate(r.to)}?`,
      confirmLabel: 'Anuluj wniosek',
    };
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed) => {
        if (confirmed) void this.doCancel(r.id);
      });
  }

  private async doCancel(id: string): Promise<void> {
    try {
      await firstValueFrom(this.api.leaveCancel({ id }));
      this.notifier.info('Wniosek anulowany.');
    } catch (e) {
      this.notifier.error(problemOf(e));
    }
    await this.refresh();
  }

  private async refresh(): Promise<void> {
    this.error.set(null);
    try {
      const [, balance, requests] = await Promise.all([
        this.directory.load(),
        firstValueFrom(this.api.leaveBalance()),
        firstValueFrom(this.api.leaveList()),
      ]);
      this.balance.set(balance);
      this.requests.set(requests);
    } catch (e) {
      this.error.set(messageOf(problemOf(e)));
    } finally {
      this.loading.set(false);
    }
  }
}
