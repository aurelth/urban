import { Component, computed, effect, inject } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatToolbar } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { accountLabel } from '../auth/account-label';
import { roleLabel } from '../auth/role-label';
import { SessionStore } from '../auth/session.store';
import { ThemeStore } from '../services/theme.store';

@Component({
  selector: 'urban-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbar,
    MatButton,
    MatIconButton,
    MatIcon,
  ],
  templateUrl: './shell.layout.html',
})
export class ShellLayout {
  protected readonly theme = inject(ThemeStore);
  protected readonly session = inject(SessionStore);
  protected readonly accountLabel = accountLabel;
  protected readonly roleLabel = roleLabel;

  /** "Meus dados" é do síndico que já cadastrou o condomínio; "Minha conta" é de todos. */
  protected readonly showProfile = computed(() => {
    const user = this.session.user();
    return user?.role === 'Manager' && user.condominiumId !== null;
  });

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
