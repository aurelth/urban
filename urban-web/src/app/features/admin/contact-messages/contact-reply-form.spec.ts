import { HttpTestingController } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import {
  CONTACT_MESSAGES_URL,
  REPLY_RECEIPT,
  contactMessage,
  contactMessagesPage,
  fileOfSize,
  replyUrl,
} from '../../../testing/contact.fixtures';
import { ContactReplyForm } from './contact-reply-form';
import { ContactMessagesStore } from './contact-messages.store';

const MESSAGE = contactMessage();
const TEXT = 'Corrigi o CNPJ no cadastro. Confira em Meus dados.';
const MB = 1024 * 1024;

interface Harness {
  fixture: ComponentFixture<ContactReplyForm>;
  http: HttpTestingController;
  html: HTMLElement;
  sent: jest.Mock;
  cancelled: jest.Mock;
}

function setup(message = MESSAGE): Harness {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      ContactMessagesStore,
    ],
  });
  const fixture = TestBed.createComponent(ContactReplyForm);
  fixture.componentRef.setInput('message', message);
  const sent = jest.fn();
  const cancelled = jest.fn();
  fixture.componentInstance.sent.subscribe(sent);
  fixture.componentInstance.cancelled.subscribe(cancelled);
  fixture.detectChanges();
  return {
    fixture,
    http: TestBed.inject(HttpTestingController),
    html: fixture.nativeElement,
    sent,
    cancelled,
  };
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

function write(html: HTMLElement, value: string): void {
  const field = html.querySelector<HTMLTextAreaElement>('textarea[formControlName="body"]')!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
}

function pick(html: HTMLElement, files: File[]): void {
  const input = testId(html, 'file-input') as HTMLInputElement;
  Object.defineProperty(input, 'files', { value: files, configurable: true });
  input.dispatchEvent(new Event('change'));
}

async function submit(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await settle(fixture);
}

