import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { appConfig } from './app.config';

describe('appConfig', () => {
  it('liga o tamanho dinâmico da área de erro dos campos em todo o app', () => {
    const provider = appConfig.providers.find(
      (candidate) =>
        (candidate as { provide?: unknown }).provide === MAT_FORM_FIELD_DEFAULT_OPTIONS,
    ) as { useValue: { subscriptSizing?: string } } | undefined;

    expect(provider?.useValue.subscriptSizing).toBe('dynamic');
  });
});
