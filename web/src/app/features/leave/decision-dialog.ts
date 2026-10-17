import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { LeaveRequestDto } from '../../api/models/leave-request-dto';
import { LeaveService } from '../../api/services/leave.service';
import { ApiProblem, messageOf, problemOf } from '../../core/api-problem';
import { formatDate } from '../../core/date-format';

export interface DecisionDialogData {
  request: LeaveRequestDto;
  approve: boolean;
}

/** Outcome of the dialog: the decided request, or the problem when the API answered 403/404. */
export type DecisionDialogResult = { decided: LeaveRequestDto } | { failure: ApiProblem };

/**
 * Sends the decision itself so a 422 (e.g. insufficient balance) can be shown
 * inside the dialog, which then stays open.
 */
@Component({
  selector: 'app-decision-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './decision-dialog.html',
  styleUrl: './new-leave-dialog.scss',
})
export class DecisionDialog {
  private readonly leave = inject(LeaveService);
  private readonly dialogRef = inject<MatDialogRef<DecisionDialog, DecisionDialogResult>>(MatDialogRef);
  protected readonly data = inject<DecisionDialogData>(MAT_DIALOG_DATA);

  protected readonly formatDate = formatDate;
  protected readonly reason = new FormControl('', { nonNullable: true });
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async save(): Promise<void> {
    if (this.sending()) return;
    const reason = this.reason.value.trim();
    if (!this.data.approve && !reason) {
      this.error.set('Podaj uzasadnienie odrzucenia.');
      return;
    }
    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.error.set(null);
    try {
      const decided = await firstValueFrom(
        this.leave.leaveDecide({
          id: this.data.request.id,
          body: { approve: this.data.approve, reason: reason || null },
        }),
      );
      this.dialogRef.close({ decided });
    } catch (e) {
      const problem = problemOf(e);
      if (problem.status === 403 || problem.status === 404) {
        this.dialogRef.close({ failure: problem });
        return;
      }
      this.error.set(messageOf(problem));
      this.sending.set(false);
      this.dialogRef.disableClose = false;
    }
  }
}
