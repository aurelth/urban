export interface MenuSelectOption {
  value: string;
  label: string;
  checked: boolean;
}

export function isMenuSelect(element: Element | null | undefined): element is HTMLElement {
  return element?.tagName.toLowerCase() === 'urban-menu-select';
}

function trigger(host: HTMLElement): HTMLButtonElement {
  return host.querySelector<HTMLButtonElement>('button')!;
}

/** Itens do menu aberto por último: os menus já fechados continuam no documento, então só o último vale. */
function menuItems(): HTMLElement[] {
  const panels = document.querySelectorAll('.mat-mdc-menu-panel');
  return [
    ...(panels.item(panels.length - 1)?.querySelectorAll<HTMLElement>('[role="menuitemradio"]') ??
      []),
  ];
}

function closeMenu(): void {
  document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
}

/** Valor escolhido num `urban-menu-select`, como o componente o guarda (o rótulo pode ser outro texto). */
export function menuSelectValue(host: HTMLElement): string {
  return trigger(host).dataset['value'] ?? '';
}

/** Texto que o `urban-menu-select` mostra no botão. */
export function menuSelectLabel(host: HTMLElement): string {
  return (trigger(host).textContent ?? '').replace(/\s+/g, ' ').trim();
}

/** Abre o menu, lê as opções e o fecha de novo. */
export function menuSelectOptions(host: HTMLElement): MenuSelectOption[] {
  trigger(host).click();
  const options = menuItems().map((item) => ({
    value: item.dataset['value'] ?? '',
    label: (item.textContent ?? '').replace(/\s+/g, ' ').trim(),
    checked: item.getAttribute('aria-checked') === 'true',
  }));
  closeMenu();
  return options;
}

/** Abre o menu e clica na opção de valor `value`, como a pessoa faria. */
export function chooseMenuOption(host: HTMLElement, value: string): void {
  trigger(host).click();
  const item = menuItems().find((candidate) => candidate.dataset['value'] === value);
  if (item === undefined) {
    closeMenu();
    throw new Error(`O menu não tem a opção "${value}"`);
  }
  item.click();
}

/** Preenche um campo de texto, uma lista nativa ou um `urban-menu-select` pelo valor. */
export function setFieldValue(element: HTMLElement, value: string): void {
  if (isMenuSelect(element)) {
    chooseMenuOption(element, value);
    return;
  }
  const field = element as HTMLInputElement | HTMLSelectElement;
  field.value = value;
  field.dispatchEvent(new Event(field instanceof HTMLSelectElement ? 'change' : 'input'));
}

/** Valor de um campo de texto, de uma lista nativa ou de um `urban-menu-select`. */
export function fieldValueOf(element: HTMLElement): string {
  return isMenuSelect(element)
    ? menuSelectValue(element)
    : (element as HTMLInputElement | HTMLSelectElement).value;
}
