import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { SEND_CONTACT_URL } from '../../testing/contact.fixtures';
import { setupComponent } from '../../testing/component.harness';
import { ContactPage } from './contact.page';

const CREATED = { status: 201, statusText: 'Created' };
const RECEIPT = { id: 'm-1', sentAt: '2026-11-13T15:05:00+00:00' };
const SUBJECT = 'Corrigir o CNPJ do condomínio';
const BODY = 'Digitei o CNPJ errado no cadastro. Pode corrigir?';

function setup(): ReturnType<typeof setupComponent<ContactPage>> {
  const harness = setupComponent(ContactPage);
  harness.fixture.detectChanges();
  return harness;
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function testId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(root: ParentNode, id: string): string {
  return testId(root, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function fill(html: HTMLElement, subject: string, body: string): void {
  const subjectInput = html.querySelector<HTMLInputElement>('input[formControlName="subject"]')!;
  subjectInput.value = subject;
  subjectInput.dispatchEvent(new Event('input'));
  const bodyInput = html.querySelector<HTMLTextAreaElement>('textarea[formControlName="body"]')!;
  bodyInput.value = body;
  bodyInput.dispatchEvent(new Event('input'));
}

async function submit(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await settle(fixture);
}

function errors(html: HTMLElement): string[] {
  return [...html.querySelectorAll('mat-error')].map((e) => e.textContent!.trim());
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ContactPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('tem o título, o assunto, a mensagem e o botão de enviar', () => {
    const { html } = setup();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Contato');
    expect(html.querySelector('mat-label')?.textContent?.trim()).toBe('Assunto');
    expect(html.querySelectorAll('mat-label')[1].textContent?.trim()).toBe('Mensagem');
    expect(html.querySelector('button[type="submit"]')?.textContent?.trim()).toBe(
      'Enviar mensagem',
    );
    expect(testId(html, 'sent')).toBeNull();
  });

  it('conta os caracteres da mensagem, sem os espaços das pontas', async () => {
    const { fixture, html } = setup();
    expect(text(html, 'body-counter')).toBe('0 de 2000 caracteres');

    fill(html, '', '  Olá!  ');
    fixture.detectChanges();

    expect(text(html, 'body-counter')).toBe('4 de 2000 caracteres');
  });

  describe('validação', () => {
    it('com tudo vazio, não envia e pede o assunto e a mensagem', async () => {
      const { fixture, http, html } = setup();

      await submit(fixture);

      http.expectNone(SEND_CONTACT_URL);
      expect(errors(html)).toEqual(['Informe o assunto', 'Informe a mensagem']);
    });

    it('com texto só de espaços, também pede', async () => {
      const { fixture, http, html } = setup();
      fill(html, '   ', '      ');

      await submit(fixture);

      http.expectNone(SEND_CONTACT_URL);
      expect(errors(html)).toEqual(['Informe o assunto', 'Informe a mensagem']);
    });

    it.each([
      ['Oi', BODY, ['O assunto precisa ter ao menos 3 caracteres']],
      ['a'.repeat(121), BODY, ['O assunto pode ter até 120 caracteres']],
      [SUBJECT, 'Curta', ['A mensagem precisa ter ao menos 10 caracteres']],
      [SUBJECT, 'a'.repeat(2001), ['A mensagem pode ter até 2000 caracteres']],
    ])('recusa o tamanho fora do limite (%#)', async (subject, body, expected) => {
      const { fixture, http, html } = setup();
      fill(html, subject, body);

      await submit(fixture);

      http.expectNone(SEND_CONTACT_URL);
      expect(errors(html)).toEqual(expected);
    });

    it('leva o foco ao primeiro campo inválido', async () => {
      const { fixture, html } = setup();
      document.body.append(html);
      fill(html, SUBJECT, '');

      await submit(fixture);

      expect(document.activeElement).toBe(html.querySelector('textarea[formControlName="body"]'));
      html.remove();
    });
  });

  describe('envio', () => {
    it('manda o texto aparado, trava o botão enquanto envia e mostra a confirmação', async () => {
      const { fixture, http, html } = setup();
      fill(html, `  ${SUBJECT} `, `\n${BODY}  `);

      const sending = submit(fixture);
      const request = http.expectOne(SEND_CONTACT_URL);
      expect(request.request.body).toEqual({ subject: SUBJECT, body: BODY });
      fixture.detectChanges();
      const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
      expect(button.disabled).toBe(true);
      expect(button.textContent?.trim()).toBe('Enviando');
      request.flush(RECEIPT, CREATED);
      await sending;
      await settle(fixture);

      expect(html.querySelector('form')).toBeNull();
      expect(testId(html, 'sent')?.getAttribute('role')).toBe('status');
      expect(testId(html, 'sent')!.querySelector('h2')?.textContent?.trim()).toBe(
        'Mensagem enviada',
      );
      expect(testId(html, 'sent')!.querySelector('mat-icon')).not.toBeNull();
    });

    it('"Enviar outra mensagem" volta ao formulário vazio', async () => {
      const { fixture, http, html } = setup();
      fill(html, SUBJECT, BODY);
      const sending = submit(fixture);
      http.expectOne(SEND_CONTACT_URL).flush(RECEIPT, CREATED);
      await sending;
      await settle(fixture);

      testId(html, 'send-another')!.click();
      await settle(fixture);

      expect(testId(html, 'sent')).toBeNull();
      expect(html.querySelector<HTMLInputElement>('input[formControlName="subject"]')!.value).toBe(
        '',
      );
      expect(
        html.querySelector<HTMLTextAreaElement>('textarea[formControlName="body"]')!.value,
      ).toBe('');
      expect(errors(html)).toEqual([]);
    });

    it.each([
      [
        400,
        'identity.contact.subject_invalid',
        'O assunto precisa ter de 3 a 120 caracteres, em uma só linha',
      ],
      [400, 'identity.contact.body_invalid', 'A mensagem precisa ter de 10 a 2000 caracteres'],
      [
        403,
        'identity.contact.send_forbidden',
        'Só o síndico pode enviar mensagens ao dono da plataforma',
      ],
      [
        429,
        'rate_limit.exceeded',
        'Muitas tentativas em pouco tempo. Aguarde alguns instantes e tente de novo',
      ],
      [500, 'identity.internal', 'Algo deu errado. Tente novamente em instantes'],
    ])('explica a recusa %s %s e guarda o que foi digitado', async (status, code, message) => {
      const { fixture, http, html } = setup();
      fill(html, SUBJECT, BODY);

      const sending = submit(fixture);
      http.expectOne(SEND_CONTACT_URL).flush(...failure(status, code));
      await sending;
      await settle(fixture);

      expect(html.querySelector('[role="alert"]')?.textContent?.trim()).toBe(message);
      expect(testId(html, 'sent')).toBeNull();
      expect(html.querySelector<HTMLInputElement>('input[formControlName="subject"]')!.value).toBe(
        SUBJECT,
      );
      expect(
        html.querySelector<HTMLTextAreaElement>('textarea[formControlName="body"]')!.value,
      ).toBe(BODY);
      expect(html.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
    });

    it('ao tentar de novo, some o erro anterior', async () => {
      const { fixture, http, html } = setup();
      fill(html, SUBJECT, BODY);
      const first = submit(fixture);
      http.expectOne(SEND_CONTACT_URL).flush(...failure(500, 'identity.internal'));
      await first;
      await settle(fixture);
      expect(html.querySelector('[role="alert"]')).not.toBeNull();

      const second = submit(fixture);
      http.expectOne(SEND_CONTACT_URL).flush(RECEIPT, CREATED);
      await second;
      await settle(fixture);

      expect(html.querySelector('[role="alert"]')).toBeNull();
      expect(testId(html, 'sent')).not.toBeNull();
    });
  });
});
