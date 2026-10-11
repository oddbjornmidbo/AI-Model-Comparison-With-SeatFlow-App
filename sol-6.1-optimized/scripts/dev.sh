#!/usr/bin/env bash
set -euo pipefail
seatflow_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$seatflow_root"
seatflow_dotnet="${DOTNET_BIN:-dotnet}"
if ! command -v "$seatflow_dotnet" >/dev/null 2>&1; then
  printf 'Install the .NET 9 SDK or set DOTNET_BIN to its executable.\n' >&2
  exit 1
fi
if ! node -e 'if (Number(process.versions.node.split(".")[0]) < 20) process.exit(1)' 2>/dev/null; then
  printf 'Use Node.js 22 (for example: nvm use 22), then run this script again.\n' >&2
  exit 1
fi
seatflow_api_port="${SEATFLOW_API_PORT:-5057}"
seatflow_frontend_port="${SEATFLOW_FRONTEND_PORT:-5177}"
"$seatflow_dotnet" build SeatFlow.sln --nologo
if [ ! -d frontend/node_modules ]; then (cd frontend && npm ci); fi
env ASPNETCORE_URLS="http://127.0.0.1:$seatflow_api_port" "$seatflow_dotnet" run --no-build --no-launch-profile --project backend/SeatFlow.Api &
seatflow_api_pid=$!
(
  cd frontend
  env SEATFLOW_API_URL="http://127.0.0.1:$seatflow_api_port" npm run dev -- --port "$seatflow_frontend_port"
) &
seatflow_frontend_pid=$!
trap 'kill "$seatflow_api_pid" "$seatflow_frontend_pid" 2>/dev/null || true' EXIT INT TERM
printf '\nSeatFlow: http://127.0.0.1:%s\nAPI: http://127.0.0.1:%s/api/health\n' "$seatflow_frontend_port" "$seatflow_api_port"
wait "$seatflow_api_pid" "$seatflow_frontend_pid"
