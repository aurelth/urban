import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { FormSubmit } from './form-submit';

function setup(inputs: Record<string, unknown> = {}): {
  fixture: ComponentFixture<FormSubmit>;
  html: HTMLElement;
} {
  TestBed.configureTestingModule({ providers: [provideUrbanIcons()] });
  const fixture = TestBed.createComponent(FormSubmit);
  fixture.componentRef.setInput('label', 'Salvar');
  fixture.componentRef.setInput('busyLabel', 'Salvando');
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  fixture.detectChanges();
  return { fixture, html: fixture.nativeElement };
}

describe('FormSubmit', () => {
  it('mostra um botao de envio habilitado, sem aviso de erro', () => {
    const { html } = setup();

    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.textContent?.trim()).toBe('Salvar');
    expect(button.disabled).toBe(false);
    expect(html.querySelector('[role="alert"]')).toBeNull();
  });

  it('troca o texto e desabilita o botao enquanto envia', () => {
    const { html } = setup({ busy: true });

    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.textContent?.trim()).toBe('Salvando');
    expect(button.disabled).toBe(true);
  });

  it('mostra o motivo do erro como alerta', () => {
    const { html } = setup({ error: 'Algo deu errado' });

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Algo deu errado');
  });

  it('empilha o aviso e o botao em coluna, com o botao na largura do formulario', () => {
    const { html } = setup();

    expect(html.classList.contains('flex')).toBe(true);
    expect(html.classList.contains('flex-col')).toBe(true);
  });

  it('desabilita o botao quando a tela diz que nao ha o que enviar', () => {
    const { html } = setup({ disabled: true });

    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Salvar');
  });
});
