import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { provideInactivityLogout } from './core/auth/inactivity.monitor';
import { provideSessionRestore } from './core/auth/session.providers';
import { providePageTitle } from './core/services/page-title.strategy';
import { provideTheme } from './core/services/theme.providers';
import { provideUrbanIcons } from './core/theme/icons';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    providePageTitle(),
    provideUrbanIcons(),
    provideTheme(),
    provideSessionRestore(),
    provideInactivityLogout(),
  ],
};
