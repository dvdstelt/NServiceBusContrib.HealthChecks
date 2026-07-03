#!/usr/bin/env bash
#
# Deploy NServiceBusContrib.WarmUp to NuGet.
#
# Usage:
#   NUGET_API_KEY=<key> ./deploy-warmup.sh (-major | -minor | -patch) [options]
#
# Exactly one of -major / -minor / -patch is required. It bumps that part of the
# package's current <Version> (SemVer), builds a Release package with the new
# version, and pushes it to NuGet. The bumped <Version> stays in the .csproj only
# after a successful push; on a dry run or any failure the .csproj is restored.
#
# Options:
#   --dry-run           build the package but do not push (leaves the .csproj unchanged)
#   --source <url>      NuGet feed (default: https://api.nuget.org/v3/index.json, or $NUGET_SOURCE)
#   --api-key <key>     NuGet API key (default: $NUGET_API_KEY)
#
set -euo pipefail

PROJECT="src/NServiceBusContrib.WarmUp/NServiceBusContrib.WarmUp.csproj"
PACKAGE_ID="NServiceBusContrib.WarmUp"
SOLUTION="src/NServiceBusContrib.slnx"

cd "$(dirname "$0")"

bump=""
dry_run=false
source_url="${NUGET_SOURCE:-https://api.nuget.org/v3/index.json}"
api_key="${NUGET_API_KEY:-}"

die() { echo "error: $*" >&2; exit 1; }
set_bump() { [ -z "$bump" ] || die "specify only one of -major / -minor / -patch"; bump="$1"; }

while [ $# -gt 0 ]; do
  case "$1" in
    -major|--major) set_bump major ;;
    -minor|--minor) set_bump minor ;;
    -patch|--patch) set_bump patch ;;
    --dry-run)      dry_run=true ;;
    --source)       shift; source_url="${1:?--source needs a value}" ;;
    --api-key)      shift; api_key="${1:?--api-key needs a value}" ;;
    -h|--help)      sed -n '2,19p' "$0"; exit 0 ;;
    *)              die "unknown argument: $1 (expected -major | -minor | -patch)" ;;
  esac
  shift
done

[ -n "$bump" ] || die "one of -major / -minor / -patch is required"
[ -f "$PROJECT" ] || die "project not found: $PROJECT"
$dry_run || [ -n "$api_key" ] || die "no API key: set NUGET_API_KEY or pass --api-key (or use --dry-run)"

# --- compute the new version from the current <Version> ---
current="$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT" | head -1 || true)"
current="${current:-0.0.0}"
IFS='.' read -r major minor patch <<< "$current"
major="${major:-0}"; minor="${minor:-0}"; patch="${patch%%[!0-9]*}"; patch="${patch:-0}"
case "$bump" in
  major) major=$((major + 1)); minor=0; patch=0 ;;
  minor) minor=$((minor + 1)); patch=0 ;;
  patch) patch=$((patch + 1)) ;;
esac
new="$major.$minor.$patch"
echo ">> $PACKAGE_ID: $current -> $new ($bump)"

# --- set the version in the .csproj; restore it unless the push succeeds ---
backup="$(mktemp)"; cp "$PROJECT" "$backup"
keep_version=false
cleanup() { $keep_version || cp "$backup" "$PROJECT"; rm -f "$backup"; }
trap cleanup EXIT
sed -i "s#<Version>[^<]*</Version>#<Version>$new</Version>#" "$PROJECT"

# --- test, then pack (uses the .csproj version; no global override that would leak
#     into project references) ---
echo ">> running tests"
dotnet test "$SOLUTION" -c Release --nologo
echo ">> packing $PACKAGE_ID $new"
out="artifacts"
rm -f "$out/$PACKAGE_ID."*.nupkg "$out/$PACKAGE_ID."*.snupkg
dotnet pack "$PROJECT" -c Release -o "$out" --nologo
package="$out/$PACKAGE_ID.$new.nupkg"
[ -f "$package" ] || die "expected package not found: $package"

if $dry_run; then
  echo ">> dry run: built $package (not pushed, .csproj restored)"
  exit 0
fi

# --- push, then keep the bumped version in source ---
echo ">> pushing $package -> $source_url"
dotnet nuget push "$package" --api-key "$api_key" --source "$source_url" --skip-duplicate
keep_version=true
echo ">> published $PACKAGE_ID $new"
echo ">> next: git commit -m \"Release $PACKAGE_ID $new\" -- $PROJECT   (tag if you like)"
