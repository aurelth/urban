import { SelectOption } from '../../shared/ui/select-field/select.field';
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
  startDate: {
    required: 'Informe a data de entrada',
    date: 'Informe uma data de entrada válida, como 01/03/2024',
  },
} as const;
