import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { firstValueFrom } from 'rxjs';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { LeaveService } from '../../api/services/leave.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { toDateOnly } from '../../core/date-format';
import { Notifier } from '../../core/notifier';

/** Dialog for submitting a leave request; closes with the created `LeaveRequestDto`. */
@Component({
  selector: 'app-new-leave-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule, MatFormFieldModule],
  templateUrl: './new-leave-dialog.html',
  styleUrl: './new-leave-dialog.scss',
})
export class NewLeaveDialog {
  private readonly leave = inject(LeaveService);
  private readonly notifier = inject(Notifier);
  private readonly dialogRef = inject<MatDialogRef<NewLeaveDialog, LeaveRequestDto>>(MatDialogRef);

  readonly range = new FormGroup({
    start: new FormControl<Date | null>(null),
    end: new FormControl<Date | null>(null),
  });

  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async save(): Promise<void> {
    const { start, end } = this.range.value;
    if (this.sending() || !start || !end) return;
    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.error.set(null);
    try {
      const created = await firstValueFrom(
        this.leave.leaveSubmit({ body: { from: toDateOnly(start), to: toDateOnly(end) } }),
      );
      this.notifier.info(`Wniosek złożony: ${created.days} dni roboczych.`);
      this.dialogRef.close(created);
    } catch (e) {
      this.error.set(messageOf(problemOf(e)));
      this.sending.set(false);
      this.dialogRef.disableClose = false;
    }
  }
}
