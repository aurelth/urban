import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { appConfig } from './app.config';

describe('appConfig', () => {
  // Importar o token do MatFormField no arquivo de configuração traz o componente inteiro (cerca de 70 kB) para
  // o pacote inicial. O tamanho dinâmico da área de erro vive em cada campo `urban-*-field`.
  it('não liga o MatFormField globalmente, para o componente ficar fora do pacote inicial', () => {
    const provider = appConfig.providers.find(
      (candidate) =>
        (candidate as { provide?: unknown }).provide === MAT_FORM_FIELD_DEFAULT_OPTIONS,
    );

    expect(provider).toBeUndefined();
  });
});
