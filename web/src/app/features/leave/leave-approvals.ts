import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { firstValueFrom } from 'rxjs';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { LeaveService } from '../../api/services/leave.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate, formatInstantDate } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { SessionService } from '../../core/session';
import { DecisionDialog, DecisionDialogData, DecisionDialogResult } from './decision-dialog';

@Component({
  selector: 'app-leave-approvals',
  imports: [MatButtonModule, MatTableModule],
  templateUrl: './leave-approvals.html',
  styleUrl: './leave-approvals.scss',
})
export class LeaveApprovals {
  private readonly api = inject(LeaveService);
  private readonly directory = inject(EmployeeDirectory);
  private readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);

  protected readonly columns = ['employee', 'team', 'from', 'to', 'days', 'created', 'actions'];
  protected readonly formatDate = formatDate;
  protected readonly formatInstantDate = formatInstantDate;
  protected readonly nameOf = (id: string): string => this.directory.nameOf(id);

  protected readonly requests = signal<LeaveRequestDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly rows = computed(() => {
    const me = this.session.user()?.employeeId;
    return this.requests()
      .filter((r) => r.status === 'Pending' && r.employeeId !== me)
      .sort((a, b) => a.from.localeCompare(b.from) || a.createdAtUtc.localeCompare(b.createdAtUtc));
  });

  constructor() {
    void this.refresh();
  }

  protected teamOf(id: string): string {
    return this.directory.employees().find((e) => e.id === id)?.teamName ?? '—';
  }

  protected decide(request: LeaveRequestDto, approve: boolean): void {
    this.dialog
      .open<DecisionDialog, DecisionDialogData, DecisionDialogResult>(DecisionDialog, { data: { request, approve } })
      .afterClosed()
      .subscribe((result) => {
        if (!result) return;
        if ('failure' in result) {
          this.notifier.error(result.failure);
          return;
        }
        this.requests.update((list) => list.filter((r) => r.id !== request.id));
        this.notifier.info(approve ? 'Wniosek zatwierdzony.' : 'Wniosek odrzucony.');
      });
  }

  private async refresh(): Promise<void> {
    try {
      const [, requests] = await Promise.all([this.directory.load(), firstValueFrom(this.api.leaveList())]);
      this.requests.set(requests);
    } catch (e) {
      this.error.set(messageOf(problemOf(e)));
    } finally {
      this.loading.set(false);
    }
  }
}
