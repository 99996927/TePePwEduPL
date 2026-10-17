import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { firstValueFrom } from 'rxjs';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate, formatHours, formatTime, localDay, monthRange, workedHours } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { APPROVER_ROLES, Roles } from '../../core/roles';
import { SessionService } from '../../core/session';
import { EmployeePicker } from '../../shared/employee-picker';
import { MonthSwitcher } from '../../shared/month-switcher';
import { AnalysisPanel } from './analysis-panel';
import { EntryDialog, EntryDialogData } from './entry-dialog';

/** Month from a `yyyy-MM` query value; invalid or future values fall back to the current month. */
function initialMonth(value: string | null): Date {
  const now = new Date();
  const current = new Date(now.getFullYear(), now.getMonth(), 1);
  const match = value ? /^(\d{4})-(0[1-9]|1[0-2])$/.exec(value) : null;
  if (!match) return current;
  const requested = new Date(Number(match[1]), Number(match[2]) - 1, 1);
  return requested > current ? current : requested;
}

@Component({
  selector: 'app-timesheet',
  imports: [MatButtonModule, MatTableModule, MonthSwitcher, EmployeePicker, AnalysisPanel],
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

  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly month = signal(initialMonth(this.query.get('miesiac')));
  protected readonly selectedId = signal<string | null>(
    this.isApprover ? (this.query.get('pracownik') ?? this.ownId) : this.ownId,
  );
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
    return workedHours(e.start, e.end, e.breakMinutes);
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
