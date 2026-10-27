import { UserRole } from './auth.models';

const LABELS: Readonly<Record<UserRole, string>> = {
  SuperAdmin: 'Administrador',
  Manager: 'Síndico',
  Resident: 'Morador',
};

export function roleLabel(role: UserRole): string {
  return LABELS[role];
}
