import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Resident, ResidentAccessStatus } from '../../core/residents/residents.models';
import { setupComponent, settle } from '../../testing/component.harness';
import { resident } from '../../testing/residents.fixtures';
import { ResidentAccess } from './resident-access';

interface Opened {
  fixture: ComponentFixture<ResidentAccess>;
  http: HttpTestingController;
  html: HTMLElement;
  /** Quantas vezes o componente avisou o pai de que o convite saiu. */
  invited: { count: number };
}

function open(found: Resident, blockedReason: string | null = null): Opened {
  const { fixture, http, html } = setupComponent(ResidentAccess);
  fixture.componentRef.setInput('resident', found);
  fixture.componentRef.setInput('blockedReason', blockedReason);
  const invited = { count: 0 };
  fixture.componentInstance.invited.subscribe(() => (invited.count += 1));
  fixture.detectChanges();
  return { fixture, http, html, invited };
}

function withStatus(accessStatus: ResidentAccessStatus, email: string | null = 'ana@exemplo.com') {
  return resident({ fullName: 'Ana Lima', email, accessStatus });
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(html: HTMLElement, id: string): string {
  return testId(html, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function invitation(http: HttpTestingController, id: string): TestRequest {
  return http.expectOne(`/api/residents/${id}/invitation`);
}

async function click(opened: Opened, id: string): Promise<void> {
  testId(opened.html, id)!.click();
  opened.fixture.detectChanges();
}

function reject(request: TestRequest, status: number, code?: string): void {
  request.flush(code === undefined ? null : { status, code }, { status, statusText: 'Erro' });
}

describe('ResidentAccess', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  describe('situação', () => {
    it.each([
      ['none', 'Sem acesso'],
      ['invited', 'Convite enviado'],
      ['active', 'Acesso ativo'],
      ['inactive', 'Acesso desativado'],
    ] as const)('mostra "%s" como "%s", em texto e com ícone', (status, label) => {
      const { html } = open(withStatus(status));

      expect(text(html, 'access-status')).toBe(label);
      expect(testId(html, 'access-status')?.querySelector('mat-icon')).not.toBeNull();
    });
  });

  describe('ações', () => {
    it('sem acesso e com e-mail, oferece "Convidar" com o nome do morador no rótulo', () => {
      const { html } = open(withStatus('none'));

      const button = testId(html, 'invite');
      expect(button?.textContent?.trim()).toBe('Convidar');
      expect(button?.getAttribute('aria-label')).toBe('Convidar Ana Lima');
      expect(testId(html, 'resend-invite')).toBeNull();
    });

    it('sem acesso e sem e-mail, não oferece o botão e explica o que falta', () => {
      const { html } = open(withStatus('none', null));

      expect(testId(html, 'invite')).toBeNull();
      expect(text(html, 'access-hint')).toBe('Cadastre o e-mail do morador para poder convidá-lo');
    });

    it('com convite enviado, oferece "Reenviar convite" e não "Convidar"', () => {
      const { html } = open(withStatus('invited'));

      const button = testId(html, 'resend-invite');
      expect(button?.textContent?.trim()).toBe('Reenviar convite');
      expect(button?.getAttribute('aria-label')).toBe('Reenviar convite para Ana Lima');
      expect(testId(html, 'invite')).toBeNull();
    });

    it.each(['active', 'inactive'] as const)('com acesso "%s", não oferece convite', (status) => {
      const { html } = open(withStatus(status));

      expect(testId(html, 'invite')).toBeNull();
      expect(testId(html, 'resend-invite')).toBeNull();
    });
  });

  describe('convidar', () => {
    it('manda o convite, trava o botão enquanto envia e avisa quando o e-mail foi pedido', async () => {
      const found = withStatus('none');
      const opened = open(found);

      await click(opened, 'invite');
      const request = invitation(opened.http, found.id);
      expect(request.request.method).toBe('POST');
      expect(testId(opened.html, 'invite')?.hasAttribute('disabled')).toBe(true);
      expect(testId(opened.html, 'invite')?.textContent?.trim()).toBe('Enviando');

      request.flush(null, { status: 202, statusText: 'Accepted' });
      await settle(opened.fixture);

      expect(text(opened.html, 'access-notice')).toBe('Convite enviado para ana@exemplo.com');
      expect(testId(opened.html, 'access-error')).toBeNull();
      expect(testId(opened.html, 'invite')?.hasAttribute('disabled')).toBe(false);
      expect(opened.invited.count).toBe(1);
    });

    it('no reenvio, o aviso diz "reenviado"', async () => {
      const found = withStatus('invited');
      const opened = open(found);

      await click(opened, 'resend-invite');
      invitation(opened.http, found.id).flush(null, { status: 202, statusText: 'Accepted' });
      await settle(opened.fixture);

      expect(text(opened.html, 'access-notice')).toBe('Convite reenviado para ana@exemplo.com');
      expect(opened.invited.count).toBe(1);
    });

    it('um segundo clique durante o envio não manda outro pedido', async () => {
      const found = withStatus('none');
      const opened = open(found);

      await click(opened, 'invite');
      await click(opened, 'invite');

      invitation(opened.http, found.id).flush(null, { status: 202, statusText: 'Accepted' });
      await settle(opened.fixture);
      expect(opened.invited.count).toBe(1);
    });
  });

  describe('recusas', () => {
    it.each([
      [
        422,
        'condominium.resident.email_required',
        'Cadastre o e-mail do morador para poder convidá-lo',
      ],
      [
        409,
        'identity.resident_access.email_unavailable',
        'Este e-mail já tem uma conta no Urban e não pode receber outro convite. Corrija o e-mail do morador',
      ],
      [
        409,
        'identity.resident_access.not_pending',
        'Este morador já criou a senha. Recarregue a página para ver a situação atual',
      ],
      [
        429,
        'identity.resident_access.resend_too_soon',
        'Um convite acabou de ser enviado. Espere um minuto para enviar outro',
      ],
      [
        404,
        'identity.resident_access.not_found',
        'Este morador não tem convite para reenviar. Recarregue a página',
      ],
      [404, 'condominium.resident.not_found', 'Este morador não existe mais. Recarregue a página'],
      [
        403,
        'identity.resident_access.forbidden',
        'Só o síndico com a conta ativa pode convidar moradores',
      ],
      [500, 'qualquer.coisa', 'Algo deu errado. Tente novamente em instantes'],
    ])('%i %s vira texto de alerta, sem avisar o pai', async (status, code, message) => {
      const found = withStatus('none');
      const opened = open(found);

      await click(opened, 'invite');
      reject(invitation(opened.http, found.id), status, code);
      await settle(opened.fixture);

      expect(text(opened.html, 'access-error')).toBe(message);
      expect(testId(opened.html, 'access-error')?.getAttribute('role')).toBe('alert');
      expect(testId(opened.html, 'access-notice')).toBeNull();
      expect(testId(opened.html, 'invite')?.hasAttribute('disabled')).toBe(false);
      expect(opened.invited.count).toBe(0);
    });

    it('uma nova tentativa limpa a recusa anterior', async () => {
      const found = withStatus('none');
      const opened = open(found);
      await click(opened, 'invite');
      reject(invitation(opened.http, found.id), 500);
      await settle(opened.fixture);
      expect(testId(opened.html, 'access-error')).not.toBeNull();

      await click(opened, 'invite');
      expect(testId(opened.html, 'access-error')).toBeNull();
      invitation(opened.http, found.id).flush(null, { status: 202, statusText: 'Accepted' });
      await settle(opened.fixture);

      expect(testId(opened.html, 'access-notice')).not.toBeNull();
    });
  });

  describe('bloqueado', () => {
    it('mostra o motivo, desabilita o botão e não chama a API', async () => {
      const found = withStatus('none');
      const opened = open(found, 'Salve as alterações antes de convidar');

      expect(text(opened.html, 'access-blocked')).toBe('Salve as alterações antes de convidar');
      expect(testId(opened.html, 'invite')?.hasAttribute('disabled')).toBe(true);

      await click(opened, 'invite');
      opened.http.expectNone(`/api/residents/${found.id}/invitation`);
    });

    it('sem motivo, não mostra o aviso', () => {
      const { html } = open(withStatus('none'));

      expect(testId(html, 'access-blocked')).toBeNull();
    });
  });
});
