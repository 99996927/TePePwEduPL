import { Component } from '@angular/core';
import { Routes } from '@angular/router';

// Placeholder removed in Task 3 (replaced by the real login page).
@Component({ selector: 'app-login-placeholder', template: '' })
class LoginPlaceholder {}

export const routes: Routes = [
  { path: 'logowanie', component: LoginPlaceholder },
  { path: '**', redirectTo: '' },
];
