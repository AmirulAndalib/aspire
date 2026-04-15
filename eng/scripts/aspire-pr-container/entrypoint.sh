#!/usr/bin/env bash

set -euo pipefail

usage() {
    cat <<'EOF'
Usage:
  aspire-pr-entrypoint.sh [command [args...]]

Environment:
  HOME                     Defaults to /workspace when unset or not writable

Default command:
  bash
EOF
}

if [[ $# -eq 0 ]]; then
    set -- bash
fi

case "$1" in
    -h|--help)
        usage
        exit 0
        ;;
esac

if [[ -z "${HOME:-}" || ! -d "${HOME}" || ! -w "${HOME}" ]]; then
    export HOME=/workspace
fi

mkdir -p "$HOME"

exec "$@"
