#!/usr/bin/env bash
# Checks that a pull request title follows Conventional Commits, which GitVersion (GitVersion.yml) uses to
# bump the version when the PR is squash-merged:
#   feat!: ... or fix(git)!: ...  breaking change -> major
#   feat: ...                     new feature     -> minor
#   fix:, docs:, chore:, ...      anything else   -> patch
#
#   scripts/check-pr-title.sh "<title>"
set -euo pipefail

title="$1"
types='build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test'

if [[ ! "$title" =~ ^($types)(\([a-z0-9-]+\))?!?:\ .+ ]]; then
  echo "::error title=PR title::'$title' doesn't start with a Conventional Commits prefix such as 'feat: ', 'fix(git): ' or 'feat!: ' (breaking). Allowed types: ${types//|/, }."
  exit 1
fi

case "$title" in
  *!:*) bump="major" ;;
  feat*) bump="minor" ;;
  *) bump="patch" ;;
esac
echo "Merging this PR raises the $bump version."
