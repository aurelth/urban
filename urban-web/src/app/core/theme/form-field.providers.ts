import { Provider } from '@angular/core';
import {
  MAT_FORM_FIELD_DEFAULT_OPTIONS,
  MatFormFieldDefaultOptions,
} from '@angular/material/form-field';

/**
 * Faz a área do erro (e da dica) de todo campo do Material crescer com o texto. No tamanho fixo, que é o padrão do
 * Material, ela tem uma linha só: uma mensagem que quebra em duas, numa coluna estreita (como o "Número" do
 * endereço), vaza por cima do campo de baixo. Como a área deixa de reservar espaço, quem coloca o campo reserva o
 * espaço abaixo dele (`mb-5` nos componentes `urban-*-field`).
 */
export function provideFormFieldDefaults(): Provider {
  const options: MatFormFieldDefaultOptions = { subscriptSizing: 'dynamic' };
  return { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: options };
}
