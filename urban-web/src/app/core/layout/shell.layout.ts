import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, effect, inject, linkedSignal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatSidenav, MatSidenavContainer, MatSidenavContent } from '@angular/material/sidenav';
import { MatTooltip } from '@angular/material/tooltip';
import { MatToolbar } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { accountLabel } from '../auth/account-label';
import { initialsOf } from '../auth/initials';
import { roleLabel } from '../auth/role-label';
import { SessionStore } from '../auth/session.store';
import { BrandLink } from '../../shared/ui/brand-link/brand.link';
import { ThemeStore } from '../services/theme.store';
import { navGroupsFor, userMenuFor } from './nav-items';
import { SidebarStore } from './sidebar.store';

/** Largura (breakpoint `lg` do Tailwind) a partir da qual o menu lateral fica fixo; abaixo dela vira uma gaveta. */
const DESKTOP_QUERY = '(min-width: 1024px)';

/** Largura do menu lateral fixado (com os nomes) e recolhido (só ícones: 48 px do item + 12 px de cada lado). */
const PINNED_WIDTH = '256px';
const RAIL_WIDTH = '72px';

/**
 * Moldura da área logada: menu lateral com as telas de gestão (fixo no computador, gaveta no celular), cabeçalho
 * com o tema, a saudação e o avatar (que abre "Meus dados", "Minha conta" e "Sair") e o conteúdo da tela.
 * Quais itens aparecem vem de `nav-items.ts`; aqui fica só como eles se comportam na tela.
 */
@Component({
  selector: 'urban-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavContainer,
    MatSidenav,
    MatSidenavContent,
    MatToolbar,
    MatIconButton,
    MatIcon,
    MatMenu,
    MatMenuItem,
    MatMenuTrigger,
    MatTooltip,
    BrandLink,
  ],
  templateUrl: './shell.layout.html',
})
export class ShellLayout {
  protected readonly theme = inject(ThemeStore);
  protected readonly session = inject(SessionStore);
  protected readonly accountLabel = accountLabel;
  protected readonly initialsOf = initialsOf;
  protected readonly roleLabel = roleLabel;

  protected readonly groups = computed(() => navGroupsFor(this.session.user()));
  protected readonly userMenuItems = computed(() => userMenuFor(this.session.user()));
  protected readonly hasSidebar = computed(() => this.groups().length > 0);

  private readonly breakpoints = inject(BreakpointObserver);

  /** Sem `matchMedia` (testes em jsdom) conta como tela estreita: a gaveta começa fechada. */
  protected readonly isDesktop = toSignal(
    this.breakpoints.observe(DESKTOP_QUERY).pipe(map((state) => state.matches)),
    { initialValue: this.breakpoints.isMatched(DESKTOP_QUERY) },
  );
  protected readonly mode = computed(() => (this.isDesktop() ? 'side' : 'over'));

  /** No computador o menu nasce aberto; no celular, fechado. A pessoa pode alternar, e trocar o tamanho da tela refaz o padrão. */
  protected readonly opened = linkedSignal(() => this.isDesktop());

  private readonly sidebar = inject(SidebarStore);

  /** Recolhido só vale no computador; a gaveta do celular sempre mostra os nomes. */
  protected readonly rail = computed(() => this.isDesktop() && this.sidebar.collapsed());
  protected readonly sidebarWidth = computed(() => (this.rail() ? RAIL_WIDTH : PINNED_WIDTH));

  /** O mesmo botão do cabeçalho: no computador recolhe/fixa o menu, no celular abre/fecha a gaveta. */
  protected readonly menuToggleLabel = computed(() => {
    if (this.isDesktop()) {
      return this.rail() ? 'Fixar menu' : 'Recolher menu';
    }
    return this.opened() ? 'Fechar menu' : 'Abrir menu';
  });
  protected readonly menuExpanded = computed(() =>
    this.isDesktop() ? !this.rail() : this.opened(),
  );

  private readonly router = inject(Router);

  constructor() {
    // Sessão que caiu sozinha (renovação recusada): volta para a entrada e guarda onde a pessoa estava.
    effect(() => {
      if (this.session.status() === 'anonymous' && this.session.expired()) {
        void this.router.navigate(['/entrar'], { queryParams: { returnUrl: this.router.url } });
      }
    });
  }

  protected toggleMenu(): void {
    if (this.isDesktop()) {
      this.sidebar.toggle();
    } else {
      this.opened.update((opened) => !opened);
    }
  }

  /** Na gaveta, escolher uma tela a fecha (ela cobre o conteúdo); no menu fixo, ele continua onde está. */
  protected closeDrawer(): void {
    if (!this.isDesktop()) {
      this.opened.set(false);
    }
  }

  protected async signOut(): Promise<void> {
    await this.session.signOut();
    await this.router.navigateByUrl('/entrar');
  }
}
