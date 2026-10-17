import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { firstValueFrom } from 'rxjs';
import { PayrollChangeDto } from '../../api/models/payroll-change-dto';
import { PayrollChangesService } from '../../api/services/payroll-changes.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { toDateOnly } from '../../core/date-format';
import { EmployeeDirectory } from '../../core/employee-directory';
import { payrollComponentLabel } from '../../core/labels';
import { Notifier } from '../../core/notifier';

export const MAX_VALUE = 1_000_000;
const MONEY = /^\d+(?:[.,]\d{1,2})?$/;
const MONEY_MESSAGE = 'Podaj kwotę od 0 do 1 000 000 zł z co najwyżej dwoma miejscami po przecinku.';

/** Parses `4500,50` / `4500.5`; null when the text is not a finite amount in 0..MAX_VALUE with <= 2 decimals. */
export function parseMoney(text: string): number | null {
  const trimmed = text.trim();
  if (!MONEY.test(trimmed)) return null;
  const value = Number(trimmed.replace(',', '.'));
  return Number.isFinite(value) && value >= 0 && value <= MAX_VALUE ? value : null;
}

function moneyValidator(control: AbstractControl<string>): ValidationErrors | null {
  return parseMoney(control.value) === null ? { money: true } : null;
}

function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() ? null : { blank: true };
}

/** Dialog creating a payroll change draft; closes with the created `PayrollChangeDto`. */
@Component({
  selector: 'app-new-change-dialog',
  imports: [
    ReactiveFormsModule, MatButtonModule, MatDatepickerModule, MatDialogModule,
    MatFormFieldModule, MatInputModule, MatSelectModule,
  ],
  templateUrl: './new-change-dialog.html',
  styleUrl: '../leave/new-leave-dialog.scss',
})
export class NewChangeDialog {
  private readonly api = inject(PayrollChangesService);
  private readonly notifier = inject(Notifier);
  private readonly dialogRef = inject<MatDialogRef<NewChangeDialog, PayrollChangeDto>>(MatDialogRef);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly components = ['BaseSalary', 'FixedAllowance'] as const;
  protected readonly componentLabel = payrollComponentLabel;
  protected readonly moneyMessage = MONEY_MESSAGE;

  readonly form = new FormGroup({
    employeeId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    component: new FormControl('BaseSalary', { nonNullable: true, validators: [Validators.required] }),
    value: new FormControl('', { nonNullable: true, validators: [moneyValidator] }),
    date: new FormControl<Date | null>(null, { validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [notBlank] }),
  });

  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async save(): Promise<void> {
    if (this.sending() || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { employeeId, component, value, date, reason } = this.form.getRawValue();
    const amount = parseMoney(value);
    if (amount === null || !date) return;
    this.sending.set(true);
    this.dialogRef.disableClose = true;
    this.error.set(null);
    try {
      const created = await firstValueFrom(
        this.api.payrollChangesCreate({
          body: { employeeId, component, value: amount, effectiveFrom: toDateOnly(date), reason: reason.trim() },
        }),
      );
      this.notifier.info('Zmiana zapisana jako szkic.');
      this.dialogRef.close(created);
    } catch (e) {
      this.error.set(messageOf(problemOf(e)));
      this.sending.set(false);
      this.dialogRef.disableClose = false;
    }
  }
}
