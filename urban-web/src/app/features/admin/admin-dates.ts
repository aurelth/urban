const DAY = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/** "01/11/2026", sempre no dia de Brasília, para o administrador ler igual em qualquer computador. */
export function formatDay(iso: string): string {
  const moment = new Date(iso);
  return Number.isNaN(moment.getTime()) ? iso : DAY.format(moment);
}
