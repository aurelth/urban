const TIME_ZONE = 'America/Sao_Paulo';

const DATE = new Intl.DateTimeFormat('pt-BR', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

const TIME = new Intl.DateTimeFormat('pt-BR', {
  timeZone: TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/** "13/11/2026 às 01:23", sempre na hora de Brasília, para o administrador ler igual em qualquer computador. */
export function formatRequestedAt(iso: string): string {
  const moment = new Date(iso);
  return Number.isNaN(moment.getTime()) ? iso : `${DATE.format(moment)} às ${TIME.format(moment)}`;
}
