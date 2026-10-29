import { NonNullableFormBuilder, Validators } from '@angular/forms';
import { postalCodeFormat } from './postal-code.validator';
import { requiredText } from './required-text.validator';

/** Tamanhos máximos que a API aceita em cada parte do endereço. */
export const ADDRESS_MAX_LENGTH = {
  street: 120,
  number: 10,
  complement: 60,
  neighborhood: 80,
  city: 80,
} as const;

/** Endereço com as regras da API, igual para o do síndico e para o do condomínio. */
export function addressGroup(builder: NonNullableFormBuilder) {
  return builder.group({
    postalCode: ['', [requiredText, postalCodeFormat]],
    street: ['', [requiredText, Validators.maxLength(ADDRESS_MAX_LENGTH.street)]],
    number: ['', [requiredText, Validators.maxLength(ADDRESS_MAX_LENGTH.number)]],
    complement: ['', Validators.maxLength(ADDRESS_MAX_LENGTH.complement)],
    neighborhood: ['', [requiredText, Validators.maxLength(ADDRESS_MAX_LENGTH.neighborhood)]],
    city: ['', [requiredText, Validators.maxLength(ADDRESS_MAX_LENGTH.city)]],
    state: ['', Validators.required],
  });
}
