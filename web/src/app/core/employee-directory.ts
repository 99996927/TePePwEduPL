import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { EmployeeDto } from '../api/models/employee-dto';
import { EmployeesService } from '../api/services/employees.service';
import { SessionService } from './session';

@Injectable({ providedIn: 'root' })
export class EmployeeDirectory {
  private readonly api = inject(EmployeesService);
  private readonly session = inject(SessionService);
  private readonly state = signal<EmployeeDto[]>([]);
  private loading: Promise<void> | null = null;
  private loadedFor: string | null = null;

  readonly employees = this.state.asReadonly();

  /** Loads the list once per logged-in user; a different user (after logout/login) gets a fresh list. */
  load(): Promise<void> {
    const userId = this.session.user()?.userId ?? null;
    if (this.loading && this.loadedFor !== userId) {
      this.loading = null;
      this.state.set([]);
    }
    if (!this.loading) {
      this.loadedFor = userId;
      const request = firstValueFrom(this.api.employeesList()).then(
        (list) => {
          if (this.loading === request) this.state.set(list);
        },
        (error: unknown) => {
          if (this.loading === request) this.loading = null;
          throw error;
        },
      );
      this.loading = request;
    }
    return this.loading;
  }

  nameOf(id: string): string {
    const found = this.state().find((e) => e.id === id);
    return found ? `${found.firstName} ${found.lastName}` : '—';
  }
}
