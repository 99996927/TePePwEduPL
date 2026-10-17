import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { firstValueFrom } from 'rxjs';
import { CorrectionDraftDto } from '../../api/models/correction-draft-dto';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { CorrectionDraftsService } from '../../api/services/correction-drafts.service';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { formatDate, formatTime, localDay, monthRange } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { draftStatusLabel } from '../../core/labels';
import { Notifier } from '../../core/notifier';
import { HR_ROLES } from '../../core/roles';
import { SessionService } from '../../core/session';
import { DraftDecisionDialog, DraftDecisionDialogData, DraftDecisionDialogResult } from './draft-decision-dialog';

const FILTERS: { label: string; status: string | null }[] = [
  { label: 'Oczekujące', status: 'Pending' },
  { label: 'Wszystkie', status: null },
  { label: 'Zatwierdzone', status: 'Approved' },
  { label: 'Odrzucone', status: 'Rejected' },
];

@Component({
  selector: 'app-correction-drafts',
  imports: [MatButtonModule, MatFormFieldModule, MatSelectModule, MatTableModule],
  templateUrl: './correction-drafts.html',
  styleUrl: './correction-drafts.scss',
})
export class CorrectionDrafts {
  private readonly api = inject(CorrectionDraftsService);
  private readonly timesheet = inject(TimesheetService);
  private readonly session = inject(SessionService);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly columns = ['employee', 'date', 'current', 'proposed', 'reason', 'author', 'status', 'note', 'actions'];
  protected readonly filters = FILTERS;
  protected readonly statusLabel = draftStatusLabel;
  protected readonly formatDate = formatDate;
  protected readonly nameOf = (id: string): string => this.directory.nameOf(id);
  protected readonly userLabel = (id: string | null): string => this.directory.userLabel(id);

  protected readonly canDecide = this.session.hasRole(...HR_ROLES);

  protected readonly drafts = signal<CorrectionDraftDto[]>([]);
  protected readonly status = signal<string | null>('Pending');
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  /** Id of the draft with a decision dialog open; its buttons stay disabled. */
  protected readonly busyId = signal<string | null>(null);
  private readonly entries = signal<Map<string, WorkEntryDto>>(new Map());

  protected readonly rows = computed(() =>
    [...this.drafts()].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)),
  );

  private requestSeq = 0;

  constructor() {
    this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    void this.refresh();
  }

  protected retry(): void {
    void this.refresh();
  }

  protected selectStatus(status: string | null): void {
    this.status.set(status);
    void this.refresh();
  }

  protected currentHours(d: CorrectionDraftDto): string {
    const entry = this.entries().get(d.workEntryId);
    return entry ? `${formatTime(entry.start)}–${formatTime(entry.end)}` : '—';
  }

  protected proposedHours(d: CorrectionDraftDto): string {
    return `${formatTime(d.proposedStart)}–${formatTime(d.proposedEnd)}`;
  }

  protected nextDay(d: CorrectionDraftDto): boolean {
    return d.proposedEnd.slice(0, 10) > d.proposedStart.slice(0, 10);
  }

  protected decide(draft: CorrectionDraftDto, approve: boolean): void {
    if (this.busyId()) return;
    this.busyId.set(draft.id);
    this.dialog
      .open<DraftDecisionDialog, DraftDecisionDialogData, DraftDecisionDialogResult>(DraftDecisionDialog, {
        data: { draft, approve },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((result) => {
        this.busyId.set(null);
        if (!result) return;
        if ('failure' in result) {
          this.notifier.error(result.failure);
          if (result.failure.status === 409) void this.refresh();
        } else {
          this.notifier.info(approve ? 'Szkic zatwierdzony.' : 'Szkic odrzucony.');
          void this.refresh();
        }
      });
  }

  private async refresh(): Promise<void> {
    const seq = ++this.requestSeq;
    const status = this.status();
    this.loading.set(true);
    try {
      const list = await firstValueFrom(this.api.correctionDraftsList(status ? { status } : undefined));
      if (seq !== this.requestSeq) return;
      this.drafts.set(list);
      this.error.set(null);
      void this.loadEntries(list, seq);
    } catch (e) {
      if (seq !== this.requestSeq) return;
      this.error.set(messageOf(problemOf(e)));
    } finally {
      if (seq === this.requestSeq) this.loading.set(false);
    }
  }

  /** One timesheet request per distinct month of the proposed starts; entries are matched by work entry id. */
  private async loadEntries(list: CorrectionDraftDto[], seq: number): Promise<void> {
    const months = new Map<string, { from: string; to: string }>();
    for (const d of list) {
      const range = monthRange(localDay(d.proposedStart));
      months.set(range.from, range);
    }
    try {
      const lists = await Promise.all([...months.values()].map((range) => firstValueFrom(this.timesheet.timesheetList(range))));
      if (seq !== this.requestSeq) return;
      this.entries.set(new Map(lists.flat().map((w) => [w.id, w])));
    } catch (e) {
      if (seq !== this.requestSeq) return;
      this.entries.set(new Map());
      this.notifier.error(problemOf(e));
    }
  }
}
