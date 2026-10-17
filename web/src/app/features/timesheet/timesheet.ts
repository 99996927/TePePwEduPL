import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { firstValueFrom } from 'rxjs';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate, formatHours, formatTime, monthRange } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { APPROVER_ROLES, Roles } from '../../core/roles';
import { SessionService } from '../../core/session';
import { EmployeePicker } from '../../shared/employee-picker';
import { MonthSwitcher } from '../../shared/month-switcher';
import { EntryDialog, EntryDialogData } from './entry-dialog';

function localDay(iso: string): Date {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

@Component({
  selector: 'app-timesheet',
  imports: [MatButtonModule, MatTableModule, MonthSwitcher, EmployeePicker],
  templateUrl: './timesheet.html',
  styleUrl: './timesheet.scss',
})
export class Timesheet {
  private readonly api = inject(TimesheetService);
  private readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly columns = ['day', 'start', 'end', 'break', 'worked', 'actions'];
  protected readonly formatTime = formatTime;
  protected readonly formatHours = formatHours;
  protected readonly isApprover = this.session.hasRole(...APPROVER_ROLES);
  private readonly ownId = this.session.user()?.employeeId ?? null;
  private readonly isHr = this.session.hasRole(Roles.HR, Roles.HRApprover);

  protected readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  protected readonly selectedId = signal<string | null>(this.ownId);
  protected readonly entries = signal<WorkEntryDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly isOwn = computed(() => this.selectedId() === this.ownId);
  protected readonly rows = computed(() => {
    const id = this.selectedId();
    return this.entries()
      .filter((e) => e.employeeId === id)
      .sort((a, b) => a.start.localeCompare(b.start));
  });

  private requestSeq = 0;

  constructor() {
    if (this.isApprover) {
      this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    }
    void this.refresh();
  }

  protected setMonth(month: Date): void {
    this.month.set(month);
    void this.refresh();
  }

  protected dayLabel(e: WorkEntryDto): string {
    const weekday = localDay(e.start).toLocaleDateString('pl-PL', { weekday: 'short' });
    return `${weekday} ${formatDate(e.start)}`;
  }

  protected endsNextDay(e: WorkEntryDto): boolean {
    return e.end.slice(0, 10) > e.start.slice(0, 10);
  }

  protected workedHours(e: WorkEntryDto): number {
    const ms = new Date(e.end).getTime() - new Date(e.start).getTime();
    return ms / 3_600_000 - e.breakMinutes / 60;
  }

  protected canCorrect(e: WorkEntryDto): boolean {
    return e.employeeId === this.ownId || this.isHr;
  }

  protected openAdd(): void {
    this.open({ mode: 'add' });
  }

  protected openCorrect(entry: WorkEntryDto): void {
    this.open({ mode: 'correct', entry });
  }

  protected refresh(): Promise<void> {
    return this.load();
  }

  private open(data: EntryDialogData): void {
    this.dialog
      .open<EntryDialog, EntryDialogData, WorkEntryDto>(EntryDialog, { data })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) void this.load();
      });
  }

  private async load(): Promise<void> {
    const seq = ++this.requestSeq;
    this.loading.set(true);
    this.error.set(null);
    try {
      const entries = await firstValueFrom(this.api.timesheetList(monthRange(this.month())));
      if (seq !== this.requestSeq) return;
      this.entries.set(entries);
    } catch (e) {
      if (seq !== this.requestSeq) return;
      this.entries.set([]);
      this.error.set(messageOf(problemOf(e)));
    } finally {
      if (seq === this.requestSeq) this.loading.set(false);
    }
  }
}
