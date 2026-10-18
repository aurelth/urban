# Urban

Sistema de gestão condominial: substitui planilhas por um painel único para o administrador e para os moradores.

**Stack:** .NET 10 (monólito modular, Clean Architecture, CQRS) · Angular 22 + PrimeNG 22 · PostgreSQL 18 · Redis 8 · RabbitMQ + MassTransit · Docker.

## Estrutura do repositório

```text
src/        Backend .NET (API, Gateway, módulos, SharedKernel, Workers)
tests/      Testes unitários, de integração e de arquitetura
infra/      Docker Compose, Dockerfiles, Nginx e observabilidade
urban-web/  Frontend Angular
docs/       Documentação de arquitetura e decisões
.github/    Pipelines, templates e configurações do GitHub
```

## Fluxo de trabalho

- `main`: produção. Só recebe código da `develop`, via Pull Request (merge commit).
- `develop`: integração. Recebe os branches de trabalho via Pull Request (squash).
- Branches de trabalho saem da `develop`: `feature/…`, `fix/…`, `chore/…`, `docs/…`.
- Commits e títulos de PR seguem Conventional Commits: `feat(financeiro): adiciona lançamento de despesas`.

## Segurança

Este repositório é público. **Nunca** versione senhas, chaves de API ou connection strings reais. Use `.env` (ignorado pelo Git) e GitHub Secrets.
