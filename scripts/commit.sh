#!/usr/bin/env bash
set -euo pipefail

# ── Help ──────────────────────────────────────────────────────────
if [ "${1:-}" = "-h" ] || [ "${1:-}" = "--help" ]; then
  echo "Usage: ./scripts/commit.sh [-n|--dry-run]"
  echo ""
  echo "Commits staged changes with a conventional-commit message."
  echo "  Type      → derived from branch prefix (e.g. feat/, fix/, chore/)"
  echo "  Description → auto-generated from staged file names"
  echo ""
  echo "Options:"
  echo "  -n, --dry-run   Show the commit message without committing"
  exit 0
fi

DRY_RUN=false
if [ "${1:-}" = "-n" ] || [ "${1:-}" = "--dry-run" ]; then
  DRY_RUN=true
fi

# ── 1. Check for staged changes ───────────────────────────────────
STAGED_FILES=$(git diff --cached --name-only)
if [ -z "$STAGED_FILES" ]; then
  echo "No staged changes to commit."
  exit 0
fi

FILE_COUNT=$(echo "$STAGED_FILES" | wc -l | tr -d ' ')

# ── 2. Determine commit type from branch name ─────────────────────
BRANCH=$(git branch --show-current)
TYPE="${BRANCH%%/*}"   # everything before the first /

case "$TYPE" in
  feat|fix|chore|docs|test|refactor|perf|ci|style|build) ;;
  *) TYPE="chore" ;;
esac

# ── 3. Generate description from staged file paths ─────────────────
generate_description() {
  local files="$1"
  local count="$2"

  if [ "$count" -eq 1 ]; then
    local file
    file=$(echo "$files" | head -1)
    local name
    name=$(basename "$file")
    # Strip extension unless it's a dotfile (e.g. .gitignore)
    if [[ "$name" != .* ]]; then
      name="${name%.*}"
    fi
    echo "update $name"
    return
  fi

  # Multiple files — group by first path component (area/module)
  # Files without '/' are treated as area "root"
  local areas
  areas=$(echo "$files" | awk -F'/' '{if (NF>1) print $1; else print "."}' | sort -u)
  local area_count
  area_count=$(echo "$areas" | wc -l | tr -d ' ')

  if [ "$area_count" -eq 1 ]; then
    local area
    area=$(echo "$areas" | head -1)
    [ "$area" = "." ] && area="root"
    echo "update $area ($count files)"
  else
    # Replace '.' with 'root' in the list
    areas=$(echo "$areas" | sed 's/^\.$/root/')
    echo "update $(echo "$areas" | paste -sd ',' -) ($count files)"
  fi
}

DESC=$(generate_description "$STAGED_FILES" "$FILE_COUNT")
MESSAGE="${TYPE}: ${DESC}"

# ── 4. Commit ─────────────────────────────────────────────────────
if [ "$DRY_RUN" = true ]; then
  echo "[dry-run] Would commit: $MESSAGE"
  echo ""
  echo "Staged files:"
  echo "$STAGED_FILES" | sed 's/^/  /'
else
  echo "Committing: $MESSAGE"
  git commit -m "$MESSAGE"
fi
