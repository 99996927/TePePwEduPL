import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { WorkEntryDto } from '../../api/models/work-entry-dto';
import { TimesheetService } from '../../api/services/timesheet.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { toLocalDateTime } from '../../core/date-format';
import { Notifier } from '../../core/notifier';

export type EntryDialogData = { mode: 'add' } | { mode: 'correct'; entry: WorkEntryDto };

const TIME = /^\d{2}:\d{2}$/;

function localDay(iso: string): Date {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** Dialog for adding or correcting a work entry; closes with the saved `WorkEntryDto`. */
@Component({
  selector: 'app-entry-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './entry-dialog.html',
  styleUrl: './entry-dialog.scss',
})
export class EntryDialog {
  private readonly timesheet = inject(TimesheetService);
  private readonly notifier = inject(Notifier);
  private readonly dialogRef = inject<MatDialogRef<EntryDialog, WorkEntryDto>>(MatDialogRef);
  protected readonly data = inject<EntryDialogData>(MAT_DIALOG_DATA);

  protected readonly correcting = this.data.mode === 'correct';

  readonly form = new FormGroup({
    date: new FormControl<Date | null>(new Date(), Validators.required),
    start: new FormControl('', [Validators.required, Validators.pattern(TIME)]),
    end: new FormControl('', [Validators.required, Validators.pattern(TIME)]),
    breakMinutes: new FormControl<number | null>(0, [Validators.required, Validators.min(0)]),
    reason: new FormControl('', this.data.mode === 'correct' ? [Validators.required] : []),
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });

  /** The end time is not after the start time, so the entry finishes the next day. */
  protected readonly endsNextDay = computed(() => {
    const { start, end } = this.value();
    return !!start && !!end && TIME.test(start) && TIME.test(end) && end <= start;
  });

  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    if (this.data.mode === 'correct') {
      const { entry } = this.data;
      this.form.patchValue({
        date: localDay(entry.start),
        start: entry.start.slice(11, 16),
        end: entry.end.slice(11, 16),
        breakMinutes: entry.breakMinutes,
      });
    }
  }

  protected get canSave(): boolean {
    const reason = this.form.controls.reason.value ?? '';
    return this.form.valid && (!this.correcting || reason.trim().length > 0);
  }

  protected async save(): Promise<void> {
    if (this.sending() || !this.canSave) return;
    const { date, start, end, breakMinutes, reason } = this.form.getRawValue();
    if (!date || !start || !end || breakMinutes === null) return;
    const endDay = this.endsNextDay() ? new Date(date.getFullYear(), date.getMonth(), date.getDate() + 1) : date;
    const range = { start: toLocalDateTime(date, start), end: toLocalDateTime(endDay, end), breakMinutes };

    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.error.set(null);
    try {
      const saved = await firstValueFrom(
        this.data.mode === 'correct'
          ? this.timesheet.timesheetCorrect({ id: this.data.entry.id, body: { ...range, reason: (reason ?? '').trim() } })
          : this.timesheet.timesheetAdd({ body: range }),
      );
      this.notifier.info(this.correcting ? 'Wpis skorygowany.' : 'Wpis dodany.');
      this.dialogRef.close(saved);
    } catch (e) {
      this.error.set(messageOf(problemOf(e)));
      this.sending.set(false);
      this.dialogRef.disableClose = false;
    }
  }
}
