import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { LinkProblemNotice } from './link-problem.notice';

function setup(): { fixture: ComponentFixture<LinkProblemNotice>; html: HTMLElement } {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideUrbanIcons()] });
  const fixture = TestBed.createComponent(LinkProblemNotice);
  fixture.componentRef.setInput('heading', 'Link inválido');
  fixture.componentRef.setInput('message', 'Link expirado. Peça um novo');
  fixture.componentRef.setInput('actionLabel', 'Pedir novo link');
  fixture.componentRef.setInput('actionLink', '/esqueci-a-senha');
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement };
}

describe('LinkProblemNotice', () => {
  it('mostra o titulo, o motivo como alerta e o caminho para pedir outro link', () => {
    const { html } = setup();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link inválido');
    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Link expirado');
    const action = html.querySelector('a[href="/esqueci-a-senha"]');
    expect(action?.textContent?.trim()).toBe('Pedir novo link');
  });
});
