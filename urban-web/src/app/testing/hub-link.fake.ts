import { HubLink, HubLinkFactory } from '../core/realtime/hub-link';

/** Uma conexão de mentira: o teste decide quando o servidor avisa, quando a conexão cai e volta e quando ela não abre. */
export class FakeHubLink implements HubLink {
  readonly handlers = new Map<string, (message: unknown) => void>();
  reconnected: () => void = () => undefined;
  closed: () => void = () => undefined;
  starts = 0;
  stops = 0;
  /** Quantas aberturas seguintes vão falhar. */
  failStarts = 0;

  constructor(
    readonly path: string,
    readonly accessToken: () => string,
  ) {}

  start(): Promise<void> {
    this.starts += 1;
    if (this.failStarts > 0) {
      this.failStarts -= 1;
      return Promise.reject(new Error('conexão recusada'));
    }
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stops += 1;
    return Promise.resolve();
  }

  on(method: string, handler: (message: unknown) => void): void {
    this.handlers.set(method, handler);
  }

  onReconnected(handler: () => void): void {
    this.reconnected = handler;
  }

  onClosed(handler: () => void): void {
    this.closed = handler;
  }

  /** O servidor avisa. */
  emit(method: string, message: unknown): void {
    this.handlers.get(method)?.(message);
  }
}

/** A fábrica de conexões de mentira: guarda cada conexão montada, na ordem. `failFirstStarts` faz as primeiras aberturas da primeira conexão falharem. */
export function fakeHubLinks(failFirstStarts = 0): {
  factory: HubLinkFactory;
  links: FakeHubLink[];
} {
  const links: FakeHubLink[] = [];
  const factory: HubLinkFactory = (path, accessToken) => {
    const link = new FakeHubLink(path, accessToken);
    if (links.length === 0) {
      link.failStarts = failFirstStarts;
    }
    links.push(link);
    return link;
  };
  return { factory, links };
}
