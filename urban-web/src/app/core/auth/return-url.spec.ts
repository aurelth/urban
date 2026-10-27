import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it.each([
    ['/dashboard', '/dashboard'],
    ['/moradores?pagina=2#topo', '/moradores?pagina=2#topo'],
    ['/a/b', '/a/b'],
  ])('mantem o caminho interno %s', (raw, expected) => {
    expect(safeReturnUrl(raw)).toBe(expected);
  });

  it.each([
    undefined,
    null,
    '',
    'dashboard',
    'moradores',
    ' /dashboard',
    'https://outro.example',
    '//outro.example',
    '/\\outro.example',
    '/\\/outro.example',
    '/\t/outro.example',
    'javascript:alert(1)',
    '/entrar',
    '/entrar?returnUrl=%2Fdashboard',
  ])('descarta %p e volta para o painel', (raw) => {
    expect(safeReturnUrl(raw)).toBe('/dashboard');
  });

  it('aceita outro destino padrao', () => {
    expect(safeReturnUrl('https://outro.example', '/inicio')).toBe('/inicio');
  });
});
