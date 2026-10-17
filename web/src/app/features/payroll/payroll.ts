import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';
import { PayrollResultDto } from '../../api/models/payroll-result-dto';
import { PayrollService } from '../../api/services/payroll.service';
import { messageOf, problemOf } from '../../core/api-problem';
import { EmployeeDirectory } from '../../core/employee-directory';
import { Notifier } from '../../core/notifier';
import { HR_ROLES } from '../../core/roles';
import { SessionService } from '../../core/session';
import { EmployeePicker } from '../../shared/employee-picker';
import { MonthSwitcher } from '../../shared/month-switcher';
import { PayrollCard } from './payroll-card';

@Component({
  selector: 'app-payroll',
  imports: [MatButtonModule, MonthSwitcher, EmployeePicker, PayrollCard],
  templateUrl: './payroll.html',
  styleUrl: './payroll.scss',
})
export class Payroll {
  private readonly api = inject(PayrollService);
  private readonly session = inject(SessionService);
  private readonly notifier = inject(Notifier);
  protected readonly directory = inject(EmployeeDirectory);

  protected readonly isHr = this.session.hasRole(...HR_ROLES);
  private readonly ownId = this.session.user()?.employeeId ?? null;
  private readonly ownName = this.session.user()?.displayName ?? '—';

  protected readonly month = signal(this.currentMonth());
  protected readonly selectedId = signal<string | null>(this.ownId);
  protected readonly result = signal<PayrollResultDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly name = computed(() => {
    const id = this.selectedId();
    return this.isHr && id ? this.directory.nameOf(id) : this.ownName;
  });

  private requestSeq = 0;

  constructor() {
    if (this.isHr) {
      this.directory.load().catch((e: unknown) => this.notifier.error(problemOf(e)));
    }
    void this.load();
  }

  protected setMonth(month: Date): void {
    this.month.set(month);
    void this.load();
  }

  protected select(id: string | null): void {
    this.selectedId.set(id);
    void this.load();
  }

  protected retry(): void {
    void this.load();
  }

  private currentMonth(): Date {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  }

  private async load(): Promise<void> {
    const seq = ++this.requestSeq;
    const month = this.month();
    const id = this.selectedId();
    this.loading.set(true);
    this.error.set(null);
    this.notFound.set(false);
    this.result.set(null);
    try {
      const result = await firstValueFrom(
        this.api.payrollGet({
          year: month.getFullYear(),
          month: month.getMonth() + 1,
          ...(this.isHr && id && id !== this.ownId ? { employeeId: id } : {}),
        }),
      );
      if (seq !== this.requestSeq) return;
      this.result.set(result);
    } catch (e) {
      if (seq !== this.requestSeq) return;
      if (e instanceof HttpErrorResponse && e.status === 404) {
        this.notFound.set(true);
      } else {
        this.error.set(messageOf(problemOf(e)));
      }
    } finally {
      if (seq === this.requestSeq) this.loading.set(false);
    }
  }
}
