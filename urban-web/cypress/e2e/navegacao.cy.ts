import {
  DESKTOP,
  PHONE,
  chooseUserMenu,
  expectText,
  openDrawer,
  openUserMenu,
} from '../support/navegacao';
import { TODAY } from '../support/financeiro';
import { stubMyAccess, stubMyAccessUnavailable, stubMyMonth } from '../support/minha-fatura';
import { stubProfile } from '../support/perfil';
import { NEW_MANAGER } from '../support/condominio';
import { MANAGER, RESIDENT, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';

const LINKS = [
  ['nav-dashboard', 'Painel', '/dashboard'],
  ['nav-units', 'Unidades', '/unidades'],
  ['nav-residents', 'Moradores', '/moradores'],
  ['nav-vehicles', 'Veículos', '/veiculos'],
  ['nav-month-dashboard', 'Painel financeiro', '/financeiro/painel'],
  ['nav-expenses', 'Despesas', '/financeiro/despesas'],
  ['nav-tariffs', 'Tarifas', '/financeiro/tarifas'],
  ['nav-readings', 'Leituras', '/financeiro/leituras'],
  ['nav-entries', 'Lançamentos', '/financeiro/lancamentos'],
  ['nav-invoice-deliveries', 'Enviar faturas', '/financeiro/enviar-faturas'],
  ['nav-payments', 'Pagamentos', '/financeiro/pagamentos'],
  ['nav-delinquency', 'Inadimplência', '/financeiro/inadimplencia'],
  ['nav-payment-instructions', 'Dados de pagamento', '/financeiro/dados-de-pagamento'],
] as const;

function widthOf($element: JQuery<HTMLElement>): number {
  return Math.round($element[0].getBoundingClientRect().width);
}

/** Espessura da barra de rolagem de um elemento: o que sobra entre a largura total e a útil. */
function scrollbarOf(element: HTMLElement): number {
  return element.offsetWidth - element.clientWidth;
}

/** Quanto falta de espaço no menu lateral: só passa de zero quando os itens não cabem e a barra aparece. */
function menuScroller(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('mat-sidenav .mat-drawer-inner-container');
}

function expectMenuScrolls(scrolls: boolean): void {
  menuScroller().should(($menu) => {
    const element = $menu[0];
    if (scrolls) {
      expect(element.scrollHeight, 'itens maiores que o menu').to.be.greaterThan(
        element.clientHeight,
      );
      expect(scrollbarOf(element), 'barra visível').to.be.greaterThan(0);
    } else {
      expect(element.scrollHeight, 'itens cabem no menu').to.be.at.most(element.clientHeight);
      expect(scrollbarOf(element), 'sem barra').to.eq(0);
    }
  });
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
    stubMyAccess('none');
  });

  describe('síndico que mora no condomínio', () => {
    beforeEach(() => {
      cy.viewport(DESKTOP.width, DESKTOP.height);
    });

    it('ganha "Minha fatura" logo depois do Painel e abre a própria fatura', () => {
      cy.clock(TODAY, ['Date']);
      stubMyAccess('unit');
      stubMyMonth();
      cy.visit('/dashboard');

      cy.get('nav[aria-label="Menu principal"] a').then(($links) => {
        expect([...$links].slice(0, 3).map((link) => link.dataset['testid'])).to.deep.eq([
          'nav-dashboard',
          'nav-my-invoice',
          'nav-units',
        ]);
      });
      cy.get('[data-testid="nav-my-invoice"]').should('contain.text', 'Minha fatura').click();

      cy.location('pathname').should('eq', '/minha-fatura');
      cy.contains('h1', 'Minha fatura').should('be.visible');
      cy.get('[data-testid="nav-my-invoice"]').should('have.attr', 'aria-current', 'page');
    });

    for (const access of ['dependent', 'none'] as const) {
      it(`não mostra "Minha fatura" quando o acesso é ${access}`, () => {
        stubMyAccess(access);
        cy.visit('/dashboard');

        cy.wait('@myAccess');
        cy.get('[data-testid="nav-units"]').should('be.visible');
        cy.get('[data-testid="nav-my-invoice"]').should('not.exist');
      });
    }

    it('segue sem o item, e com o menu de sempre, se a consulta falhar', () => {
      stubMyAccessUnavailable();
      cy.visit('/dashboard');

      cy.wait('@myAccess');
      cy.get('[data-testid="nav-units"]').should('be.visible');
      cy.get('[data-testid="nav-my-invoice"]').should('not.exist');
    });

    it('mostra o item também na gaveta do celular, sem rolagem horizontal', () => {
      cy.viewport(PHONE.width, PHONE.height);
      cy.clock(TODAY, ['Date']);
      stubMyAccess('unit');
      stubMyMonth();
      cy.visit('/dashboard');

      openDrawer();

      cy.get('[data-testid="nav-my-invoice"]').should('be.visible');
      expectNoHorizontalScroll();
    });
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
        cy.get('nav[aria-label="Menu principal"] a mat-icon').should('have.length', LINKS.length);
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

    it('dá ao morador só o painel e a sua fatura no menu lateral', () => {
      stubSignedIn(RESIDENT);
      cy.visit('/dashboard');

      cy.get('nav[aria-label="Menu principal"] a[data-testid^="nav-"]').should('have.length', 2);
      cy.get('[data-testid="nav-dashboard"]').should('be.visible');
      cy.get('[data-testid="nav-my-invoice"]').should('be.visible');
      cy.get('[data-testid="nav-units"]').should('not.exist');
      cy.get('[data-testid="menu-toggle"]').should('be.visible');
      expectText('[data-testid="current-user"]', 'Olá Joao - Morador');
    });

    it('dá ao administrador só a visão geral, os síndicos, os pedidos de cadastro e as mensagens no menu lateral', () => {
      stubSignedIn({ ...MANAGER, role: 'SuperAdmin', condominiumId: null });
      cy.visit('/conta');

      cy.get('nav[aria-label="Menu principal"] a[data-testid^="nav-"]').should('have.length', 4);
      cy.get('[data-testid="nav-overview"]').should('be.visible');
      cy.get('[data-testid="nav-managers"]').should('be.visible');
      cy.get('[data-testid="nav-dashboard"]').should('not.exist');
      cy.get('[data-testid="nav-registration-requests"]').should('be.visible');
      cy.get('[data-testid="nav-messages"]').should('be.visible');
      cy.get('[data-testid="nav-units"]').should('not.exist');
      expectText('[data-testid="current-user"]', 'Olá Marina - Administrador');
      openUserMenu();
      cy.get('[data-testid="menu-profile"]').should('not.exist');
      cy.get('[data-testid="menu-account"]').should('be.visible');
    });

    it('usa uma barra de rolagem fina e discreta na página', () => {
      cy.viewport(DESKTOP.width, 300);
      cy.visit('/unidades');
      cy.contains('h1', 'Unidades').should('be.visible');

      cy.document().should((doc) => {
        const thickness = doc.defaultView!.innerWidth - doc.documentElement.clientWidth;
        expect(thickness, 'barra da página').to.be.greaterThan(0).and.at.most(10);
      });
    });

    describe('seções recolhíveis do menu lateral', () => {
      const SHORT = { width: DESKTOP.width, height: 600 } as const;

      beforeEach(() => {
        cy.viewport(SHORT.width, SHORT.height);
      });

      function section(label: string): Cypress.Chainable<JQuery<HTMLElement>> {
        return cy.contains('button[data-testid="nav-group"]', label);
      }

      it('mostra o título de cada seção como botão aberto, com o ícone de menos', () => {
        cy.visit('/dashboard');

        for (const label of ['Condomínio', 'Financeiro']) {
          section(label)
            .should('have.attr', 'aria-expanded', 'true')
            .find('mat-icon')
            .should('have.attr', 'data-mat-icon-name', 'remove');
        }
      });

      it('recolhe a seção ao clicar e abre de novo ao clicar outra vez', () => {
        cy.visit('/dashboard');

        section('Financeiro').click();

        section('Financeiro')
          .should('have.attr', 'aria-expanded', 'false')
          .find('mat-icon')
          .should('have.attr', 'data-mat-icon-name', 'add');
        cy.get('[data-testid="nav-expenses"]').should('not.exist');
        cy.get('[data-testid="nav-payment-instructions"]').should('not.exist');
        cy.get('[data-testid="nav-units"]').should('be.visible');

        section('Financeiro').click();

        cy.get('[data-testid="nav-expenses"]').should('be.visible');
        section('Financeiro').should('have.attr', 'aria-expanded', 'true');
      });

      it('funciona pelo teclado', () => {
        cy.visit('/dashboard');

        section('Condomínio').focus().type('{enter}');

        cy.get('[data-testid="nav-units"]').should('not.exist');
        section('Condomínio').type(' ');
        cy.get('[data-testid="nav-units"]').should('be.visible');
      });

      it('só mostra a barra do menu quando os itens não cabem, e a tira quando passam a caber', () => {
        cy.visit('/dashboard');
        expectMenuScrolls(true);

        section('Condomínio').click();
        section('Financeiro').click();
        expectMenuScrolls(false);

        section('Condomínio').click();
        expectMenuScrolls(false);

        section('Financeiro').click();
        expectMenuScrolls(true);
      });

      it('deixa a barra do menu fina', () => {
        cy.visit('/dashboard');

        menuScroller().should(($menu) => {
          expect(scrollbarOf($menu[0])).to.be.greaterThan(0).and.at.most(10);
        });
      });

      it('lembra as seções recolhidas depois de recarregar a página', () => {
        cy.visit('/dashboard');
        section('Financeiro').click();

        cy.reload();

        section('Financeiro').should('have.attr', 'aria-expanded', 'false');
        cy.get('[data-testid="nav-expenses"]').should('not.exist');
        cy.get('[data-testid="nav-units"]').should('be.visible');
      });

      it('mostra todos os itens, sem títulos, quando o menu está recolhido em ícones', () => {
        cy.visit('/dashboard');
        section('Financeiro').click();

        cy.get('[data-testid="menu-toggle"]').click();

        cy.get('button[data-testid="nav-group"]').should('not.exist');
        cy.get('[data-testid="nav-expenses"]').should('exist');
        cy.get('[data-testid="nav-payment-instructions"]').should('exist');
      });

      it('tem uma só barra de rolagem no menu recolhido em ícones, a da própria coluna', () => {
        cy.visit('/dashboard');

        cy.get('[data-testid="menu-toggle"]').click();

        cy.get('[data-testid="nav-label"]').first().should('have.class', 'sr-only');
        menuScroller().should(($menu) => {
          expect(scrollbarOf($menu[0]), 'barra da coluna de ícones').to.be.greaterThan(0);
        });
        // Os nomes escondidos (`sr-only`) são posicionados; se a coluna não for a referência deles, a gaveta ganha uma segunda barra.
        cy.get('mat-sidenav').should(($drawer) => {
          expect($drawer[0].scrollHeight, 'área rolável da gaveta').to.be.at.most(
            $drawer[0].clientHeight,
          );
        });
      });
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

    it('recolhe uma seção na gaveta, sem rolagem horizontal, e a escolha continua ao reabrir', () => {
      cy.visit('/dashboard');
      openDrawer();

      cy.contains('button[data-testid="nav-group"]', 'Financeiro').click();

      cy.get('[data-testid="nav-expenses"]').should('not.exist');
      cy.get('[data-testid="nav-units"]').should('be.visible');
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

      // A medição se repete até a página assentar: o cabeçalho ainda se move durante a animação de entrada.
      cy.get('mat-toolbar').should(($toolbar) => {
        const toolbar = $toolbar[0].getBoundingClientRect();
        for (const testId of ['menu-toggle', 'user-menu']) {
          const $item = Cypress.$(`[data-testid="${testId}"]`);
          expect($item, `${testId} na tela`).to.have.length(1);
          const box = $item[0].getBoundingClientRect();
          expect(box.top, `${testId} dentro do cabeçalho`).to.be.at.least(toolbar.top);
          expect(box.bottom, `${testId} dentro do cabeçalho`).to.be.at.most(toolbar.bottom);
          expect(box.right, `${testId} dentro da tela`).to.be.at.most(PHONE.width);
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
