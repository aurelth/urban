import type { HubConnection } from '@microsoft/signalr';
import type { HubLink, HubLinkFactory } from './hub-link';

/** O que a conexão usa do pacote do SignalR: as duas peças que montam a conexão. */
export type SignalrModule = Pick<
  typeof import('@microsoft/signalr'),
  'HubConnectionBuilder' | 'HttpTransportType'
>;

/**
 * O pacote do SignalR só é baixado quando a primeira conexão é aberta: o pacote inicial do app não cresce. A conexão abre direto em
 * WebSocket, sem negociar (a API roda numa instância só e o nginx já encaminha o upgrade): o token vai na URL, o único jeito de o
 * navegador autenticar um WebSocket, e a API só o lê nas rotas dos hubs. A cada reconexão o SignalR pede o token de novo, então o
 * renovado vale.
 */
export function createSignalrHubLink(load: () => Promise<SignalrModule>): HubLinkFactory {
  return (path, accessToken) => new SignalrHubLink(load, path, accessToken);
}

class SignalrHubLink implements HubLink {
  private connection: HubConnection | null = null;
  private readonly listeners = new Map<string, (message: unknown) => void>();
  private reconnectedHandler: () => void = () => undefined;
  private closedHandler: () => void = () => undefined;

  constructor(
    private readonly load: () => Promise<SignalrModule>,
    private readonly path: string,
    private readonly accessToken: () => string,
  ) {}

  on(method: string, handler: (message: unknown) => void): void {
    this.listeners.set(method, handler);
  }

  onReconnected(handler: () => void): void {
    this.reconnectedHandler = handler;
  }

  onClosed(handler: () => void): void {
    this.closedHandler = handler;
  }

  async start(): Promise<void> {
    const { HubConnectionBuilder, HttpTransportType } = await this.load();
    const connection = new HubConnectionBuilder()
      .withUrl(this.path, {
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
        accessTokenFactory: () => this.accessToken(),
      })
      .withAutomaticReconnect()
      .build();

    for (const [method, handler] of this.listeners) {
      connection.on(method, (message: unknown) => handler(message));
    }
    connection.onreconnected(() => this.reconnectedHandler());
    connection.onclose(() => this.closedHandler());

    this.connection = connection;
    try {
      await connection.start();
    } catch (error) {
      this.connection = null;
      throw error;
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    await connection?.stop();
  }
}

export const signalrHubLink: HubLinkFactory = createSignalrHubLink(
  () => import('@microsoft/signalr'),
);
