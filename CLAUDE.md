# Urban: memória do projeto

Leia este arquivo inteiro antes de criar ou alterar qualquer coisa neste repositório. Se algo aqui conflitar com o que o Aurel pedir na conversa, vale o pedido dele; avise e proponha atualizar este arquivo.

## O que é

Urban (https://urban-pro.io) é um sistema de gestão condominial que substitui as planilhas da síndica do condomínio do Aurel. Ela será a beta-tester do MVP. Roadmap em 3 fases, com recursos de IA na fase 2. O primeiro módulo é Identidade (autenticação).

## Como trabalhar com o Aurel

- Idioma: português do Brasil na conversa, na interface, nos commits, PRs e documentação. Documentos gerados se chamam `Urban-…`.
- Ele quer aprender enquanto constrói: explique e justifique cada escolha (tecnologia, arquitetura, como front, back, infra, testes e CI/CD se comunicam), com detalhe suficiente para ele ensinar outras pessoas.
- Guie passo a passo, em texto simples no chat (sem widget de cards), com comandos exatos para o PowerShell que ele já está usando. Tudo o que der para fazer pelo terminal é feito pelo terminal, sem abrir outra janela. Passos só de navegador (Hostinger, SonarQube Cloud) são guiados com capturas de tela.
- Dê o resultado esperado de cada comando (por exemplo, número de linhas de um `git diff --stat`) e peça para ele colar a saída quando algo divergir.
- Nunca peça nem aceite chaves, tokens ou senhas no chat. Segredos entram só por `gh secret set` ou pelo ambiente do GitHub.
- Não mexa nos outros apps do VPS (Orizon e FinanceFlow) sem pedido explícito.

## Princípios de engenharia

SOLID, Clean Code, padrões de projeto, CQRS, Clean Architecture, desempenho, escala, reuso e facilidade de manutenção e de troca de tecnologia. Prefira soluções simples e justificadas a soluções genéricas demais.

## Stack (lista oficial)

- Backend: .NET 10 / C# 14, monolito modular, EF Core 10 + PostgreSQL 18, MediatR 12.5.0 + FluentValidation (CQRS), ASP.NET Identity + JWT, BCrypt.Net, Hangfire, SignalR, Redis 8, RabbitMQ + MassTransit 8.5.10, Garage (S3), YARP (gateway), Scalar (documentação da API), Brevo (e-mail).
- Observabilidade: Serilog + Seq/Loki, OpenTelemetry, Jaeger, Prometheus, Grafana.
- Frontend: Angular 22 + TypeScript 6, Angular Material 22 (Material 3), NgRx Signals, Tailwind 4, SCSS, GSAP, ícones Material Symbols locais.
- Testes: xUnit + Moq + FluentAssertions 7.2.2, NetArchTest, Testcontainers, Jest 30, Cypress 16.
- Entrega: GitHub Actions, GHCR, SonarQube Cloud, Dependabot, Semantic Release, Docker, VPS Hostinger com nginx e certbot.
- Congelados de propósito (licença): MediatR 12.5.0, MassTransit 8.5.10, FluentAssertions 7.2.2. O Dependabot ignora major dessas e de Moq. Não atualize por conta própria.
- Não use PrimeNG (foi trocado pelo Angular Material) nem Kafka (foi trocado por RabbitMQ).

## Arquitetura

- Solução `Urban.slnx`, versões de pacotes centralizadas em `Directory.Packages.props`, SDK fixado em `global.json`.
- Projetos de entrada: `Urban.Api`, `Urban.Gateway`, `Urban.Workers`, mais `Urban.SharedKernel`.
- Módulos em `src/Modules/<Modulo>/Urban.<Modulo>.{Domain,Application,Infrastructure}`: Identidade, Condominio, Financeiro, Operacoes.
- Regras de dependência (verificadas por NetArchTest em `tests/Urban.ArchTests`): Domain não depende de nada; Application depende só de Domain; Infrastructure depende de Application; módulos não referenciam as camadas internas uns dos outros e se comunicam por contratos e eventos de integração (RabbitMQ).
- CQRS: comandos e consultas separados (MediatR), validação com FluentValidation em pipeline, regras de negócio no Domain.
- Testes: `tests/Urban.UnitTests`, `tests/Urban.IntegrationTests` (WebApplicationFactory), `tests/Urban.ArchTests`; E2E do frontend em `urban-web/cypress`.

## Convenções de código

- C#: tipos explícitos em vez de `var` quando o tipo não é óbvio (regra IDE0008 do Sonar), `await app.RunAsync()`, nullable habilitado, funções puras extraídas para poderem ser testadas.
- Cobertura de código novo mínima de 80% (gate do Sonar). Arquivos sem lógica (`Program.cs`, `AssemblyReference.cs`, `*.routes.ts`) já estão excluídos da cobertura.
- Angular: zoneless, componentes OnPush e standalone, estado com NgRx Signals. Tailwind só para o layout dos próprios elementos; a aparência dos componentes do Material vem dos tokens do tema, nunca de CSS por cima.
- Textos da interface em português do Brasil, em sentença (só a primeira letra maiúscula), verbo no começo dos botões, sem ponto final em rótulos e títulos.
- Idioma dos identificadores no código (português ou inglês): a decidir no início do módulo Identidade e registrar aqui.
- Nunca desative o sanitizador do Angular sem validar a entrada e documentar o motivo (ver `core/theme/icons.ts`).

## Interface (identidade visual)

Estilo Apple: limpo, minimalista, muito espaço em branco, bordas finas, um único acento. Paleta "Índigo suave":

| Papel | Claro |
| --- | --- |
| Fundo | `#F6F6FA` |
| Superfície (cards) | `#FFFFFF` |
| Texto | `#1C1C1E` |
| Texto secundário | `#65657A` |
| Destaque (cor-semente do Material 3) | `#5856D6`, texto sobre ele `#FFFFFF` |
| Linhas e bordas | `#E6E6EF` |
| Sucesso / pago | `#248A3D` |
| Alerta / pendente | `#B25000` |
| Erro / atrasado | `#D70015` |

O modo escuro é gerado pelo Material 3 a partir da cor-semente (destaque mais claro, fundos grafite). Estados nunca dependem só de cor: use também texto ou ícone.

## Git e CI

- `main` (produção) e `develop` (integração) protegidas. Branches de trabalho saem da `develop` e seguem `^(feature|fix|chore|docs|hotfix)/.+`. Nunca use `ci/…`.
- Commits e títulos de PR em Conventional Commits: `^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\([a-z0-9-]+\))?!?: .{1,100}$` (escopo em minúsculas).
- PR de trabalho para `develop`: squash e apagar a branch. `develop` para `main`: merge commit, sem apagar a `develop`.
- Checks obrigatórios: `pr-title`, `branch-policy`, `build-test`, `web-build-test`. Também rodam `web-e2e` e o Sonar. Só faça o merge com todos verdes e confira que a lista está completa.
- Em CI use `npm ci --ignore-scripts` e binários locais (`./node_modules/.bin/...`) em vez de `npx`.
- Commits e PRs levam as linhas de co-autoria e de sessão definidas pelo ambiente.

## Entrega e infraestrutura

- Push na `develop` publica imagens `sha-<7>` no GHCR e faz deploy em https://staging.urban-pro.io. Push na `main` roda o Semantic Release (tags `vX.Y.Z`, começando em 0.x) e, com versão nova, faz deploy em https://urban-pro.io depois de aprovação manual.
- Código de deploy em `infra/deploy/` (compose, `deploy.sh` com health check e rollback, modelos de nginx). Portas do Urban no VPS: 5200 (produção) e 5210 (staging), só em 127.0.0.1.
- Dockerfiles em `infra/`; compose local em `infra/docker-compose.yml` com perfis `tools` e `observability`.
- Endpoints de apoio: `GET /health` e `GET /api/version`.

## PowerShell 5.1 (terminal do Aurel)

- Funções `New-RepoFile` e `Complete-Pr` ficam em `~\urban-helpers.ps1`, carregado pelo perfil.
- Não coloque `else` em linha separada no prompt; cole blocos em uma linha. Não use placeholders como `NN` em comandos: use o número real. Aspas duplas dentro de argumentos de programas nativos quebram; use filtros `jq` sem aspas ou JSON gravado em arquivo com `--input`.
- `Get-Content` precisa de `-Encoding UTF8` para acentos. Use `curl.exe`. Mensagens de commit passam por pipe em ASCII.
- `gh pr checks --watch` logo depois de um push pode mostrar a rodada anterior ou uma lista incompleta: espere e repita.
