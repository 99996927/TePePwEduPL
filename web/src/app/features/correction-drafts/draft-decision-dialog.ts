import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { CorrectionDraftDto } from '../../api/models/correction-draft-dto';
import { CorrectionDraftsService } from '../../api/services/correction-drafts.service';
import { ApiProblem, messageOf, problemOf } from '../../core/api-problem';
import { formatDate, formatTime } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';

export interface DraftDecisionDialogData {
  draft: CorrectionDraftDto;
  approve: boolean;
}

/** Outcome: the decided draft, or the problem when the API answered 403/404/409 (the dialog then closes). */
export type DraftDecisionDialogResult = { decided: CorrectionDraftDto } | { failure: ApiProblem };

/** Sends the decision itself so a 422 (e.g. overlapping entries) can be shown inside the dialog, which then stays open. */
@Component({
  selector: 'app-draft-decision-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './draft-decision-dialog.html',
  styleUrl: './draft-decision-dialog.scss',
})
export class DraftDecisionDialog {
  private readonly api = inject(CorrectionDraftsService);
  private readonly dialogRef = inject<MatDialogRef<DraftDecisionDialog, DraftDecisionDialogResult>>(MatDialogRef);
  private readonly directory = inject(EmployeeDirectory);
  protected readonly data = inject<DraftDecisionDialogData>(MAT_DIALOG_DATA);

  protected readonly formatDate = formatDate;
  protected readonly formatTime = formatTime;
  protected readonly employeeName = this.directory.nameOf(this.data.draft.employeeId);
  protected readonly note = new FormControl('', { nonNullable: true });
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async save(): Promise<void> {
    if (this.sending()) return;
    const note = this.note.value.trim();
    if (!this.data.approve && !note) {
      this.error.set('Podaj uzasadnienie odrzucenia.');
      return;
    }
    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.error.set(null);
    try {
      const decided = await firstValueFrom(
        this.api.correctionDraftsDecide({
          id: this.data.draft.id,
          body: { approve: this.data.approve, note: note || null },
        }),
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
