#!/usr/bin/env bash
# Builds ImageToIcon into bin/<configuration>/.
# Debug: linux-x64 only. Release: linux-x64 and win-x64 share the same output directory by design.
# Usage: $0 [Debug|Release]

set -euo pipefail

# ── Colors ────────────────────────────────────────────────────────────────────

nc='\033[0m'
red='\033[0;31m'
green='\033[0;32m'
white='\033[1;37m'
ol='\033[53m'
ul='\033[4m'

# ── Args ──────────────────────────────────────────────────────────────────────

configuration="Debug"

while [[ $# -gt 0 ]]; do
    case "$1" in
        Debug|Release) configuration="$1" ;;
        --help|-h)
            echo -e "
${ul}${white}Usage${nc}
  $(basename "$0") [Debug|Release]

${ul}${white}Options${nc}
  ${green}Debug${nc}     Build with debug symbols (default).
  ${green}Release${nc}   Build optimised release output.
  ${green}--help, -h${nc}  Show this help text and exit.
"
            exit 0 ;;
        *) echo -e "${red}[ERROR]${nc} Unknown argument: $1"; exit 1 ;;
    esac
    shift
done

# ── Dependency check ──────────────────────────────────────────────────────────

for cmd in dotnet; do
    if ! command -v "$cmd" &>/dev/null; then
        echo -e "${red}[ERROR]${nc} Executable not found: $cmd"
        exit 1
    fi
done

# ── Paths ─────────────────────────────────────────────────────────────────────

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$REPO_ROOT/src/ImageToIcon/ImageToIcon.csproj"
OUT_BASE="$REPO_ROOT/bin/$configuration"

echo -e "\n${ul}${white}Building ${green}${ul}ImageToIcon [$configuration]${white}${ul}...${nc}\n"

# ── Publish ───────────────────────────────────────────────────────────────────

publish() {
    local rid="$1"

    echo -e "${white}Platform:${nc} $rid"

    local extra=()
    if [[ "$configuration" == "Release" ]]; then
        extra+=(-p:DebugType=none -p:DebugSymbols=false)
    fi

    dotnet publish "$CSPROJ" \
        --configuration "$configuration" \
        --runtime "$rid" \
        --output "$OUT_BASE" \
        --nologo \
        -v minimal \
        "${extra[@]}"

    if [[ "$configuration" == "Release" ]]; then
        find "$OUT_BASE" -name "*.pdb" -delete
    fi
}

if [[ "$configuration" == "Debug" ]]; then
    publish linux-x64
else
    publish linux-x64
    publish win-x64
fi

echo -e "\n${green}${ol}$(basename "$0")${white}${ol} done!${nc}"
echo -e "  Output: ${green}$OUT_BASE/${nc}\n"
