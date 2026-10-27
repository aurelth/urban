import { Component, effect, inject } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatToolbar } from '@angular/material/toolbar';
import { Router, RouterOutlet } from '@angular/router';
import { roleLabel } from '../auth/role-label';
import { SessionStore } from '../auth/session.store';
import { ThemeStore } from '../services/theme.store';

@Component({
  selector: 'urban-shell',
  imports: [RouterOutlet, MatToolbar, MatButton, MatIconButton, MatIcon],
  templateUrl: './shell.layout.html',
})
export class ShellLayout {
  protected readonly theme = inject(ThemeStore);
  protected readonly session = inject(SessionStore);
  protected readonly roleLabel = roleLabel;

  private readonly router = inject(Router);

  constructor() {
    // Sessão que caiu sozinha (renovação recusada): volta para a entrada e guarda onde a pessoa estava.
    effect(() => {
      if (this.session.status() === 'anonymous' && this.session.expired()) {
        void this.router.navigate(['/entrar'], { queryParams: { returnUrl: this.router.url } });
      }
    });
  }

  protected async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/entrar');
  }
}
