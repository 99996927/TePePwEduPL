import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiProblem, messageOf } from './api-problem';

@Injectable({ providedIn: 'root' })
export class Notifier {
  private readonly snackBar = inject(MatSnackBar);

  error(problem: ApiProblem | string): void {
    this.show(typeof problem === 'string' ? problem : messageOf(problem), 'error-snackbar');
  }

  info(message: string): void {
    this.show(message);
  }

  private show(message: string, panelClass?: string): void {
    this.snackBar.open(message, 'Zamknij', {
      duration: 5000,
      politeness: 'assertive',
      panelClass,
    });
  }
}
