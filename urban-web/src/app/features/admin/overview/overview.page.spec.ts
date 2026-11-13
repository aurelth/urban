import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PLATFORM_SUMMARY_URL, platformSummary } from '../../../testing/admin.fixtures';
import { setupComponent, settle } from '../../../testing/component.harness';
import { OverviewPage } from './overview.page';

function byTestId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function textOf(element: Element | null | undefined): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

async function open(summary = platformSummary()) {
  const harness = setupComponent(OverviewPage);
  harness.fixture.detectChanges();
  harness.http.expectOne(PLATFORM_SUMMARY_URL).flush(summary);
  await settle(harness.fixture);
  return harness;
}

describe('OverviewPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o titulo e avisa que esta carregando', () => {
    const { fixture, http, html } = setupComponent(OverviewPage);

    fixture.detectChanges();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Visão geral');
    expect(byTestId(html, 'loading')?.getAttribute('role')).toBe('status');
    expect(textOf(byTestId(html, 'loading'))).toContain('Carregando a visão geral');
    http.expectOne(PLATFORM_SUMMARY_URL);
  });

  it('mostra cada numero da plataforma com o seu rotulo', async () => {
    const { html } = await open(
      platformSummary({
        activeManagers: 12,
        suspendedManagers: 2,
        awaitingConfirmationManagers: 3,
        pendingRequests: 4,
        condominiums: 11,
      }),
    );

    const expected: [string, string, string][] = [
      ['stat-active', 'Síndicos ativos', '12'],
      ['stat-suspended', 'Síndicos suspensos', '2'],
      ['stat-awaiting', 'Aguardando confirmação do e-mail', '3'],
      ['stat-requests', 'Pedidos de cadastro', '4'],
      ['stat-condominiums', 'Condomínios', '11'],
    ];
    for (const [id, label, value] of expected) {
      const stat = byTestId(html, id)!;
      expect(textOf(stat.querySelector('dt'))).toBe(label);
      expect(textOf(stat.querySelector('dd'))).toBe(value);
    }
    expect(byTestId(html, 'loading')).toBeNull();
  });

  it('leva aos pedidos de cadastro e a lista de sindicos', async () => {
    const { html } = await open();

    const requests = byTestId(html, 'go-requests') as HTMLAnchorElement;
    const managers = byTestId(html, 'go-managers') as HTMLAnchorElement;
    expect(requests.getAttribute('href')).toBe('/admin/pedidos-de-cadastro');
    expect(managers.getAttribute('href')).toBe('/admin/sindicos');
    expect(textOf(managers)).toContain('Ver síndicos');
  });

  it.each([
    [0, 'Nenhum pedido aguardando análise'],
    [1, '1 pedido aguardando análise'],
    [4, '4 pedidos aguardando análise'],
  ])('com %s pedido(s), o atalho diz "%s"', async (pending, label) => {
    const { html } = await open(platformSummary({ pendingRequests: pending }));

    expect(textOf(byTestId(html, 'go-requests'))).toContain(label);
  });

  it('avisa o erro da busca com um botao para tentar de novo', async () => {
    const { fixture, http, html } = setupComponent(OverviewPage);
    fixture.detectChanges();
    http.expectOne(PLATFORM_SUMMARY_URL).flush(null, { status: 500, statusText: 'Erro' });
    await settle(fixture);

    expect(textOf(byTestId(html, 'load-error'))).toContain(
      'Não foi possível carregar a visão geral',
    );

    byTestId(html, 'retry')!.click();
    await settle(fixture);
    http.expectOne(PLATFORM_SUMMARY_URL).flush(platformSummary());
    await settle(fixture);

    expect(byTestId(html, 'load-error')).toBeNull();
    expect(byTestId(html, 'stat-active')).not.toBeNull();
  });
});
