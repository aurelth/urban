# urban-web

Frontend do Urban: Angular 22 (standalone, zoneless, signals), Angular Material 22 (Material 3), Tailwind CSS 4, NgRx Signals e GSAP.

## Comandos

| Comando            | O que faz                                                                                                |
| ------------------ | -------------------------------------------------------------------------------------------------------- |
| `npm start`        | Servidor de desenvolvimento em http://localhost:4200 (proxy de `/api` e `/hubs` para o Gateway em :5100) |
| `npm run build`    | Build de producao em `dist/urban-web`                                                                    |
| `npm test`         | Testes unitarios (Jest)                                                                                  |
| `npm run test:ci`  | Jest com cobertura (usado no CI)                                                                         |
| `npm run lint`     | ESLint (angular-eslint)                                                                                  |
| `npm run e2e:ci`   | Sobe o app e roda os testes E2E (Cypress)                                                                |
| `npm run e2e:open` | Abre o Cypress interativo (com `npm start` em outro terminal)                                            |

## Tema e estilos

- Cores, tipografia e formas vem do tema Material 3 em `src/styles/_material-theme.scss` (paleta provisoria).
- O Tailwind enxerga as cores do tema (`bg-surface`, `text-on-surface-variant`, `border-outline-variant`...): ver `src/styles/tailwind.css`.
- Tema escuro: classe `app-dark` no `<html>`, controlada pelo `ThemeStore`.
- Regra de uso: Tailwind para layout e espacamento dos nossos elementos; a aparencia dos componentes Material muda pelo tema (tokens), nao por classes utilitarias.
- Icones: SVGs do Material Symbols servidos pelo proprio app. Para usar um icone novo, inclua o nome no `glob` de assets do `angular.json` e use `<mat-icon svgIcon="nome" />`.

## Estrutura de `src/app`

- `core/`: singletons da aplicacao (auth, http, realtime, layout, services, theme).
- `shared/`: reutilizavel entre features (ui, animations, directives, pipes, models). Componentes de biblioteca de UI ficam encapsulados aqui.
- `features/`: uma pasta por area, carregada sob demanda (lazy loading).
- `src/styles/`: `tailwind.css`, `_tokens.scss`, `_material-theme.scss` e `main.scss`.
