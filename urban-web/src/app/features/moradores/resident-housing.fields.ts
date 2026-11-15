import { SelectOption } from '../../shared/ui/select-field/select.field';
import { FIRST_ENTRY_YEAR } from './resident-field.validators';
import { kindLabel } from './resident-values';

/** Tipos de vínculo que a tela oferece, no cadastro do morador e ao adicionar uma unidade. */
export const KIND_OPTIONS: readonly SelectOption[] = [
  { value: 'owner', label: kindLabel('owner') },
  { value: 'tenant', label: kindLabel('tenant') },
  { value: 'dependent', label: kindLabel('dependent') },
];

/** Textos de erro dos campos da moradia (unidade, vínculo e entrada), iguais no cadastro e em "Adicionar unidade". */
export const HOUSING_ERRORS = {
  unit: { required: 'Escolha a unidade' },
  kind: {
    required: 'Escolha o vínculo: proprietário, inquilino ou dependente',
    minorKind: 'Quem tem menos de 18 anos só pode ser dependente',
  },
} as const;

/** Erros da data de entrada, cada um com o motivo exato; o limite do futuro acompanha o dia de hoje. */
export function startDateErrors(
  clock: () => Date = () => new Date(),
): Readonly<Record<string, string>> {
  const now = clock();
  const [day, month] = [now.getDate(), now.getMonth() + 1].map((n) => String(n).padStart(2, '0'));
  return {
    required: 'Informe a data de entrada',
    date: 'Informe uma data de entrada válida, como 01/03/2024',
    tooOld: `A data de entrada não pode ser anterior a 01/01/${FIRST_ENTRY_YEAR}`,
    future: `A data de entrada pode ser, no máximo, ${day}/${month}/${now.getFullYear() + 1} (1 ano à frente de hoje)`,
  };
}
