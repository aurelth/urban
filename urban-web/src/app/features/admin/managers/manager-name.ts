import { Manager } from '../../../core/admin/admin.models';

/** Como a pessoa é chamada nos títulos e nos avisos: o nome, ou o e-mail enquanto ela não informou o nome. */
export function managerLabel(manager: Pick<Manager, 'name' | 'email'>): string {
  return manager.name ?? manager.email;
}
