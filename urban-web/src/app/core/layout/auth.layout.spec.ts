import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthLayout } from './auth.layout';

describe('AuthLayout', () => {
  afterEach(() => TestBed.inject(HttpTestingController).match(() => true));

  it('leva a marca para a entrada, a página inicial de quem ainda não entrou', async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(AuthLayout);
    await fixture.whenStable();

    const logo = (fixture.nativeElement as HTMLElement).querySelector('urban-brand-link a');

    expect(logo?.getAttribute('href')).toBe('/entrar');
    expect(logo?.textContent?.trim()).toBe('Urban');
  });
});
