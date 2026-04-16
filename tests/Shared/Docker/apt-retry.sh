#!/bin/sh

set -eu

attempt=1
max_attempts="${APT_RETRY_ATTEMPTS:-5}"
delay_seconds="${APT_RETRY_DELAY_SECONDS:-10}"

while :; do
  if "$@"; then
    exit 0
  fi

  if [ "$attempt" -ge "$max_attempts" ]; then
    echo "Command failed after ${attempt} attempts: $*" >&2
    exit 1
  fi

  echo "Command failed (attempt ${attempt}/${max_attempts}), retrying: $*" >&2
  attempt=$((attempt + 1))
  rm -rf /var/lib/apt/lists/*
  sleep "$delay_seconds"
done
