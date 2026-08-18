#!/usr/bin/env bash
# detect-forge.sh — identify which software forge serves a host: Forgejo, Gitea,
# GitHub, or GitLab. Works from a URL, a bare host, or the current repo's remote.
#
# Usage:
#   detect-forge.sh                          # infer from `git remote get-url origin`
#   detect-forge.sh https://code.example.com
#   detect-forge.sh code.example.com/subpath
#   detect-forge.sh --remote upstream        # use another git remote
#   detect-forge.sh --token <t> <url>        # auth for REQUIRE_SIGNIN_VIEW instances
#   detect-forge.sh --json <url>             # machine-readable output
#
# Token is also read from $FORGE_TOKEN, $GITEA_TOKEN, or $FORGEJO_TOKEN.
#
# Exit codes: 0 = detected, 1 = unknown/unreachable, 2 = usage error.
#
# Detection logic (validated against Forgejo 14/15/16, Gitea 1.25/1.27,
# locked-down instances, and sub-path installs):
#   1. GET {base}/api/v1/version
#      200 + version contains "+gitea-"  -> Forgejo (reports Gitea compat suffix)
#      200 + plain "1.x.y" version       -> Gitea
#      401/403                           -> locked instance, go to step 2
#      404                               -> wrong base path, walk up one segment
#   2. GET {base}/api/forgejo/v1/version (route exists only on Forgejo)
#      200/401/403 -> Forgejo            404 -> Gitea
#   3. Confirm "likely" results against the HTML footer ("Powered by Forgejo" /
#      "Powered by Gitea"), which is served even on the login page.
#   4. GitLab: /api/v4/version route exists (401, not 404).
#      GitHub/GHES: x-github-request-id response header.

set -u

JSON=0
TOKEN="${FORGE_TOKEN:-${GITEA_TOKEN:-${FORGEJO_TOKEN:-}}}"
REMOTE="origin"
INPUT=""
TIMEOUT=10

while [ $# -gt 0 ]; do
  case "$1" in
    --json) JSON=1 ;;
    --token) shift; TOKEN="${1:-}" ;;
    --remote) shift; REMOTE="${1:-origin}" ;;
    --timeout) shift; TIMEOUT="${1:-10}" ;;
    -h|--help) sed -n '2,26p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "unknown option: $1" >&2; exit 2 ;;
    *) INPUT="$1" ;;
  esac
  shift
done

if [ -z "$INPUT" ]; then
  INPUT=$(git remote get-url "$REMOTE" 2>/dev/null) || {
    echo "no input URL and no git remote '$REMOTE' found" >&2; exit 2; }
fi

# --- normalize the input into an https base URL (path preserved) -------------
normalize() {
  local url="$1" host path
  case "$url" in
    ssh://*)
      # ssh://user@host:port/owner/repo.git -> host + path
      host="${url#ssh://}"; host="${host#*@}"
      path="/${host#*/}"; [ "$path" = "/$host" ] && path=""
      host="${host%%/*}"; host="${host%%:*}"
      ;;
    http://*|https://*)
      host="${url#*://}"
      path="/${host#*/}"; [ "$path" = "/$host" ] && path=""
      host="${host%%/*}"
      ;;
    *@*:*)
      # scp-style: user@host:owner/repo.git
      host="${url#*@}"; path="/${host#*:}"; host="${host%%:*}"
      ;;
    *)
      host="${url%%/*}"
      path="/${url#*/}"; [ "$path" = "/$url" ] && path=""
      ;;
  esac
  path="${path%.git}"; path="${path%/}"
  echo "https://$host$path"
}

FULL=$(normalize "$INPUT")

