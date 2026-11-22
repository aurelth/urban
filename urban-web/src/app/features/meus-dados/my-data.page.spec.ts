import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { Resident } from '../../core/residents/residents.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { occupancy, resident } from '../../testing/residents.fixtures';
import { MyDataPage } from './my-data.page';

const ROUTE = '/api/residents/me';

interface Harness {
  fixture: ComponentFixture<MyDataPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

async function open(
  answer: Resident | 'pending' | { status: number } = resident(),
): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  const fixture = TestBed.createComponent(MyDataPage);
  fixture.detectChanges();
  const request = http.expectOne(ROUTE);
  if (answer !== 'pending') {
    if ('status' in answer) {
      request.flush(
        { code: answer.status === 404 ? 'condominium.resident.not_found' : 'x' },
        { status: answer.status, statusText: 'Erro' },
      );
    } else {
      request.flush(answer);
    }
  }
  await settle(fixture);
  return { fixture, http, html: fixture.nativeElement };
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(html: HTMLElement, id: string): string {
  return (testId(html, id)?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function headings(html: HTMLElement): string[] {
  return [...html.querySelectorAll('h2')].map((h) => h.textContent!.trim());
}

describe('MyDataPage', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('mostra o título e avisa que a correção dos dados é com o síndico', async () => {
    const { html } = await open();

    expect(html.querySelector('h1')!.textContent!.trim()).toBe('Meus dados');
    expect(text(html, 'correction-note')).toBe('Para corrigir qualquer dado, fale com o síndico');
  });

  it('enquanto o cadastro não chega, avisa que está carregando', async () => {
    const { html } = await open('pending');

    expect(testId(html, 'loading')!.getAttribute('role')).toBe('status');
    expect(text(html, 'loading')).toBe('Carregando seus dados');
    expect(testId(html, 'personal-section')).toBeNull();
  });

  describe('dados pessoais', () => {
    it('mostra nome, CPF mascarado, nascimento, celular, e-mail e profissão', async () => {
      const { html } = await open();

      expect(headings(html)[0]).toBe('Dados pessoais');
      expect(text(html, 'full-name')).toBe('Maria Souza');
      expect(text(html, 'cpf')).toBe('***.456.789-**');
      expect(text(html, 'birth-date')).toBe('20/05/1990');
      expect(text(html, 'phone')).toBe('(47) 99876-5432');
      expect(text(html, 'email')).toBe('maria@exemplo.com');
      expect(text(html, 'profession')).toBe('Engenheira');
    });

    it('sem e-mail ou profissão, diz que não foram informados', async () => {
      const { html } = await open(resident({ email: null, profession: null }));

      expect(text(html, 'email')).toBe('Não informado');
      expect(text(html, 'profession')).toBe('Não informada');
    });
  });

  describe('contato de emergência', () => {
    it('mostra nome, parentesco e telefone', async () => {
      const { html } = await open();

      expect(headings(html)).toContain('Contato de emergência');
      expect(text(html, 'emergency-contact')).toContain('Carlos Souza');
      expect(text(html, 'emergency-contact')).toContain('Irmão');
      expect(text(html, 'emergency-contact')).toContain('(47) 3333-4444');
    });

    it('sem contato, o cartão nem aparece', async () => {
      const { html } = await open(resident({ emergencyContact: null }));

      expect(testId(html, 'emergency-section')).toBeNull();
      expect(headings(html)).not.toContain('Contato de emergência');
    });
  });

  describe('onde moro', () => {
    it('lista cada vínculo com a unidade, o tipo e o período, os em andamento e os encerrados', async () => {
      const { html } = await open(
        resident({
          occupancies: [
            occupancy({ unitBlock: 'A', unitNumber: '101' }),
            occupancy({
              unitBlock: 'B',
              unitNumber: '202',
              kind: 'tenant',
              startDate: '2023-01-10',
              endDate: '2026-10-31',
            }),
          ],
        }),
      );

      expect(headings(html)).toContain('Onde moro');
      const items = [...html.querySelectorAll('[data-testid="occupancy"]')].map((item) =>
        item.textContent!.replace(/\s+/g, ' ').trim(),
      );
      expect(items).toHaveLength(2);
      expect(items[0]).toContain('Bloco A · 101');
      expect(items[0]).toContain('Proprietário, desde 01/03/2024');
      expect(items[0]).toContain('Mora aqui');
      expect(items[1]).toContain('Bloco B · 202');
      expect(items[1]).toContain('Inquilino, de 10/01/2023 a 31/10/2026');
      expect(items[1]).toContain('Já saiu');
    });

    it('sem vínculo, pede para falar com o síndico', async () => {
      const { html } = await open(resident({ occupancies: [] }));

      expect(text(html, 'no-occupancies')).toBe(
        'Você ainda não está vinculado a nenhuma unidade. Fale com o síndico',
      );
    });
  });

  it('é só leitura: sem formulário, campo nem botão de gravar', async () => {
    const { html } = await open();

    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('input, select, textarea')).toBeNull();
    expect(html.querySelector('button')).toBeNull();
  });

  describe('quando o cadastro não abre', () => {
    it('conta sem cadastro de morador (404): explica e manda falar com o síndico', async () => {
      const { html } = await open({ status: 404 });

      expect(testId(html, 'not-found')!.getAttribute('role')).toBe('alert');
      expect(text(html, 'not-found')).toBe(
        'Não encontramos o seu cadastro de morador. Fale com o síndico',
      );
      expect(testId(html, 'personal-section')).toBeNull();
    });

    it('outra falha: avisa e "Tentar de novo" busca outra vez', async () => {
      const { fixture, http, html } = await open({ status: 500 });

      expect(text(html, 'load-error')).toContain('Não foi possível carregar seus dados');
      testId(html, 'retry')!.click();
      await settle(fixture);
      http.expectOne(ROUTE).flush(resident());
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(text(html, 'full-name')).toBe('Maria Souza');
    });
  });
});
