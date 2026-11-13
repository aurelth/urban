import {
  DESKTOP,
  PHONE,
  chooseUserMenu,
  expectText,
  openDrawer,
  openUserMenu,
} from '../support/navegacao';
import { stubProfile } from '../support/perfil';
import { NEW_MANAGER } from '../support/condominio';
import { MANAGER, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';

const LINKS = [
  ['nav-dashboard', 'Painel', '/dashboard'],
  ['nav-units', 'Unidades', '/unidades'],
  ['nav-expenses', 'Despesas', '/financeiro/despesas'],
  ['nav-tariffs', 'Tarifas', '/financeiro/tarifas'],
  ['nav-readings', 'Leituras', '/financeiro/leituras'],
  ['nav-entries', 'Lançamentos', '/financeiro/lancamentos'],
] as const;

function widthOf($element: JQuery<HTMLElement>): number {
  return Math.round($element[0].getBoundingClientRect().width);
}

function expectNoHorizontalScroll(): void {
  cy.document().should((doc) => {
    expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
  });
}

describe('Navegação', () => {
  beforeEach(() => {
    stubSignedIn();
    stubUnits();
    stubProfile();
  });

  describe('no computador', () => {
    beforeEach(() => {
      cy.viewport(DESKTOP.width, DESKTOP.height);
    });

    it('mostra o menu lateral fixo, com as telas agrupadas, e o botão de recolher', () => {
      cy.visit('/dashboard');

      cy.get('nav[aria-label="Menu principal"]').should('be.visible');
      cy.get('nav[aria-label="Menu principal"] a').then(($links) => {
        expect([...$links].map((link) => link.dataset['testid'])).to.deep.eq(
          LINKS.map(([id]) => id),
        );
        expect([...$links].map((link) => link.textContent?.trim())).to.deep.eq(
          LINKS.map(([, label]) => label),
        );
      });
      cy.get('[data-testid="nav-group"]').then(($groups) => {
        expect([...$groups].map((group) => group.textContent?.trim())).to.deep.eq([
          'Condomínio',
          'Financeiro',
        ]);
      });
      cy.get('[data-testid="menu-toggle"]')
        .should('be.visible')
        .and('have.attr', 'aria-label', 'Recolher menu')
        .and('have.attr', 'aria-expanded', 'true');
      cy.get('mat-sidenav').should(($menu) => expect(widthOf($menu)).to.eq(256));
    });

    it('leva cada item à sua tela e mantém o menu lateral na tela', () => {
      cy.visit('/dashboard');

      cy.get('[data-testid="nav-units"]').click();

      cy.location('pathname').should('eq', '/unidades');
      cy.contains('h1', 'Unidades').should('be.visible');
      cy.get('nav[aria-label="Menu principal"]').should('be.visible');
    });

    it('marca a tela aberta, e só ela, com destaque e aria-current', () => {
      cy.visit('/unidades');

      cy.get('[data-testid="nav-units"]')
        .should('have.attr', 'aria-current', 'page')
        .and('have.css', 'font-weight', '500');
      cy.get('nav[aria-label="Menu principal"] a[aria-current="page"]').should('have.length', 1);
      cy.get('[data-testid="nav-dashboard"]').should('not.have.attr', 'aria-current');
    });

    it('deixa a logo, com o U colorido do favicon, no topo do menu lateral, e não duplica no cabeçalho', () => {
      cy.visit('/dashboard');

      cy.get('mat-sidenav a[aria-label="Urban, ir para a página inicial"]')
        .should('be.visible')
        .and('contain.text', 'Urban');
      cy.get('mat-sidenav [data-testid="brand-mark"]').should('be.visible');
      cy.get('mat-sidenav [data-testid="brand-mark"] rect').should(
        'have.css',
        'fill',
        'rgb(88, 86, 214)',
      );
      cy.get('mat-sidenav [data-testid="brand-mark"] path').should(
        'have.css',
        'stroke',
        'rgb(255, 255, 255)',
      );
      cy.get('mat-toolbar a[aria-label="Urban, ir para a página inicial"]').should('not.exist');
    });

    it('não separa o cabeçalho do corpo com uma linha', () => {
      cy.visit('/dashboard');

      cy.get('mat-toolbar').should('have.css', 'border-bottom-width', '0px');
    });

    describe('recolher e fixar o menu', () => {
      const MENU = 'mat-sidenav';
      const TOGGLE = '[data-testid="menu-toggle"]';

      function collapse(): void {
        cy.get(TOGGLE).click();
        cy.get(MENU).should(($menu) => expect(widthOf($menu)).to.eq(72));
      }

      it('recolhe numa coluna só de ícones e a página ocupa o espaço que sobrou', () => {
        cy.visit('/dashboard');

        collapse();

        cy.get(TOGGLE)
          .should('have.attr', 'aria-label', 'Fixar menu')
          .and('have.attr', 'aria-expanded', 'false');
        cy.get('mat-sidenav-content').should('have.css', 'margin-left', '72px');
        cy.get('[data-testid="nav-label"]').each(($label) => {
          expect(
            $label[0].getBoundingClientRect().width,
            'nome só para leitor de tela',
          ).to.be.at.most(1);
        });
        cy.get('[data-testid="nav-group"]').should('not.exist');
        cy.get('nav[aria-label="Menu principal"] a mat-icon').should('have.length', 6);
        cy.get('nav[aria-label="Menu principal"] a mat-icon').each(($icon) => {
          cy.wrap($icon).should('be.visible');
        });
        cy.get('[data-testid="user-menu"]').should('be.visible');
        expectNoHorizontalScroll();
      });

      it('mostra só o U da logo quando recolhido, e a logo continua levando ao painel', () => {
        cy.visit('/conta');

        collapse();

        cy.get('mat-sidenav [data-testid="brand-mark"]').should('be.visible');
        cy.get('mat-sidenav a[aria-label="Urban, ir para a página inicial"]')
          .should(($logo) => expect($logo.text().trim()).to.eq(''))
          .click();
        cy.location('pathname').should('eq', '/dashboard');
      });

      it('mostra o nome do item numa dica ao passar o mouse', () => {
        cy.visit('/dashboard');
        collapse();

        cy.get('[data-testid="nav-units"]').trigger('mouseenter');

        cy.get('.mat-mdc-tooltip').should('be.visible').and('contain.text', 'Unidades');
      });

      it('continua levando às telas, e fica recolhido ao navegar', () => {
        cy.visit('/dashboard');
        collapse();

        cy.get('[data-testid="nav-units"]').click();

        cy.location('pathname').should('eq', '/unidades');
        cy.get(MENU).should(($menu) => expect(widthOf($menu)).to.eq(72));
        cy.get('[data-testid="nav-units"]').should('have.attr', 'aria-current', 'page');
      });

      it('fixa de novo, voltando os nomes e os grupos', () => {
        cy.visit('/dashboard');
        collapse();

        cy.get(TOGGLE).click();

        cy.get(MENU).should(($menu) => expect(widthOf($menu)).to.eq(256));
        cy.get(TOGGLE).should('have.attr', 'aria-label', 'Recolher menu');
        cy.get('[data-testid="nav-label"]').first().should('be.visible').and('have.text', 'Painel');
        cy.get('[data-testid="nav-group"]').should('have.length', 2);
      });

      it('lembra a escolha depois de recarregar a página', () => {
        cy.visit('/dashboard');
        collapse();

        cy.reload();

        cy.get(MENU).should(($menu) => expect(widthOf($menu)).to.eq(72));
        cy.get(TOGGLE).should('have.attr', 'aria-label', 'Fixar menu');
      });

      it('não atrapalha o celular: a gaveta sempre abre com os nomes', () => {
        cy.viewport(PHONE.width, PHONE.height);
        cy.visit('/dashboard', {
          onBeforeLoad: (win) => win.localStorage.setItem('urban.sidebar', 'collapsed'),
        });

        openDrawer();

        cy.get(MENU).should(($menu) => expect(widthOf($menu)).to.eq(256));
        cy.get('[data-testid="nav-label"]').first().should('be.visible').and('have.text', 'Painel');
        cy.get('[data-testid="nav-group"]').should('have.length', 2);
      });
    });

    it('mostra a saudação ao lado do avatar, nessa ordem: tema, saudação, avatar', () => {
      cy.visit('/dashboard');

      expectText('[data-testid="current-user"]', 'Olá Marina - Síndico');
      expectText('[data-testid="user-menu"]', 'M');
      cy.get('[data-testid="user-menu"]').should('be.visible');
      cy.get('[data-testid="current-user"]').then(($greeting) => {
        cy.get('[data-testid="user-menu"]').then(($avatar) => {
          const greeting = $greeting[0].getBoundingClientRect();
          const avatar = $avatar[0].getBoundingClientRect();
          expect(greeting.right, 'a saudação vem antes do avatar').to.be.at.most(avatar.left);
          expect(avatar.left - greeting.right, 'e bem ao lado dele').to.be.at.most(16);
        });
      });
      cy.get('button[aria-label="Usar tema escuro"]').then(($theme) => {
        cy.get('[data-testid="current-user"]').then(($greeting) => {
          expect($theme[0].getBoundingClientRect().right).to.be.at.most(
            $greeting[0].getBoundingClientRect().left,
          );
        });
      });
    });

    it('deixa o avatar redondo, com a cor de destaque', () => {
      cy.visit('/dashboard');

      cy.get('[data-testid="user-menu"]')
        .should('have.css', 'background-color', 'rgb(88, 86, 214)')
        .and('have.css', 'color', 'rgb(255, 255, 255)')
        .and(($avatar) => {
          const box = $avatar[0].getBoundingClientRect();
          const radius = parseFloat(getComputedStyle($avatar[0]).borderTopLeftRadius);
          expect(box.width, 'avatar quadrado').to.eq(box.height);
          expect(radius, 'cantos totalmente redondos').to.be.at.least(box.width / 2);
        });
    });

    it('abre o menu do avatar com meus dados, minha conta e sair, e o fecha com Esc', () => {
      cy.visit('/dashboard');

      openUserMenu();

      cy.get('.mat-mdc-menu-panel [role="menuitem"]').then(($items) => {
        expect([...$items].map((item) => item.textContent?.trim())).to.deep.eq([
          'Meus dados',
          'Minha conta',
          'Sair',
        ]);
      });
      cy.get('body').type('{esc}');
      cy.get('.mat-mdc-menu-panel').should('not.exist');
    });

    it('leva "Meus dados" e "Minha conta" às telas, que têm o menu lateral', () => {
      cy.visit('/dashboard');

      chooseUserMenu('menu-profile');
      cy.location('pathname').should('eq', '/condominio/meus-dados');
      cy.get('nav[aria-label="Menu principal"]').should('be.visible');

      chooseUserMenu('menu-account');
      cy.location('pathname').should('eq', '/conta');
      cy.contains('h1', 'Minha conta').should('be.visible');
    });

    it('não tem menu lateral para o síndico que ainda não cadastrou o condomínio', () => {
      stubSignedIn(NEW_MANAGER);
      cy.visit('/condominio/cadastrar');

      cy.get('nav[aria-label="Menu principal"]').should('not.exist');
      cy.get('[data-testid="menu-toggle"]').should('not.exist');
      cy.get('[data-testid="user-menu"]').should('be.visible');
      cy.get('mat-toolbar a[aria-label="Urban, ir para a página inicial"]').should('be.visible');
      expectText('[data-testid="current-user"]', 'sindica@example.com - Síndico');
    });

    it('dá ao administrador só a visão geral, os síndicos e os pedidos de cadastro no menu lateral', () => {
      stubSignedIn({ ...MANAGER, role: 'SuperAdmin', condominiumId: null });
      cy.visit('/conta');

      cy.get('nav[aria-label="Menu principal"] a[data-testid^="nav-"]').should('have.length', 3);
      cy.get('[data-testid="nav-overview"]').should('be.visible');
      cy.get('[data-testid="nav-managers"]').should('be.visible');
      cy.get('[data-testid="nav-dashboard"]').should('not.exist');
      cy.get('[data-testid="nav-registration-requests"]').should('be.visible');
      cy.get('[data-testid="nav-units"]').should('not.exist');
      expectText('[data-testid="current-user"]', 'Olá Marina - Administrador');
      openUserMenu();
      cy.get('[data-testid="menu-profile"]').should('not.exist');
      cy.get('[data-testid="menu-account"]').should('be.visible');
    });

    it('mantém a rolagem na própria página, sem prender o conteúdo numa área com barra', () => {
      cy.viewport(DESKTOP.width, 300);
      cy.visit('/unidades');
      cy.contains('h1', 'Unidades').should('be.visible');

      cy.scrollTo('bottom');

      cy.window().its('scrollY').should('be.greaterThan', 0);
      expectNoHorizontalScroll();
    });
  });

  describe('no celular (375 px)', () => {
    beforeEach(() => {
      cy.viewport(PHONE.width, PHONE.height);
    });

    it('esconde o menu lateral e mostra o botão de abrir no cabeçalho, ao lado da logo', () => {
      cy.visit('/dashboard');

      cy.get('mat-sidenav').should('not.be.visible');
      cy.get('[data-testid="menu-toggle"]')
        .should('be.visible')
        .and('have.attr', 'aria-label', 'Abrir menu')
        .and('have.attr', 'aria-expanded', 'false');
      cy.get('mat-toolbar a[aria-label="Urban, ir para a página inicial"]').should('be.visible');
      expectNoHorizontalScroll();
    });

    it('abre a gaveta pelo botão e mostra todas as telas', () => {
      cy.visit('/dashboard');

      openDrawer();

      cy.get('[data-testid="menu-toggle"]')
        .should('have.attr', 'aria-label', 'Fechar menu')
        .and('have.attr', 'aria-expanded', 'true');
      for (const [id, label] of LINKS) {
        cy.get(`[data-testid="${id}"]`).should('be.visible').and('contain.text', label);
      }
      expectNoHorizontalScroll();
    });

    it('leva à tela escolhida e fecha a gaveta sozinha', () => {
      cy.visit('/dashboard');
      openDrawer();

      cy.get('[data-testid="nav-units"]').click();

      cy.location('pathname').should('eq', '/unidades');
      cy.get('mat-sidenav').should('not.have.class', 'mat-drawer-opened');
      cy.contains('h1', 'Unidades').should('be.visible');
    });

    it('fecha a gaveta com Esc', () => {
      cy.visit('/dashboard');
      openDrawer();
      // Ao abrir, o foco vai para dentro da gaveta; é lá que o Esc é ouvido.
      cy.focused().closest('mat-sidenav').should('exist');

      cy.focused().type('{esc}');

      cy.get('mat-sidenav').should('not.have.class', 'mat-drawer-opened');
    });

    it('esconde o texto da saudação, que passa a aparecer dentro do menu do avatar', () => {
      cy.visit('/dashboard');

      cy.get('[data-testid="current-user"]').should('not.be.visible');
      cy.get('[data-testid="user-menu"]').should('be.visible');

      openUserMenu();

      expectText('[data-testid="menu-user"]', 'Olá Marina - Síndico');
      expectNoHorizontalScroll();
    });

    it('mantém tema, avatar e botão do menu na mesma linha do cabeçalho, sem rolagem horizontal', () => {
      cy.visit('/dashboard');

      cy.get('mat-toolbar').then(($toolbar) => {
        const toolbar = $toolbar[0].getBoundingClientRect();
        for (const testId of ['menu-toggle', 'user-menu']) {
          cy.get(`[data-testid="${testId}"]`).then(($item) => {
            const box = $item[0].getBoundingClientRect();
            expect(box.top, `${testId} dentro do cabeçalho`).to.be.at.least(toolbar.top);
            expect(box.bottom, `${testId} dentro do cabeçalho`).to.be.at.most(toolbar.bottom);
            expect(box.right, `${testId} dentro da tela`).to.be.at.most(PHONE.width);
          });
        }
      });
      expectNoHorizontalScroll();
    });

    it('não mostra o botão do menu para quem não tem menu lateral', () => {
      stubSignedIn(NEW_MANAGER);
      cy.visit('/condominio/cadastrar');

      cy.get('[data-testid="menu-toggle"]').should('not.exist');
      cy.get('[data-testid="user-menu"]').should('be.visible');
      expectNoHorizontalScroll();
    });
  });
});
