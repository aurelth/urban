import type { HubLinkFactory } from './hub-link';

/** O que a conexão usa do pacote do SignalR: as duas peças que montam a conexão. */
export type SignalrModule = Pick<
  typeof import('@microsoft/signalr'),
  'HubConnectionBuilder' | 'HttpTransportType'
>;

/** O pacote do SignalR só é baixado quando a primeira conexão é aberta: o pacote inicial do app não cresce. */
export function createSignalrHubLink(load: () => Promise<SignalrModule>): HubLinkFactory {
  void load;
  return () => {
    throw new Error('not implemented');
  };
}

export const signalrHubLink: HubLinkFactory = createSignalrHubLink(
  () => import('@microsoft/signalr'),
);
