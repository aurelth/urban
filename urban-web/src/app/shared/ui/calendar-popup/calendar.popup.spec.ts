import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OverlayContainer } from '@angular/cdk/overlay';
import { flushPromises } from '../../../testing/async';
import { CalendarPopup } from './calendar.popup';

@Component({
  imports: [CalendarPopup],
  template: `
    <urban-calendar-popup
      [value]="value()"
      [birthDate]="birthDate()"
      (picked)="picked.push($event)"
      (closed)="closedCount = closedCount + 1"
    />
  `,
})
class Host {
  readonly value = signal<Date | null>(null);
  readonly birthDate = signal(false);
  readonly picked: Date[] = [];
  closedCount = 0;
}

async function open(
  setup: (host: Host) => void = () => undefined,
): Promise<{ fixture: ComponentFixture<Host>; overlay: HTMLElement }> {
  const fixture = TestBed.createComponent(Host);
  setup(fixture.componentInstance);
  fixture.detectChanges();
  await fixture.whenStable();
  await flushPromises();
  fixture.detectChanges();
  return { fixture, overlay: TestBed.inject(OverlayContainer).getContainerElement() };
}

function cells(overlay: HTMLElement): HTMLElement[] {
  return Array.from(overlay.querySelectorAll<HTMLElement>('.mat-calendar-body-cell'));
}

describe('CalendarPopup', () => {
  it('abre o calendário sozinho, assim que é criado', async () => {
    const { overlay } = await open();

    expect(overlay.querySelector('mat-calendar')).not.toBeNull();
  });

  it('abre no mês da data atual do campo e marca o dia escolhido', async () => {
    const { overlay } = await open((host) => host.value.set(new Date(2024, 2, 5)));

    expect(overlay.querySelector('.mat-calendar-period-button')?.textContent).toContain('2024');
    const selected = overlay.querySelector('.mat-calendar-body-selected');
    expect(selected?.textContent?.trim()).toBe('5');
  });

  it('fala português: nomes do mês, dias da semana e rótulos de acessibilidade', async () => {
    const { overlay } = await open((host) => host.value.set(new Date(2024, 2, 5)));

    expect(
      overlay.querySelector('.mat-calendar-period-button')?.textContent?.toLowerCase(),
    ).toContain('mar');
    expect(overlay.querySelector('button[aria-label="Mês anterior"]')).not.toBeNull();
    expect(overlay.querySelector('button[aria-label="Próximo mês"]')).not.toBeNull();
    expect(
      cells(overlay)
        .find((cell) => cell.textContent?.trim() === '5')
        ?.getAttribute('aria-label')
        ?.toLowerCase(),
    ).toContain('março');
  });

  it('semana começa no domingo', async () => {
    const { overlay } = await open((host) => host.value.set(new Date(2024, 2, 5)));

    const first = overlay.querySelector('.mat-calendar-table-header th');
    expect(first?.textContent?.toLowerCase()).toContain('domingo');
  });

  it('escolher um dia devolve aquela data e fecha o calendário', async () => {
    const { fixture, overlay } = await open((host) => host.value.set(new Date(2024, 2, 5)));

    cells(overlay)
      .find((cell) => cell.textContent?.trim() === '20')!
      .click();
    fixture.detectChanges();
    await fixture.whenStable();

    const [date] = fixture.componentInstance.picked;
    expect([date.getFullYear(), date.getMonth(), date.getDate()]).toEqual([2024, 2, 20]);
    expect(fixture.componentInstance.closedCount).toBe(1);
  });

  it('fechar com Esc avisa que fechou, sem escolher data', async () => {
    const { fixture } = await open();

    document.body.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }),
    );
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.picked).toEqual([]);
    expect(fixture.componentInstance.closedCount).toBe(1);
  });

  it('para data de nascimento abre na lista de anos, uns 30 anos atrás quando o campo está vazio', async () => {
    const { overlay } = await open((host) => host.birthDate.set(true));

    expect(overlay.querySelector('mat-multi-year-view')).not.toBeNull();
    const years = cells(overlay).map((cell) => Number(cell.textContent?.trim()));
    const expected = new Date().getFullYear() - 30;
    expect(years).toContain(expected);
  });

  it('para data de nascimento com data já digitada abre na lista de anos daquela data', async () => {
    const { overlay } = await open((host) => {
      host.birthDate.set(true);
      host.value.set(new Date(1985, 5, 10));
    });

    expect(overlay.querySelector('mat-multi-year-view')).not.toBeNull();
    expect(cells(overlay).map((cell) => cell.textContent?.trim())).toContain('1985');
  });
});
