#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TEMP_DIR="$(mktemp -d)"
PORT="${PORT:-5087}"
BASE_URL="http://127.0.0.1:${PORT}"
APP_PID=""

cleanup() {
  if [[ -n "$APP_PID" ]]; then
    kill "$APP_PID" 2>/dev/null || true
    wait "$APP_PID" 2>/dev/null || true
  fi
  if [[ "${KEEP_TEST_ARTIFACTS:-0}" == "1" ]]; then
    echo "Test artifacts kept at: $TEMP_DIR"
  else
    rm -rf "$TEMP_DIR"
  fi
}
trap cleanup EXIT

(
  cd "$TEMP_DIR"
  dotnet run \
    --project "$ROOT_DIR/validation/Validation.WebApi/Validation.WebApi.csproj" \
    --no-launch-profile \
    --urls "$BASE_URL"
) >"$TEMP_DIR/app.log" 2>&1 &
APP_PID=$!

fail() {
  echo "FAIL: $*" >&2
  echo "--- API log ---" >&2
  cat "$TEMP_DIR/app.log" >&2 || true
  exit 1
}

echo "[1/7] Waiting for API startup"
ready=0
for _ in $(seq 1 60); do
  if curl --silent --fail "$BASE_URL/validation/integration-events" >/dev/null; then
    ready=1
    break
  fi
  if ! kill -0 "$APP_PID" 2>/dev/null; then
    fail "API process exited before startup"
  fi
  sleep 1
done
[[ "$ready" == "1" ]] || fail "API did not start within 60 seconds"

echo "[2/7] POST /orders returns 201 without domain-event side effects"
create_status="$(curl --silent --show-error -o "$TEMP_DIR/create.json" -w '%{http_code}' \
  -H 'Content-Type: application/json' \
  -d '{"productName":"Coffee"}' "$BASE_URL/orders")"
[[ "$create_status" == "201" ]] || fail "POST /orders expected 201, got $create_status"
ORDER_ID="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["orderId"])' "$TEMP_DIR/create.json")"
[[ -n "$ORDER_ID" ]] || fail "POST /orders returned an empty orderId"

echo "[3/7] GET /orders/{id} shows a simple committed order"
get_status="$(curl --silent --show-error -o "$TEMP_DIR/order.json" -w '%{http_code}' "$BASE_URL/orders/$ORDER_ID")"
[[ "$get_status" == "200" ]] || fail "GET /orders/{id} expected 200, got $get_status"
python3 - "$TEMP_DIR/order.json" "$ORDER_ID" <<'PY'
import json, sys
payload = json.load(open(sys.argv[1]))
assert payload["id"].lower() == sys.argv[2].lower(), payload
assert payload["productName"] == "Coffee", payload
assert payload["confirmed"] is False, payload
assert payload["auditMessages"] == [], payload
PY

echo "[4/7] POST /orders/{id}/confirm uses the explicit transaction path"
confirm_status="$(curl --silent --show-error -o "$TEMP_DIR/confirm.json" -w '%{http_code}' \
  -X POST "$BASE_URL/orders/$ORDER_ID/confirm")"
[[ "$confirm_status" == "200" ]] || fail "POST /orders/{id}/confirm expected 200, got $confirm_status"

echo "[5/7] GET /orders/{id} shows the domain-event side effect"
get_confirmed_status="$(curl --silent --show-error -o "$TEMP_DIR/confirmed.json" -w '%{http_code}' "$BASE_URL/orders/$ORDER_ID")"
[[ "$get_confirmed_status" == "200" ]] || fail "GET confirmed order expected 200, got $get_confirmed_status"
python3 - "$TEMP_DIR/confirmed.json" "$ORDER_ID" <<'PY'
import json, sys
payload = json.load(open(sys.argv[1]))
assert payload["id"].lower() == sys.argv[2].lower(), payload
assert payload["productName"] == "Coffee", payload
assert payload["confirmed"] is True, payload
assert payload["auditMessages"] == ["Order confirmed: Coffee"], payload
PY

echo "[6/8] Integration event failure rolls back the confirmation transaction"
curl --silent --show-error --fail -X POST "$BASE_URL/validation/fail-next-integration-event" >/dev/null
rollback_status="$(curl --silent --show-error -o "$TEMP_DIR/rollback.json" -w '%{http_code}' \
  -X POST "$BASE_URL/orders/$ORDER_ID/confirm")"
[[ "$rollback_status" == "500" ]] || fail "Failed confirmation expected 500, got $rollback_status"
rollback_get_status="$(curl --silent --show-error -o "$TEMP_DIR/rollback-order.json" -w '%{http_code}' "$BASE_URL/orders/$ORDER_ID")"
[[ "$rollback_get_status" == "200" ]] || fail "GET after rollback expected 200, got $rollback_get_status"
python3 - "$TEMP_DIR/rollback-order.json" <<'PY'
import json, sys
payload = json.load(open(sys.argv[1]))
assert payload["confirmed"] is False, payload
assert payload["auditMessages"] == [], payload
PY

echo "[7/8] Invalid create requests and unknown order return expected status codes"
missing_status="$(curl --silent --show-error -o /dev/null -w '%{http_code}' \
  "$BASE_URL/orders/00000000-0000-0000-0000-000000000001")"
[[ "$missing_status" == "404" ]] || fail "Missing order expected 404, got $missing_status"
for payload in '{}' '{"productName":""}' '{"productName":"   "}' 'null'; do
  status="$(curl --silent --show-error -o "$TEMP_DIR/invalid.json" -w '%{http_code}' \
    -H 'Content-Type: application/json' -d "$payload" "$BASE_URL/orders")"
  [[ "$status" == "400" ]] || fail "Invalid payload $payload expected 400, got $status"
done

echo "[8/8] Domain event handler published the integration event"
curl --silent --show-error --fail "$BASE_URL/validation/integration-events" >"$TEMP_DIR/events.json"
python3 - "$TEMP_DIR/events.json" <<'PY'
import json, sys
events = json.load(open(sys.argv[1]))
assert "validation.order.confirmed" in events, events
assert "validation.order.created" not in events, events
PY

echo "PASS: all 8 API smoke-test groups passed."