# Candidate API bases: strip path segments one at a time (repo, then owner, then
# any reverse-proxy sub-path) down to the bare host. Handles installs like
# https://host/gitea/ where the API lives at https://host/gitea/api/v1.
CANDIDATES=()
cur="$FULL"
while :; do
  CANDIDATES+=("$cur")
  base="${cur#https://}"
  case "$base" in
    */*) cur="https://${base%/*}" ;;
    *) break ;;
  esac
done

AUTH=()
[ -n "$TOKEN" ] && AUTH=(-H "Authorization: token $TOKEN")

http() { # http <url> -> sets BODY and CODE
  local tmp; tmp=$(mktemp)
  CODE=$(curl -sS -m "$TIMEOUT" -L "${AUTH[@]}" -o "$tmp" -w '%{http_code}' "$1" 2>/dev/null) || CODE=000
  BODY=$(head -c 2000 "$tmp"); rm -f "$tmp"
}

emit() { # emit <forge> <confidence> <version> <api_base> <method>
  if [ "$JSON" = 1 ]; then
    printf '{"forge":"%s","confidence":"%s","version":"%s","api_base":"%s","method":"%s"}\n' \
      "$1" "$2" "$3" "$4" "$5"
  else
    printf '%s (%s)  version=%s  api_base=%s  via=%s\n' "$1" "$2" "${3:-?}" "$4" "$5"
  fi
  [ "$1" = "unknown" ] && exit 1 || exit 0
}

html_brand() { # html_brand <base> -> forgejo|gitea|""
  local page
  page=$(curl -sS -m "$TIMEOUT" -L "$1/" 2>/dev/null | head -c 100000)
  case "$page" in
    *"Powered by Forgejo"*|*'content="Forgejo'*) echo forgejo ;;
    *"Powered by Gitea"*|*'content="Gitea'*) echo gitea ;;
    *) echo "" ;;
  esac
}

version_of() { # extract "version":"..." from BODY
  printf '%s' "$BODY" | sed -n 's/.*"version" *: *"\([^"]*\)".*/\1/p'
}

for base in "${CANDIDATES[@]}"; do
  http "$base/api/v1/version"
  case "$CODE" in
    200)
      ver=$(version_of)
      [ -z "$ver" ] && continue  # 200 but not a version JSON (e.g. SPA fallback page)
      case "$ver" in
        *+gitea-*) emit forgejo confirmed "$ver" "$base" "api/v1/version compat suffix" ;;
        *) # plain version: Gitea, unless the Forgejo-only route also answers
           http "$base/api/forgejo/v1/version"
           if [ "$CODE" = 200 ]; then
             emit forgejo confirmed "$ver" "$base" "api/forgejo/v1/version"
           fi
           emit gitea confirmed "$ver" "$base" "api/v1/version plain version" ;;
      esac
      ;;
    401|403)
      # API exists but is locked (REQUIRE_SIGNIN_VIEW). The Forgejo-only route
      # 404s on Gitea even when locked, so its status disambiguates.
      http "$base/api/forgejo/v1/version"
      case "$CODE" in
        200) emit forgejo confirmed "$(version_of)" "$base" "api/forgejo/v1/version (authed)" ;;
        401|403) guess=forgejo ;;
        *) guess=gitea ;;
      esac
      # A 401/403 on every path can also be GitLab (or a proxy) rejecting
      # unauthenticated traffic wholesale; the /api/v4 route disambiguates.
      http "$base/api/v4/version"
      case "$CODE" in
        200|401) emit gitlab confirmed "$(version_of)" "$base" "api/v4/version status $CODE" ;;
      esac
      brand=$(html_brand "$base")
      if [ -n "$brand" ]; then
        emit "$brand" confirmed "" "$base" "locked api + html branding"
      fi
      emit "$guess" likely "" "$base" "locked api, forgejo-route status $CODE"
      ;;
    *) : ;; # 404/000: wrong base or unreachable -> walk up
  esac
done

# Not Gitea/Forgejo. Check GitLab and GitHub signatures on the host root.
ROOT="${CANDIDATES[${#CANDIDATES[@]}-1]}"
http "$ROOT/api/v4/version"
case "$CODE" in
  200|401) emit gitlab confirmed "" "$ROOT" "api/v4/version status $CODE" ;;
esac
if curl -sSI -m "$TIMEOUT" "$ROOT/" 2>/dev/null | grep -qi '^x-github-request-id:'; then
  emit github confirmed "" "$ROOT" "x-github-request-id header"
fi
brand=$(html_brand "$ROOT")
[ -n "$brand" ] && emit "$brand" likely "" "$ROOT" "html branding only"
emit unknown none "" "$ROOT" "no signature matched"
