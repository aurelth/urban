import { FormControl, FormGroup, Validators } from '@angular/forms';
import { focusFirstInvalid, focusFirstInvalidInput } from './focus-first-invalid';

function field(tag: string, name: string): HTMLElement {
  const element = document.createElement(tag);
  element.setAttribute('formControlName', name);
  return element;
}

function buildHost(): HTMLElement {
  const host = document.createElement('div');
  const checkbox = field('mat-checkbox', 'accepted');
  const box = document.createElement('input');
  box.type = 'checkbox';
  checkbox.append(box);
  host.append(field('input', 'email'), field('input', 'password'), checkbox);
  document.body.append(host);
  return host;
}

function buildForm(email: string, password: string, accepted: boolean) {
  return new FormGroup({
    email: new FormControl(email, { nonNullable: true, validators: Validators.required }),
    password: new FormControl(password, { nonNullable: true, validators: Validators.required }),
    accepted: new FormControl(accepted, { nonNullable: true, validators: Validators.requiredTrue }),
  });
}

describe('focusFirstInvalid', () => {
  let host: HTMLElement;

  beforeEach(() => {
    host = buildHost();
  });

  afterEach(() => {
    host.remove();
  });

  it('foca o primeiro campo inválido', () => {
    focusFirstInvalid(host, buildForm('', '', false));

    expect(document.activeElement).toBe(host.querySelector('[formControlName="email"]'));
  });

  it('pula os campos válidos', () => {
    focusFirstInvalid(host, buildForm('a@b.com', '', false));

    expect(document.activeElement).toBe(host.querySelector('[formControlName="password"]'));
  });

  it('foca o campo de dentro do componente, como o checkbox', () => {
    focusFirstInvalid(host, buildForm('a@b.com', 'Senha-segura-123', false));

    expect(document.activeElement).toBe(host.querySelector('mat-checkbox input'));
  });

  it('não muda o foco quando tudo é válido', () => {
    focusFirstInvalid(host, buildForm('a@b.com', 'Senha-segura-123', true));

    expect(document.activeElement).toBe(document.body);
  });
});

describe('focusFirstInvalidInput', () => {
  let host: HTMLElement;

  beforeEach(() => {
    host = document.createElement('form');
    host.className = 'ng-invalid';
    document.body.append(host);
  });

  afterEach(() => {
    host.remove();
  });

  function add(tag: string, classes: string): HTMLElement {
    const element = document.createElement(tag);
    element.className = classes;
    host.append(element);
    return element;
  }

  it('foca o primeiro campo marcado como inválido, na ordem da tela', () => {
    add('input', 'ng-valid');
    const first = add('input', 'ng-invalid');
    add('input', 'ng-invalid');

    focusFirstInvalidInput(host);

    expect(document.activeElement).toBe(first);
  });

  it('alcança listas de escolha', () => {
    add('input', 'ng-valid');
    const select = add('select', 'ng-invalid');
    add('textarea', 'ng-invalid');

    focusFirstInvalidInput(host);

    expect(document.activeElement).toBe(select);
  });

  it('alcança áreas de texto', () => {
    add('input', 'ng-valid');
    add('select', 'ng-valid');
    const area = add('textarea', 'ng-invalid');

    focusFirstInvalidInput(host);

    expect(document.activeElement).toBe(area);
  });

  it('ignora o formulário e os grupos, que também ficam inválidos', () => {
    const group = add('div', 'ng-invalid');
    const input = document.createElement('input');
    input.className = 'ng-invalid';
    group.append(input);

    focusFirstInvalidInput(host);

    expect(document.activeElement).toBe(input);
  });

  it('não faz nada quando tudo é válido', () => {
    add('input', 'ng-valid');
    const before = document.activeElement;

    focusFirstInvalidInput(host);

    expect(document.activeElement).toBe(before);
  });
});
