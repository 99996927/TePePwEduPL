export const Roles = {
  Employee: 'Employee',
  Manager: 'Manager',
  HR: 'HR',
  HRApprover: 'HRApprover',
} as const;

export type Role = (typeof Roles)[keyof typeof Roles];

export const APPROVER_ROLES: readonly Role[] = [Roles.Manager, Roles.HR, Roles.HRApprover];

export const HR_ROLES: readonly Role[] = [Roles.HR, Roles.HRApprover];
