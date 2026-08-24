#!/usr/bin/env bash
# Applies EF Core migrations using a one-off .NET SDK container on the running
# compose network. Run from anywhere; it cd-s to the project root and reads .env.
#   bash deploy/migrate.sh
set -eo pipefail

cd "$(dirname "$0")/.."

if [ ! -f ./.env ]; then
  echo "ERROR: .env not found in $(pwd). Copy it first: cp .env.example .env && nano .env" >&2
  exit 1
fi

# Read only the DB creds we need, WITHOUT sourcing .env — a value with spaces
# would break `source`/`. .env`.
read_env() { grep -E "^$1=" ./.env | tail -1 | cut -d= -f2- | sed -e 's/^"\(.*\)"$/\1/' -e "s/^'\(.*\)'\$/\1/"; }
POSTGRES_DB="$(read_env POSTGRES_DB)"
POSTGRES_USER="$(read_env POSTGRES_USER)"
POSTGRES_PASSWORD="$(read_env POSTGRES_PASSWORD)"

NET="$(docker network ls --format '{{.Name}}' | grep -m1 pamyat-ryadom || true)"
if [ -z "$NET" ]; then
  echo "ERROR: no docker network matching 'pamyat-ryadom'. Start the stack first:" >&2
  echo "  docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build" >&2
  exit 1
fi
echo "Using docker network: $NET"

docker run --rm --network "$NET" \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}" \
  -e XDG_DATA_HOME=/tmp/.dotnet \
  -v "$PWD/backend:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -lc "dotnet restore src/PamyatRyadom.Api/PamyatRyadom.Api.csproj && dotnet tool restore && dotnet ef database update --project src/PamyatRyadom.Api --startup-project src/PamyatRyadom.Api"

docker compose -f docker-compose.yml -f docker-compose.prod.yml restart api
echo "Migrations applied and api restarted."
