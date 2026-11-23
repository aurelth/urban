import { InjectionToken } from '@angular/core';
import { signalrHubLink } from './signalr-hub-link';

/**
 * A conexão com um hub do servidor, no tamanho do que a tela precisa. Existe para o resto do app (e os testes) não conhecerem o
 * SignalR: quem quiser outro transporte, ou um de mentira, troca o `HUB_LINK`.
 */
export interface HubLink {
  /** Abre a conexão. Falha (rede, token recusado) rejeita a promessa. */
  start(): Promise<void>;
  stop(): Promise<void>;
  /** Escuta um aviso do servidor. Vale para as conexões abertas depois e deve ser pedido antes do `start`. */
  on(method: string, handler: (message: unknown) => void): void;
  /** A conexão caiu e voltou sozinha: pode ter havido avisos no meio. */
  onReconnected(handler: () => void): void;
  /** A conexão acabou e não volta sozinha (o servidor recusou ou as tentativas esgotaram), ou foi fechada por `stop`. */
  onClosed(handler: () => void): void;
}

/** Monta a conexão com o hub em `path`; `accessToken` é lido a cada conexão, então a renovada vale na reconexão. */
export type HubLinkFactory = (path: string, accessToken: () => string) => HubLink;

export const HUB_LINK = new InjectionToken<HubLinkFactory>('HUB_LINK', {
  providedIn: 'root',
  factory: () => signalrHubLink,
});
