import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { PayrollChangeDto } from '../../api/models/payroll-change-dto';
import { PayrollChangesService } from '../../api/services/payroll-changes.service';
import { ApiProblem, messageOf, problemOf } from '../../core/api-problem';
import { formatDate } from '../../core/date-format';
import { payrollComponentLabel } from '../../core/labels';
import { formatMoney } from '../../core/format';

export interface RejectDialogData {
  change: PayrollChangeDto;
}

/** Outcome: the rejected change, or the problem when the API answered 403/404/409. */
export type RejectDialogResult = { decided: PayrollChangeDto } | { failure: ApiProblem };

/** Sends the rejection itself so a 422 can be shown inside the dialog, which then stays open. */
@Component({
  selector: 'app-reject-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './reject-dialog.html',
  styleUrl: '../leave/new-leave-dialog.scss',
})
export class RejectDialog {
  private readonly api = inject(PayrollChangesService);
  private readonly dialogRef = inject<MatDialogRef<RejectDialog, RejectDialogResult>>(MatDialogRef);
  protected readonly data = inject<RejectDialogData>(MAT_DIALOG_DATA);

  protected readonly formatDate = formatDate;
  protected readonly formatMoney = formatMoney;
  protected readonly componentLabel = payrollComponentLabel;
  protected readonly reason = new FormControl('', { nonNullable: true });
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async save(): Promise<void> {
    if (this.sending()) return;
    const reason = this.reason.value.trim();
    if (!reason) {
      this.error.set('Podaj uzasadnienie odrzucenia.');
      return;
    }
    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.error.set(null);
    try {
      const decided = await firstValueFrom(
        this.api.payrollChangesReject({ id: this.data.change.id, body: { reason } }),
      );
      this.dialogRef.close({ decided });
    } catch (e) {
      const problem = problemOf(e);
      if (problem.status === 403 || problem.status === 404 || problem.status === 409) {
        this.dialogRef.close({ failure: problem });
        return;
      }
      this.error.set(messageOf(problem));
      this.sending.set(false);
      this.dialogRef.disableClose = false;
    }
  }
}
