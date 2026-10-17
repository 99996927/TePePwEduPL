import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { messageOf, problemOf } from '../../core/api-problem';
import { IS_DEV_MODE } from '../../core/dev-mode';
import { SessionService } from '../../core/session';

interface DemoAccount {
  userName: string;
  password: string;
  role: string;
}

// Demo accounts from docs/arc42/07-widok-wdrozenia.md, section 7.2.
// Empty in production builds (ngDevMode is false there), so the literals are tree-shaken out of the bundle.
const DEMO_ACCOUNTS: readonly DemoAccount[] =
  typeof ngDevMode === 'undefined' || ngDevMode
    ? [
        { userName: 'anna.nowak', password: 'annanowak123', role: 'Employee' },
        { userName: 'jan.kowalski', password: 'jankowalski123', role: 'Employee' },
        { userName: 'tomasz.lis', password: 'tomaszlis123', role: 'Employee' },
        { userName: 'ewa.wisniewska', password: 'ewawisniewska123', role: 'Manager' },
        { userName: 'piotr.zielinski', password: 'piotrzielinski123', role: 'HR' },
        { userName: 'maria.kaminska', password: 'mariakaminska123', role: 'HRApprover' },
      ]
    : [];

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, MatCardModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  private readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap.pipe(map((q) => q)));

  protected readonly devMode = inject(IS_DEV_MODE);
  protected readonly demoAccounts = DEMO_ACCOUNTS;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    userName: ['', Validators.required],
    password: ['', Validators.required],
  });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly sessionExpired = computed(() => this.query()?.get('sesja') === 'wygasla');

  protected fill(account: DemoAccount): void {
    this.form.setValue({ userName: account.userName, password: account.password });
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      return;
    }
    const { userName, password } = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set(null);
    try {
      await this.session.login(userName, password);
      await this.router.navigateByUrl(this.returnUrl());
    } catch (error) {
      const problem = problemOf(error);
      this.errorMessage.set(
        problem.status === 401
          ? 'Login lub hasło są nieprawidłowe.'
          : problem.status === 423
            ? 'Konto jest zablokowane. Spróbuj ponownie później.'
            : messageOf(problem),
      );
    } finally {
      this.submitting.set(false);
    }
  }

  /** Only same-origin absolute paths are accepted, to avoid open redirects. */
  private returnUrl(): string {
    const value = this.query()?.get('returnUrl') ?? null;
    const safe = value !== null && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\');
    return safe ? value : '/';
  }
}
