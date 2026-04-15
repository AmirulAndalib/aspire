#!/usr/bin/env bash

set -euo pipefail

usage() {
    cat <<'EOF'
Usage:
  aspire-pr-entrypoint.sh PR_NUMBER [get-aspire-cli-pr.sh options...]
  aspire-pr-entrypoint.sh bash

Environment:
  GH_TOKEN / GITHUB_TOKEN  GitHub token used by gh inside the container
  INSTALL_PREFIX           Install location passed to get-aspire-cli-pr.sh
                           Default: /workspace/.aspire
  ASPIRE_REPO              Optional repo override, defaults to microsoft/aspire

Defaults added automatically:
  --install-path <INSTALL_PREFIX>
  --skip-extension
  --skip-path
EOF
}

configure_nuget_source() {
    local pr_number="$1"
    local hive_dir="$install_prefix/hives/pr-$pr_number/packages"
    local nuget_config_dir="${HOME}/.nuget/NuGet"
    local nuget_config_path="${nuget_config_dir}/NuGet.Config"

    if [[ ! -d "$hive_dir" ]]; then
        echo "PR hive not found at $hive_dir; skipping NuGet source configuration." >&2
        return 0
    fi

    mkdir -p "$nuget_config_dir"
    cat > "$nuget_config_path" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="aspire-pr-hive" value="$hive_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
EOF

    echo "Configured NuGet sources in $nuget_config_path" >&2
}

has_arg() {
    local needle="$1"
    shift
    local arg
    for arg in "$@"; do
        if [[ "$arg" == "$needle" ]]; then
            return 0
        fi
    done
    return 1
}

if [[ $# -eq 0 ]]; then
    usage
    exit 1
fi

case "$1" in
    bash|sh)
        exec "$@"
        ;;
    -h|--help)
        usage
        exit 0
        ;;
esac

if [[ -z "${GH_TOKEN:-}" && -n "${GITHUB_TOKEN:-}" ]]; then
    export GH_TOKEN="$GITHUB_TOKEN"
fi

if [[ -z "${GH_TOKEN:-}" ]]; then
    echo "GH_TOKEN or GITHUB_TOKEN must be set." >&2
    exit 1
fi

if [[ -z "${HOME:-}" || ! -d "${HOME}" || ! -w "${HOME}" ]]; then
    export HOME=/tmp/home
    mkdir -p "$HOME"
fi

install_prefix="${INSTALL_PREFIX:-/workspace/.aspire}"
mkdir -p "$install_prefix"

args=("$@")

if ! has_arg "--install-path" "${args[@]}" && ! has_arg "-i" "${args[@]}"; then
    args+=(--install-path "$install_prefix")
fi

if ! has_arg "--skip-extension" "${args[@]}"; then
    args+=(--skip-extension)
fi

if ! has_arg "--skip-path" "${args[@]}"; then
    args+=(--skip-path)
fi

/usr/local/bin/get-aspire-cli-pr.sh "${args[@]}"

if [[ "$1" =~ ^[0-9]+$ ]]; then
    configure_nuget_source "$1"
fi
