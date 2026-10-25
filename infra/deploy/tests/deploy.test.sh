#!/usr/bin/env bash
# Testa o deploy.sh sem Docker de verdade: docker e curl sao substituidos por stubs que
# registram as chamadas. Uso: bash infra/deploy/tests/deploy.test.sh
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DEPLOY_SH="$HERE/../deploy.sh"
FAILURES=0

check() {
  local description="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    echo "ok: $description"
  else
    echo "FALHOU: $description (esperado '$expected', obtido '$actual')"
    FAILURES=$((FAILURES + 1))
  fi
  return 0
}

# Monta um servidor de mentira: compose em uso (OLD), compose novo (NEW) e a tag anterior.
new_server() {
  SERVER="$(mktemp -d)"
  mkdir -p "$SERVER/bin"
  cp "$DEPLOY_SH" "$SERVER/deploy.sh"
  echo OLD > "$SERVER/docker-compose.yml"
  echo NEW > "$SERVER/docker-compose.next.yml"
  echo "POSTGRES_PASSWORD=x" > "$SERVER/secrets.env"
  printf 'URBAN_TAG=sha-old\n' > "$SERVER/.env"
  : > "$SERVER/docker.log"
  cat > "$SERVER/bin/docker" <<'STUB'
#!/usr/bin/env bash
echo "$*" >> "$(dirname "$0")/../docker.log"
if [[ "$*" == *"--wait"* && "${FAIL_WAIT:-0}" == "1" ]]; then
  exit 1
fi
exit 0
STUB
  printf '#!/usr/bin/env bash\nexit 0\n' > "$SERVER/bin/curl"
  chmod +x "$SERVER/bin/docker" "$SERVER/bin/curl"
  return 0
}

run_deploy() {
  (cd "$SERVER" && PATH="$SERVER/bin:$PATH" bash ./deploy.sh staging sha-new 5210 > "$SERVER/out.log" 2>&1)
  return $?
}

# 1) Deploy saudavel: o compose novo entra e o anterior fica guardado.
new_server
run_deploy; status=$?
check "deploy saudavel termina com 0" "0" "$status"
check "compose novo em uso" "NEW" "$(cat "$SERVER/docker-compose.yml")"
check "endereco do site no .env" "1" "$(grep -c '^WEB_BASE_URL=https://staging.urban-pro.io$' "$SERVER/.env")"
check "compose anterior guardado" "OLD" "$(cat "$SERVER/docker-compose.previous.yml")"
check "tag nova no .env" "1" "$(grep -c '^URBAN_TAG=sha-new$' "$SERVER/.env")"

# 2) Falha ao subir: volta o compose E a tag anteriores.
new_server
FAIL_WAIT=1 run_deploy; status=$?
check "falha ao subir termina com 1" "1" "$status"
check "compose anterior restaurado" "OLD" "$(cat "$SERVER/docker-compose.yml")"
check "tag anterior restaurada no .env" "1" "$(grep -c '^URBAN_TAG=sha-old$' "$SERVER/.env")"
check "segredos preservados no .env" "1" "$(grep -c '^POSTGRES_PASSWORD=x$' "$SERVER/.env")"

# 3) Sem compose novo: nada e alterado.
new_server
rm "$SERVER/docker-compose.next.yml"
run_deploy; status=$?
check "sem compose novo termina com 2" "2" "$status"
check "compose em uso intacto" "OLD" "$(cat "$SERVER/docker-compose.yml")"

if [[ "$FAILURES" -gt 0 ]]; then
  echo "$FAILURES verificacao(oes) falharam"
  exit 1
fi
echo "todas as verificacoes passaram"