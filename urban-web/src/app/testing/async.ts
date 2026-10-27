/** Espera as promessas pendentes terminarem (o HttpTestingController responde de forma síncrona). */
export function flushPromises(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve));
}
