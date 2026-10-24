#!/usr/bin/env bash
# Atualiza um ambiente do Urban neste servidor e volta atras se nao ficar saudavel.
# Uso: bash deploy.sh <staging|production> <tag-da-imagem> <porta-local-do-web>
set -euo pipefail

# Os arquivos com segredos nascem legiveis so pelo dono.
umask 077

ENVIRONMENT="${1:?uso: deploy.sh <staging|production> <tag> <porta>}"
TAG="${2:?informe a tag da imagem}"
PORT="${3:?informe a porta local do web}"

case "$ENVIRONMENT" in
  staging) ASPNET_ENV=Staging ;;
  production) ASPNET_ENV=Production ;;
  *) echo "ambiente invalido: $ENVIRONMENT" >&2; exit 2 ;;
esac

# Trabalha sempre na pasta do ambiente (~/urban/<ambiente>).
cd "$(dirname "$0")"

# secrets.env e escrito pelo workflow de deploy a partir dos secrets do Environment do GitHub.
SECRETS_FILE="secrets.env"
if [[ ! -s "$SECRETS_FILE" ]]; then
  echo "Falta o arquivo $SECRETS_FILE (segredos do ambiente). O workflow de deploy o envia." >&2
  exit 2
fi

write_env() {
  local tag="$1"
  cat > .env <<ENVFILE
URBAN_ENV=$ENVIRONMENT
URBAN_TAG=$tag
WEB_PORT=$PORT
ASPNETCORE_ENVIRONMENT=$ASPNET_ENV
ENVFILE
  cat "$SECRETS_FILE" >> .env
  return 0
}

# Saudavel = o web responde pela porta publicada E a API responde por dentro da rede.
wait_healthy() {
  for _ in $(seq 1 30); do
    if curl -fsS "http://127.0.0.1:${PORT}/healthz" >/dev/null 2>&1 \
      && docker compose exec -T web wget -qO- http://api:8080/health >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  return 1
}

PREVIOUS_TAG=""
if [[ -f .env ]]; then
  PREVIOUS_TAG="$(grep '^URBAN_TAG=' .env | cut -d= -f2 || true)"
fi

# Volta o ambiente para a versao anterior (se existir uma diferente da nova).
restore_previous() {
  if [[ -n "$PREVIOUS_TAG" && "$PREVIOUS_TAG" != "$TAG" ]]; then
    echo "Voltando para a versao anterior: $PREVIOUS_TAG" >&2
    write_env "$PREVIOUS_TAG"
    docker compose up -d --remove-orphans
  fi
  return 0
}

write_env "$TAG"
if ! docker compose pull; then
  echo "Nao foi possivel baixar as imagens da tag $TAG" >&2
  restore_previous
  exit 1
fi

# --wait espera Postgres e Redis ficarem saudaveis (healthcheck) antes de dar a subida por concluida.
if ! docker compose up -d --remove-orphans --wait --wait-timeout 120; then
  echo "Falhou ao subir os servicos. Ultimos logs:" >&2
  docker compose logs --tail 50 >&2 || true
  restore_previous
  exit 1
fi

if wait_healthy; then
  echo "Deploy ok: $ENVIRONMENT -> $TAG"
  # Remove somente imagens antigas do Urban (nunca as de outros apps do servidor).
  docker image prune -af --filter "label=org.opencontainers.image.source=https://github.com/aurelth/urban" --filter "until=168h" >/dev/null || true
  exit 0
fi

echo "Falhou a verificacao de saude. Ultimos logs:" >&2
docker compose logs --tail 50 >&2 || true

restore_previous
exit 1