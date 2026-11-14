import { SelectOption } from '../../shared/ui/select-field/select.field';
import { kindLabel } from './resident-values';

/** Tipos de vínculo que a tela oferece, no cadastro do morador e ao adicionar uma unidade. */
export const KIND_OPTIONS: readonly SelectOption[] = [
  { value: 'owner', label: kindLabel('owner') },
  { value: 'tenant', label: kindLabel('tenant') },
];

/** Textos de erro dos campos da moradia (unidade, vínculo e entrada), iguais no cadastro e em "Adicionar unidade". */
export const HOUSING_ERRORS = {
  unit: { required: 'Escolha a unidade' },
  kind: { required: 'Escolha se é proprietário ou inquilino' },
  startDate: {
    required: 'Informe a data de entrada',
    date: 'Informe uma data de entrada válida, como 01/03/2024',
  },
} as const;
