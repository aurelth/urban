/**
 * O servidor de tempo real de mentira. O E2E do CI sobe só o front, sem API, e o app abre uma conexão WebSocket com `/hubs/...`.
 * Aqui o `WebSocket` da página é trocado por um que fala o protocolo JSON do SignalR (cada mensagem termina em 0x1e): responde ao
 * aperto de mão e deixa o teste empurrar avisos do servidor. Conexões para outros endereços (o recarregamento do servidor de
 * desenvolvimento, por exemplo) seguem no WebSocket de verdade.
 */
const RECORD_SEPARATOR = '\u001e';
const INVOCATION = 1;

interface FakeSocket {
  onopen: ((event: unknown) => void) | null;
  onmessage: ((event: { data: string }) => void) | null;
  onclose: ((event: unknown) => void) | null;
  onerror: ((event: unknown) => void) | null;
  readyState: number;
  binaryType: string;
  url: string;
  send: (data: string) => void;
  close: () => void;
}

type HubWindow = Cypress.AUTWindow & { __urbanHubSockets?: FakeSocket[] };

function fakeSocket(url: string): FakeSocket {
  const socket: FakeSocket = {
    onopen: null,
    onmessage: null,
    onclose: null,
    onerror: null,
    readyState: 0,
    binaryType: 'arraybuffer',
    url,
    send: (data) => {
      if (data.includes('"protocol"')) {
        setTimeout(() => socket.onmessage?.({ data: `{}${RECORD_SEPARATOR}` }));
      }
    },
    close: () => {
      socket.readyState = 3;
      socket.onclose?.({ code: 1000, reason: '', wasClean: true });
    },
  };
  setTimeout(() => {
    socket.readyState = 1;
    socket.onopen?.({});
  });
  return socket;
}

/** Instala o WebSocket de mentira antes de a página carregar: use em `cy.visit(path, { onBeforeLoad: installFakeHub })`. */
export function installFakeHub(win: Cypress.AUTWindow): void {
  const hubWindow = win as HubWindow;
  const RealWebSocket = win.WebSocket;
  hubWindow.__urbanHubSockets = [];
  const Wrapper = function (url: string | URL, protocols?: string | string[]): WebSocket {
    if (String(url).includes('/hubs/')) {
      const socket = fakeSocket(String(url));
      hubWindow.__urbanHubSockets?.push(socket);
      return socket as unknown as WebSocket;
    }
    return new RealWebSocket(url, protocols);
  } as unknown as typeof WebSocket;
  Object.assign(Wrapper, { CONNECTING: 0, OPEN: 1, CLOSING: 2, CLOSED: 3 });
  win.WebSocket = Wrapper;
}

/** Visita a página com o servidor de tempo real de mentira ligado. */
export function visitWithFakeHub(path: string): void {
  cy.visit(path, { onBeforeLoad: installFakeHub });
}

/** Espera a conexão do mural abrir (o app a abre depois de entrar). */
export function hubConnected(): void {
  cy.window().should((win) => {
    const sockets = (win as HubWindow).__urbanHubSockets ?? [];
    expect(sockets.length, 'conexões com o hub').to.be.greaterThan(0);
    expect(sockets[sockets.length - 1].readyState, 'conexão aberta').to.eq(1);
  });
}

/** O servidor avisa, pela conexão aberta, que um comunicado foi publicado, editado ou apagado. */
export function pushAnnouncementChange(
  kind: 'published' | 'updated' | 'removed',
  id: string,
): void {
  cy.window().then((win) => {
    const sockets = (win as HubWindow).__urbanHubSockets ?? [];
    const socket = sockets[sockets.length - 1];
    const message = {
      type: INVOCATION,
      target: 'announcementChanged',
      arguments: [{ kind, id }],
    };
    socket.onmessage?.({ data: JSON.stringify(message) + RECORD_SEPARATOR });
  });
}
