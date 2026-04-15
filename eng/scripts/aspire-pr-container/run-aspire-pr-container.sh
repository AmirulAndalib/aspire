#!/usr/bin/env bash

set -euo pipefail

usage() {
    cat <<'EOF'
Usage:
  run-aspire-pr-container.sh PR_NUMBER [get-aspire-cli-pr.sh options...]
  run-aspire-pr-container.sh bash [args...]
  run-aspire-pr-container.sh sh [args...]

Environment:
  ASPIRE_PR_IMAGE        Docker image name to build/run (default: aspire-pr-runner)
  ASPIRE_PR_WORKSPACE    Host directory to mount as /workspace (default: current directory)
  INSTALL_PREFIX         In-container install prefix (default: /workspace/.aspire)
  ASPIRE_DOCKER_SOCKET   Docker socket path on the host (default: /var/run/docker.sock)
  ASPIRE_CONTAINER_USER  Container user for docker run (default: current uid:gid)
                         Set to 0:0 when the container needs direct Docker socket access.
  ASPIRE_PR_RECORD       Set to 1/true to record the full host-side session with asciinema
  ASPIRE_PR_RECORDING_PATH
                         Output path for the .cast file
                         (default: <workspace>/recordings/<timestamp>-<command>.cast)
  ASPIRE_PR_RECORDING_TITLE
                         Optional title stored in the recording metadata
  GH_TOKEN/GITHUB_TOKEN  GitHub token passed into the container
EOF
}

is_truthy() {
    local value
    value="$(printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]')"

    case "$value" in
        1|true|yes|on)
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

get_recording_stem() {
    local stem="${1:-session}"

    if [[ "$stem" =~ ^[0-9]+$ ]]; then
        stem="pr-$stem"
    fi

    stem="$(printf '%s' "$stem" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9._-]+/-/g; s/^-+//; s/-+$//')"

    if [[ -z "$stem" ]]; then
        stem="session"
    fi

    printf '%s' "$stem"
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT_PATH="$SCRIPT_DIR/$(basename "${BASH_SOURCE[0]}")"
IMAGE_NAME="${ASPIRE_PR_IMAGE:-aspire-pr-runner}"
WORKSPACE="${ASPIRE_PR_WORKSPACE:-$PWD}"
INSTALL_PREFIX="${INSTALL_PREFIX:-/workspace/.aspire}"
DOCKER_SOCKET_PATH="${ASPIRE_DOCKER_SOCKET:-/var/run/docker.sock}"
CONTAINER_USER="${ASPIRE_CONTAINER_USER:-$(id -u):$(id -g)}"

if [[ $# -lt 1 ]]; then
    usage
    exit 1
fi

case "$1" in
    -h|--help)
        usage
        exit 0
        ;;
esac

if [[ ! -d "$WORKSPACE" ]]; then
    echo "Workspace directory does not exist: $WORKSPACE" >&2
    exit 1
fi

if [[ -z "${ASPIRE_PR_RECORDING_ACTIVE:-}" ]] && [[ -n "${ASPIRE_PR_RECORD:-}" ]] && is_truthy "${ASPIRE_PR_RECORD}"; then
    if ! command -v asciinema >/dev/null 2>&1; then
        echo "asciinema is required when ASPIRE_PR_RECORD is enabled." >&2
        exit 1
    fi

    recording_path="${ASPIRE_PR_RECORDING_PATH:-$WORKSPACE/recordings/$(date -u +%Y%m%dT%H%M%SZ)-$(get_recording_stem "$1").cast}"
    mkdir -p "$(dirname "$recording_path")"

    recording_command=("$SCRIPT_PATH" "$@")
    recording_command_string="$(printf '%q ' "${recording_command[@]}")"
    recording_command_string="${recording_command_string% }"

    export ASPIRE_PR_RECORDING_ACTIVE=1
    echo "Recording session to $recording_path" >&2

    recording_args=(
        record
        --return
        --command "$recording_command_string"
    )

    if [[ -n "${ASPIRE_PR_RECORDING_TITLE:-}" ]]; then
        recording_args+=(--title "$ASPIRE_PR_RECORDING_TITLE")
    fi

    recording_args+=("$recording_path")

    asciinema "${recording_args[@]}"
    exit $?
fi

if [[ -z "${GH_TOKEN:-}" && -z "${GITHUB_TOKEN:-}" ]]; then
    GH_TOKEN="$(gh auth token)"
    export GH_TOKEN
elif [[ -z "${GH_TOKEN:-}" ]]; then
    GH_TOKEN="$GITHUB_TOKEN"
    export GH_TOKEN
fi

tty_args=()
if [[ -t 0 && -t 1 ]]; then
    tty_args=(-it)
fi

run_args=(
    --rm
    -e GH_TOKEN
    -e ASPIRE_REPO
    -e INSTALL_PREFIX="$INSTALL_PREFIX"
    -e HOME=/workspace
    -u "$CONTAINER_USER"
    -v "$WORKSPACE:/workspace"
    -w /workspace
)

if [[ ${#tty_args[@]} -gt 0 ]]; then
    run_args+=("${tty_args[@]}")
fi

if [[ -e "$DOCKER_SOCKET_PATH" ]]; then
    DOCKER_SOCKET_REALPATH="$(python3 -c 'import os, sys; print(os.path.realpath(sys.argv[1]))' "$DOCKER_SOCKET_PATH")"
    if [[ -S "$DOCKER_SOCKET_REALPATH" ]]; then
        run_args+=(-v "$DOCKER_SOCKET_REALPATH:/var/run/docker.sock")
    fi
fi

docker build -t "$IMAGE_NAME" "$SCRIPT_DIR"
docker run "${run_args[@]}" "$IMAGE_NAME" "$@"
