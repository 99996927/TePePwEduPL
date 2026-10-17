import { Component, input, model } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { EmployeeDto } from '../api/models/employee-dto';

/** Select of employees labelled `Imię Nazwisko (zespół)`; two-way `selectedId`. */
@Component({
  selector: 'app-employee-picker',
  imports: [MatFormFieldModule, MatSelectModule],
  template: `
    <mat-form-field>
      <mat-label>Pracownik</mat-label>
      <mat-select [value]="selectedId()" (selectionChange)="selectedId.set($event.value)">
        @for (e of employees(); track e.id) {
          <mat-option [value]="e.id">{{ e.firstName }} {{ e.lastName }} ({{ e.teamName }})</mat-option>
        }
      </mat-select>
    </mat-form-field>
  `,
})
export class EmployeePicker {
  readonly employees = input<EmployeeDto[]>([]);
  readonly selectedId = model<string | null>(null);
}
