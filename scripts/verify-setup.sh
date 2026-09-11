#!/usr/bin/env bash
# Brings a fresh cloud session (no .NET SDK, no node_modules) to the point where the
# standard verification commands in CLAUDE.md / docs/plan/PLAN.md can run.
#
# Usage:
#   bash scripts/verify-setup.sh
#
# After it finishes:
#   export PATH="/opt/dotnet-sdk:$PATH"   # only needed if this script installed the SDK
#   dotnet build backend/PamyatRyadom.sln
#   cd frontend && npx tsc --noEmit && npm run lint && npm run build
#
# `dotnet test` additionally needs a running Docker daemon (Testcontainers) — this
# script does not attempt to provide one; see docs/plan/PLAN.md for details.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_INSTALL_DIR="/opt/dotnet-sdk"
DOTNET_CHANNEL="10.0"

if command -v dotnet >/dev/null 2>&1; then
  echo "verify-setup: dotnet already on PATH ($(command -v dotnet)), skipping SDK install"
else
  echo "verify-setup: dotnet not found, installing .NET SDK ${DOTNET_CHANNEL} into ${DOTNET_INSTALL_DIR}"
  tmp_installer="$(mktemp)"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$tmp_installer"
  bash "$tmp_installer" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_INSTALL_DIR"
  rm -f "$tmp_installer"
  export PATH="${DOTNET_INSTALL_DIR}:${PATH}"
  echo "verify-setup: installed $(dotnet --version); add 'export PATH=\"${DOTNET_INSTALL_DIR}:\$PATH\"' to run dotnet in this shell later"
fi

echo "verify-setup: running npm ci in frontend/"
(cd "$REPO_ROOT/frontend" && npm ci)

echo "verify-setup: done. Standard verification:"
echo "  dotnet build ${REPO_ROOT}/backend/PamyatRyadom.sln"
echo "  (cd ${REPO_ROOT}/frontend && npx tsc --noEmit && npm run lint && npm run build)"
