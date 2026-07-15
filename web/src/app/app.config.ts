import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { routes } from './app.routes';
import { authInterceptor } from './auth/auth.interceptor';
import { AuthService } from './auth/auth.service';
import { VersionService } from './shared';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
    provideAnimations(), // ngx-charts (dashboard trend charts) relies on Angular animations
    // Resolve the cookie session before the app renders so guards + the shell see it (4.1).
    provideAppInitializer(() => inject(AuthService).loadSession()),
    // Load the running build's version for the footer chip + env badge (3.3). Non-blocking in
    // effect (failure is swallowed), but wired as an initializer so it kicks off at startup.
    provideAppInitializer(() => inject(VersionService).load()),
  ],
};