function attachmentNames(html: HTMLElement): string[] {
  return [...html.querySelectorAll('[data-testid="attachment-name"]')].map((n) =>
    n.textContent!.trim(),
  );
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ContactReplyForm', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('diz a quem a resposta vai e que a pessoa responde ao e-mail do administrador', () => {
      const { html } = setup();

      expect(text(html, 'reply-heading')).toBe('Responder a Ana');
      expect(text(html, 'reply-recipient')).toBe(
        'Enviaremos por e-mail para ana@example.com. Se a pessoa responder, a resposta chega ao seu e-mail',
      );
    });

    it('sem nome, o e-mail é quem recebe, e aparece uma vez no título', () => {
      const { html } = setup(contactMessage({ senderName: null }));

      expect(text(html, 'reply-heading')).toBe('Responder a ana@example.com');
    });

    it('começa vazio, com o contador zerado e sem anexos', () => {
      const { html } = setup();

      expect(
        html.querySelector<HTMLTextAreaElement>('textarea[formControlName="body"]')!.value,
      ).toBe('');
      expect(text(html, 'reply-counter')).toBe('0 de 5000 caracteres');
      expect(testId(html, 'attachments')).toBeNull();
    });

    it('explica os limites dos anexos', () => {
      const { html } = setup();

      expect(text(html, 'attachment-hint')).toBe(
        'Até 3 arquivos de 5 MB, com no máximo 10 MB no total. Aceitamos PDF, PNG, JPG, DOCX, XLSX, CSV e TXT',
      );
    });

    it('o seletor aceita vários arquivos, só dos tipos permitidos', () => {
      const { html } = setup();

      const input = testId(html, 'file-input') as HTMLInputElement;
      expect(input.type).toBe('file');
      expect(input.multiple).toBe(true);
      expect(input.accept).toBe('.pdf,.png,.jpg,.jpeg,.docx,.xlsx,.csv,.txt');
    });

    it('o seletor escondido tem nome acessível, para leitores de tela e para o Sonar', () => {
      const { html } = setup();

      const input = testId(html, 'file-input') as HTMLInputElement;
      expect(input.getAttribute('aria-label')).toBe('Anexar arquivos');
    });

    it('"Anexar arquivos" abre o seletor do navegador', () => {
      const { html } = setup();
      const input = testId(html, 'file-input') as HTMLInputElement;
      const open = jest.spyOn(input, 'click').mockImplementation(() => undefined);

      testId(html, 'attach')!.click();

      expect(open).toHaveBeenCalledTimes(1);
    });
  });

  describe('texto', () => {
    it('conta os caracteres sem os espaços das pontas', () => {
      const { fixture, html } = setup();

      write(html, '   Olá, tudo bem?  ');
      fixture.detectChanges();

      expect(text(html, 'reply-counter')).toBe('14 de 5000 caracteres');
    });

    it.each([
      ['', 'Informe a resposta'],
      ['   ', 'Informe a resposta'],
      ['abc', 'A resposta precisa ter ao menos 5 caracteres'],
      ['a'.repeat(5001), 'A resposta pode ter até 5000 caracteres'],
    ])('enviar "%s" não chama a API e explica o erro', async (value, message) => {
      const { fixture, http, html } = setup();
      write(html, value);

      await submit(fixture);

      http.expectNone(replyUrl(MESSAGE.id));
      expect(html.querySelector('mat-error')?.textContent?.trim()).toBe(message);
    });

    it('leva o foco ao campo inválido', async () => {
      const { fixture, html } = setup();
      document.body.appendChild(html);

      await submit(fixture);

      expect(document.activeElement).toBe(html.querySelector('textarea'));
      html.remove();
    });
  });

  describe('anexos', () => {
    it('mostra o nome e o tamanho de cada arquivo escolhido', () => {
      const { fixture, html } = setup();

      pick(html, [
        fileOfSize('orcamento.pdf', 1536),
        fileOfSize('foto.png', 2.5 * MB, 'image/png'),
      ]);
      fixture.detectChanges();

      expect(attachmentNames(html)).toEqual(['orcamento.pdf', 'foto.png']);
      expect(
        [...html.querySelectorAll('[data-testid="attachment-size"]')].map((n) =>
          n.textContent!.trim(),
        ),
      ).toEqual(['1,5 KB', '2,5 MB']);
    });

    it('acrescenta aos que já estavam, em vez de trocar', () => {
      const { fixture, html } = setup();

      pick(html, [fileOfSize('a.pdf', 10)]);
      pick(html, [fileOfSize('b.pdf', 10)]);
      fixture.detectChanges();

      expect(attachmentNames(html)).toEqual(['a.pdf', 'b.pdf']);
    });

    it('recusa o que não serve, avisa em alerta e guarda o resto', () => {
      const { fixture, html } = setup();

      pick(html, [fileOfSize('bom.pdf', 10), fileOfSize('programa.exe', 10)]);
      fixture.detectChanges();

      expect(attachmentNames(html)).toEqual(['bom.pdf']);
      expect(testId(html, 'attachment-errors')?.getAttribute('role')).toBe('alert');
      expect(text(html, 'attachment-errors')).toBe('“programa.exe”: tipo de arquivo não aceito');
    });

    it('o aviso some quando a pessoa escolhe arquivos que servem', () => {
      const { fixture, html } = setup();
      pick(html, [fileOfSize('programa.exe', 10)]);
      fixture.detectChanges();

      pick(html, [fileOfSize('bom.pdf', 10)]);
      fixture.detectChanges();

      expect(testId(html, 'attachment-errors')).toBeNull();
    });

    it('o botão de anexar some quando já há 3 arquivos', () => {
      const { fixture, html } = setup();

      pick(html, [fileOfSize('a.pdf', 10), fileOfSize('b.pdf', 10), fileOfSize('c.pdf', 10)]);
      fixture.detectChanges();

      expect(testId(html, 'attach')).toBeNull();
    });

    it('remove um arquivo, pelo botão com o nome dele', () => {
      const { fixture, html } = setup();
      pick(html, [fileOfSize('a.pdf', 10), fileOfSize('b.pdf', 10)]);
      fixture.detectChanges();

      const remove = html.querySelectorAll<HTMLButtonElement>('[data-testid="remove-attachment"]');
      expect(remove[0].getAttribute('aria-label')).toBe('Remover a.pdf');
      remove[0].click();
      fixture.detectChanges();

      expect(attachmentNames(html)).toEqual(['b.pdf']);
    });
  });

  describe('enviar', () => {
    it('manda o texto aparado e os arquivos, busca a lista e avisa que enviou', async () => {
      const { fixture, http, html, sent } = setup();
      write(html, `  ${TEXT}  `);
      pick(html, [fileOfSize('orcamento.pdf', 100)]);

      await submit(fixture);

      const request = http.expectOne(replyUrl(MESSAGE.id));
      const form = request.request.body as FormData;
      expect(form.get('body')).toBe(TEXT);
      expect(form.getAll('attachments').map((file) => (file as File).name)).toEqual([
        'orcamento.pdf',
      ]);
      expect(sent).not.toHaveBeenCalled();
      request.flush(REPLY_RECEIPT);
      await flushPromises();
      http
        .expectOne((r) => r.url === CONTACT_MESSAGES_URL && r.method === 'GET')
        .flush(contactMessagesPage([MESSAGE]));
      await settle(fixture);

      expect(sent).toHaveBeenCalledTimes(1);
    });

    it('trava o formulário e troca o texto do botão enquanto envia', async () => {
      const { fixture, http, html } = setup();
      write(html, TEXT);
      pick(html, [fileOfSize('a.pdf', 10)]);
      fixture.detectChanges();

      await submit(fixture);

      const submitButton = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
      expect(submitButton.disabled).toBe(true);
      expect(submitButton.textContent?.trim()).toBe('Enviando');
      expect(html.querySelector<HTMLTextAreaElement>('textarea')!.readOnly).toBe(true);
      expect((testId(html, 'cancel-reply') as HTMLButtonElement).disabled).toBe(true);
      expect(
        (html.querySelector('[data-testid="remove-attachment"]') as HTMLButtonElement).disabled,
      ).toBe(true);
      http.expectOne(replyUrl(MESSAGE.id));
    });

    it.each([
      [400, 'identity.contact.reply_body_invalid', 'A resposta precisa ter de 5 a 5000 caracteres'],
      [
        400,
        'identity.contact.attachment_name_invalid',
        'O nome de um dos arquivos não pode ser usado. Renomeie o arquivo e tente de novo',
      ],
      [
        400,
        'identity.contact.attachment_type_not_allowed',
        'Só PDF, PNG, JPG, DOCX, XLSX, CSV e TXT podem ser anexados',
      ],
      [
        400,
        'identity.contact.attachment_content_invalid',
        'O conteúdo de um dos arquivos não confere com o tipo dele. Escolha outro arquivo',
      ],
      [400, 'identity.contact.attachments_too_many', 'Anexe no máximo 3 arquivos'],
      [400, 'request.invalid', 'O envio não pôde ser lido. Confira os arquivos e tente de novo'],
      [413, 'identity.contact.attachment_too_large', 'Cada arquivo pode ter até 5 MB'],
      [413, 'identity.contact.attachments_total_too_large', 'Os anexos somam mais de 10 MB'],
      [403, 'identity.contact.reply_forbidden', 'Só o administrador pode responder às mensagens'],
      [
        409,
        'identity.contact.reply_recipient_unavailable',
        'A conta de quem escreveu não existe mais. Não há para onde enviar a resposta',
      ],
      [
        502,
        'identity.contact.reply_not_delivered',
        'O e-mail não pôde ser entregue ao provedor de envio. Nada foi registrado; tente de novo em instantes',
      ],
      [
        404,
        'identity.contact.message_not_found',
        'Esta mensagem não existe mais. A lista foi atualizada',
      ],
      [500, 'identity.internal', 'Algo deu errado. Tente novamente em instantes'],
    ])('explica a recusa %s %s e guarda o que foi digitado', async (status, code, message) => {
      const { fixture, http, html, sent } = setup();
      write(html, TEXT);
      pick(html, [fileOfSize('orcamento.pdf', 100)]);
      await submit(fixture);

      http.expectOne(replyUrl(MESSAGE.id)).flush(...failure(status, code));
      await flushPromises();
      if (status === 404) {
        http
          .expectOne((r) => r.url === CONTACT_MESSAGES_URL && r.method === 'GET')
          .flush(contactMessagesPage([]));
      }
      await settle(fixture);

      expect(html.querySelector('urban-form-submit [role="alert"]')?.textContent?.trim()).toBe(
        message,
      );
      expect(html.querySelector<HTMLTextAreaElement>('textarea')!.value).toBe(TEXT);
      expect(attachmentNames(html)).toEqual(['orcamento.pdf']);
      expect(sent).not.toHaveBeenCalled();
      expect(html.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(false);
    });

    it('depois de uma recusa dá para corrigir e enviar de novo', async () => {
      const { fixture, http, html, sent } = setup();
      write(html, TEXT);
      await submit(fixture);
      http
        .expectOne(replyUrl(MESSAGE.id))
        .flush(...failure(502, 'identity.contact.reply_not_delivered'));
      await settle(fixture);

      await submit(fixture);
      http.expectOne(replyUrl(MESSAGE.id)).flush(REPLY_RECEIPT);
      await flushPromises();
      http
        .expectOne((r) => r.url === CONTACT_MESSAGES_URL && r.method === 'GET')
        .flush(contactMessagesPage([MESSAGE]));
      await settle(fixture);

      expect(sent).toHaveBeenCalledTimes(1);
      expect(html.querySelector('urban-form-submit [role="alert"]')).toBeNull();
    });
  });

  describe('cancelar', () => {
    it('avisa que a pessoa desistiu, sem chamar a API', () => {
      const { http, html, cancelled } = setup();
      write(html, TEXT);

      testId(html, 'cancel-reply')!.click();

      expect(cancelled).toHaveBeenCalledTimes(1);
      http.expectNone(replyUrl(MESSAGE.id));
    });
  });
});
