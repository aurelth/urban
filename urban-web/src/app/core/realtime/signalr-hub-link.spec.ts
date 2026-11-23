import { jest } from '@jest/globals';
import { SignalrModule, createSignalrHubLink } from './signalr-hub-link';

type Handler = (...args: unknown[]) => void;

/** Uma conexão do SignalR de mentira, com o que o teste quer ver: o que foi pedido e o que o servidor faria. */
class FakeConnection {
  readonly listeners = new Map<string, Handler>();
  reconnected: Handler = () => undefined;
  closed: Handler = () => undefined;
  started = 0;
  stopped = 0;

  on(method: string, handler: Handler): void {
    this.listeners.set(method, handler);
  }

  onreconnected(handler: Handler): void {
    this.reconnected = handler;
  }

  onclose(handler: Handler): void {
    this.closed = handler;
  }

  start(): Promise<void> {
    this.started += 1;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stopped += 1;
    return Promise.resolve();
  }
}

interface Built {
  readonly url: string;
  readonly options: {
    transport: number;
    skipNegotiation: boolean;
    accessTokenFactory: () => string;
  };
  readonly reconnect: boolean;
  readonly connection: FakeConnection;
}

function fakeModule(): { module: SignalrModule; built: Built[] } {
  const built: Built[] = [];
  class Builder {
    private url = '';
    private options: Built['options'] | null = null;
    private reconnect = false;

    withUrl(url: string, options: Built['options']): this {
      this.url = url;
      this.options = options;
      return this;
    }

    withAutomaticReconnect(): this {
      this.reconnect = true;
      return this;
    }

    build(): FakeConnection {
      const connection = new FakeConnection();
      built.push({
        url: this.url,
        options: this.options as Built['options'],
        reconnect: this.reconnect,
        connection,
      });
      return connection;
    }
  }
  const module = {
    HubConnectionBuilder: Builder,
    HttpTransportType: { None: 0, WebSockets: 1, ServerSentEvents: 2, LongPolling: 4 },
  } as unknown as SignalrModule;
  return { module, built };
}

describe('createSignalrHubLink', () => {
  it('só carrega o pacote quando a conexão é aberta', async () => {
    const { module } = fakeModule();
    const load = jest.fn(() => Promise.resolve(module));

    const link = createSignalrHubLink(load)('/hubs/announcements', () => 'token-1');
    expect(load).not.toHaveBeenCalled();

    await link.start();

    expect(load).toHaveBeenCalledTimes(1);
  });

  it('abre direto em WebSocket, sem negociar, com o token na conexão e reconexão automática', async () => {
    const { module, built } = fakeModule();
    let token = 'token-1';

    const link = createSignalrHubLink(() => Promise.resolve(module))(
      '/hubs/announcements',
      () => token,
    );
    await link.start();

    expect(built).toHaveLength(1);
    expect(built[0].url).toBe('/hubs/announcements');
    expect(built[0].options.transport).toBe(1);
    expect(built[0].options.skipNegotiation).toBe(true);
    expect(built[0].reconnect).toBe(true);
    expect(built[0].options.accessTokenFactory()).toBe('token-1');
    token = 'token-2';
    expect(built[0].options.accessTokenFactory()).toBe('token-2');
    expect(built[0].connection.started).toBe(1);
  });

  it('entrega o aviso do servidor a quem o pediu antes de abrir', async () => {
    const { module, built } = fakeModule();
    const received: unknown[] = [];
    const link = createSignalrHubLink(() => Promise.resolve(module))(
      '/hubs/announcements',
      () => 't',
    );
    link.on('announcementChanged', (message) => received.push(message));

    await link.start();
    built[0].connection.listeners.get('announcementChanged')?.({ kind: 'published', id: 'a' });

    expect(received).toEqual([{ kind: 'published', id: 'a' }]);
  });

  it('avisa quando a conexão volta sozinha e quando ela acaba', async () => {
    const { module, built } = fakeModule();
    const events: string[] = [];
    const link = createSignalrHubLink(() => Promise.resolve(module))(
      '/hubs/announcements',
      () => 't',
    );
    link.onReconnected(() => events.push('reconnected'));
    link.onClosed(() => events.push('closed'));

    await link.start();
    built[0].connection.reconnected();
    built[0].connection.closed(new Error('caiu'));

    expect(events).toEqual(['reconnected', 'closed']);
  });

  it('fecha a conexão aberta, e fechar sem ter aberto não faz nada', async () => {
    const { module, built } = fakeModule();
    const link = createSignalrHubLink(() => Promise.resolve(module))(
      '/hubs/announcements',
      () => 't',
    );

    await link.stop();
    await link.start();
    await link.stop();

    expect(built[0].connection.stopped).toBe(1);
  });

  it('abre uma conexão nova depois de fechar, com os mesmos pedidos de aviso', async () => {
    const { module, built } = fakeModule();
    const received: unknown[] = [];
    const link = createSignalrHubLink(() => Promise.resolve(module))(
      '/hubs/announcements',
      () => 't',
    );
    link.on('announcementChanged', (message) => received.push(message));

    await link.start();
    await link.stop();
    await link.start();
    built[1].connection.listeners.get('announcementChanged')?.({ kind: 'updated', id: 'b' });

    expect(built).toHaveLength(2);
    expect(received).toEqual([{ kind: 'updated', id: 'b' }]);
  });

  it('rejeita a abertura quando o pacote não carrega', async () => {
    const link = createSignalrHubLink(() => Promise.reject(new Error('sem rede')))(
      '/hubs/announcements',
      () => 't',
    );

    await expect(link.start()).rejects.toThrow('sem rede');
  });
});
