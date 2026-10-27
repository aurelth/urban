import { roleLabel } from './role-label';

describe('roleLabel', () => {
  it.each([
    ['SuperAdmin', 'Administrador'],
    ['Manager', 'Síndico'],
    ['Resident', 'Morador'],
  ] as const)('traduz %s', (role, label) => {
    expect(roleLabel(role)).toBe(label);
  });
});
