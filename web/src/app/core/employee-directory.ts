import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { EmployeeDto } from '../api/models/employee-dto';
import { EmployeesService } from '../api/services/employees.service';

@Injectable({ providedIn: 'root' })
export class EmployeeDirectory {
  private readonly api = inject(EmployeesService);
  private readonly state = signal<EmployeeDto[]>([]);
  private loading: Promise<void> | null = null;

  readonly employees = this.state.asReadonly();

  load(): Promise<void> {
    this.loading ??= firstValueFrom(this.api.employeesList()).then(
      (list) => this.state.set(list),
      (error: unknown) => {
        this.loading = null;
        throw error;
      },
    );
    return this.loading;
  }

  nameOf(id: string): string {
    const found = this.state().find((e) => e.id === id);
    return found ? `${found.firstName} ${found.lastName}` : '—';
  }
}
