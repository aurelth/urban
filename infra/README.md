# Infraestrutura local

Ambiente de desenvolvimento com Docker Compose. Requer Docker Desktop.

## Primeira vez

    Copy-Item infra\.env.dev infra\.env

O `infra/.env` é ignorado pelo git. O `infra/.env.dev` é o modelo (apenas valores de desenvolvimento).

## Subir e parar

    # essenciais (postgres, redis, rabbitmq, garage)
    docker compose -f infra/docker-compose.yml up -d

    # + ferramentas (pgadmin, seq)
    docker compose -f infra/docker-compose.yml --profile tools up -d

    # + observabilidade (jaeger, prometheus, loki, grafana)
    docker compose -f infra/docker-compose.yml --profile observability up -d

    # tudo
    docker compose -f infra/docker-compose.yml --profile tools --profile observability up -d

    # parar (mantém os dados)
    docker compose -f infra/docker-compose.yml --profile tools --profile observability down

Atenção: `down -v` apaga os volumes, ou seja, todos os dados locais.

## Serviços

| Serviço | Perfil | Endereço | Credenciais (dev) |
|---|---|---|---|
| PostgreSQL 18 | - | localhost:5432, banco `urban` | urban / urban_dev_password |
| Redis 8 | - | localhost:6379 | senha urban_dev_password |
| RabbitMQ 4 | - | AMQP localhost:5672, painel http://localhost:15672 | urban / urban_dev_password |
| Garage (S3) | - | http://localhost:3900 (região `garage`, path-style), admin :3903 | ver `infra/.env` |
| pgAdmin | tools | http://localhost:5050 | admin@urban-pro.io / urban_dev_password |
| Seq | tools | http://localhost:5341 | sem login (somente dev) |
| Grafana | observability | http://localhost:3000 | admin / urban_dev_password |
| Prometheus | observability | http://localhost:9090 | - |
| Loki | observability | http://localhost:3100 | - |
| Jaeger | observability | UI http://localhost:16686, OTLP gRPC 4317 e HTTP 4318 | - |

## Notas

- Todas as portas ficam presas em 127.0.0.1 (acessíveis apenas na própria máquina).
- Os dados ficam em volumes nomeados do Docker, não em pastas do projeto.
- Jaeger fixado em 2.20.x: a partir da 2.21 foi removida a API que o plugin do Grafana usa (grafana/grafana-jaeger-datasource#87).
- No Garage, as flags `--single-node --default-bucket` são só para desenvolvimento. Em produção o layout é criado manualmente.
- Segredos de produção nunca entram neste repositório (ele é público).
