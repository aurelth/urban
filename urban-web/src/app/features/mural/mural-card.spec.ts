import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MyAnnouncement } from '../../core/announcements/announcements.models';
import { NOW } from '../../core/finance/clock';
import { provideUrbanIcons } from '../../core/theme/icons';
import { myAnnouncement } from '../../testing/announcements.fixtures';
import { MuralCard } from './mural-card';

const CLOCK = new Date('2026-11-22T15:00:00Z');

interface Harness {
  fixture: ComponentFixture<MuralCard>;
  html: HTMLElement;
  emitted: () => number;
}

async function setup(item: MyAnnouncement, marking = false): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      { provide: NOW, useValue: () => CLOCK },
    ],
  });
  const fixture = TestBed.createComponent(MuralCard);
  fixture.componentRef.setInput('item', item);
  fixture.componentRef.setInput('marking', marking);
  let count = 0;
  fixture.componentInstance.markRead.subscribe(() => (count += 1));
  await fixture.whenStable();
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement, emitted: () => count };
}

function byId(html: ParentNode, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function textOf(element: Element | null): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('MuralCard', () => {
  afterEach(() => TestBed.inject(HttpTestingController).match(() => true));

  it('mostra o título, o texto e a categoria em palavras e com ícone', async () => {
    const { html } = await setup(
      myAnnouncement({
        title: 'Água cortada na terça',
        body: 'A rua será interditada.',
        category: 'maintenance',
      }),
    );

    expect(textOf(html.querySelector('h3'))).toBe('Água cortada na terça');
    expect(textOf(html.querySelector('urban-expandable-text'))).toContain(
      'A rua será interditada.',
    );
    const category = byId(html, 'category')!;
    expect(textOf(category)).toBe('Manutenção');
    expect(category.querySelector('mat-icon')).not.toBeNull();
    expect(category.className).toContain('bg-tag-amber-container');
  });

  it('mostra para quem o aviso vale, na voz de quem lê', async () => {
    const { html } = await setup(
      myAnnouncement({ scope: 'unit', unit: { id: 'u-1', block: 'A', number: '101' } }),
    );

    expect(textOf(byId(html, 'scope'))).toBe('Sua unidade 101 do bloco A');
    expect(byId(html, 'scope')!.className).toContain('bg-tag-pink-container');
  });

  it('diz há quanto tempo o aviso saiu e guarda o dia e a hora exatos no title', async () => {
    const { html } = await setup(myAnnouncement({ createdAt: '2026-11-22T13:00:00Z' }));

    const published = byId(html, 'published')!;
    expect(textOf(published)).toBe('há 2 h');
    expect(published.getAttribute('title')).toBe('22/11/2026 às 10:00');
    expect(published.tagName).toBe('TIME');
    expect(published.getAttribute('datetime')).toBe('2026-11-22T13:00:00Z');
  });

  describe('aviso não lido', () => {
    it('se destaca: marcado como novo em texto, e oferece "Marcar como lido"', async () => {
      const { html } = await setup(myAnnouncement({ title: 'Reunião' }));

      expect(byId(html, 'mural-card')!.getAttribute('data-unread')).toBe('true');
      expect(textOf(byId(html, 'new-badge'))).toBe('Novo');
      const button = byId(html, 'mark-read') as HTMLButtonElement;
      expect(textOf(button)).toBe('Marcar como lido');
      expect(button.getAttribute('aria-label')).toBe('Marcar como lido: Reunião');
      expect(byId(html, 'read-state')).toBeNull();
    });

    it('avisa o mural quando o morador marca como lido', async () => {
      const { html, emitted } = await setup(myAnnouncement());

      byId(html, 'mark-read')!.click();

      expect(emitted()).toBe(1);
    });

    it('trava o botão enquanto a leitura está indo para a API', async () => {
      const { html } = await setup(myAnnouncement(), true);

      expect((byId(html, 'mark-read') as HTMLButtonElement).disabled).toBe(true);
    });
  });

  describe('aviso lido', () => {
    it('diz quando foi lido, sem "Novo" e sem o botão', async () => {
      const { html } = await setup(myAnnouncement({ read: true, readAt: '2026-11-22T13:50:00Z' }));

      expect(byId(html, 'mural-card')!.getAttribute('data-unread')).toBe('false');
      expect(byId(html, 'new-badge')).toBeNull();
      expect(byId(html, 'mark-read')).toBeNull();
      const state = byId(html, 'read-state')!;
      expect(textOf(state)).toBe('Lido em 22/11/2026 às 10:50');
      expect(state.querySelector('mat-icon')).not.toBeNull();
    });
  });

  describe('aviso editado', () => {
    it('mostra "Editado" com o dia e a hora da edição no title', async () => {
      const { html } = await setup(myAnnouncement({ updatedAt: '2026-11-22T14:00:00Z' }));

      const edited = byId(html, 'edited')!;
      expect(textOf(edited)).toBe('Editado');
      expect(edited.getAttribute('title')).toBe('Editado em 22/11/2026 às 11:00');
    });

    it('não mostra a marca quando o aviso nunca foi editado', async () => {
      const { html } = await setup(myAnnouncement());

      expect(byId(html, 'edited')).toBeNull();
    });
  });
});
