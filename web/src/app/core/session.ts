import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CurrentUserDto } from '../api/models/current-user-dto';
import { AuthService } from '../api/services/auth.service';
import { problemOf } from './api-problem';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly auth = inject(AuthService);
  private readonly userState = signal<CurrentUserDto | null>(null);
  private readonly loadedState = signal(false);

  readonly user = this.userState.asReadonly();
  readonly loaded = this.loadedState.asReadonly();
  readonly isAuthenticated = computed(() => this.userState() !== null);

  hasRole(...roles: string[]): boolean {
    const own = this.userState()?.roles ?? [];
    return roles.some((role) => own.includes(role));
  }

  async load(): Promise<void> {
    try {
      this.userState.set(await firstValueFrom(this.auth.authMe()));
    } catch (error) {
      if (!(error instanceof HttpErrorResponse)) throw error;
      this.userState.set(null);
    } finally {
      this.loadedState.set(true);
    }
  }

  async login(userName: string, password: string): Promise<void> {
    try {
      await firstValueFrom(this.auth.authAntiforgery());
      this.userState.set(await firstValueFrom(this.auth.authLogin({ body: { userName, password } })));
      this.loadedState.set(true);
    } catch (error) {
      throw problemOf(error);
    }
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.auth.authLogout());
    } finally {
      this.clear();
    }
  }

  clear(): void {
    this.userState.set(null);
  }
}
