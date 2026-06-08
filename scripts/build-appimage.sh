#!/usr/bin/env bash
# Build a self-contained Difflection AppImage from a linux-x64 publish output.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT/artifacts/linux-x64}"
APPDIR="${APPDIR:-$ROOT/artifacts/Difflection.AppDir}"
APPIMAGE_OUT="${APPIMAGE_OUT:-$ROOT/artifacts/difflection-x86_64.AppImage}"
APPIMAGETOOL="${APPIMAGETOOL:-$ROOT/artifacts/appimagetool-x86_64.AppImage}"
APPIMAGETOOL_VERSION="12"
APPIMAGETOOL_SHA256="d918b4df547b388ef253f3c9e7f6529ca81a885395c31f619d9aaf7030499a13"
APPIMAGETOOL_URL="https://github.com/AppImage/AppImageKit/releases/download/${APPIMAGETOOL_VERSION}/appimagetool-x86_64.AppImage"
SKIP_PUBLISH=false

usage() {
    cat <<'EOF'
Usage: scripts/build-appimage.sh [--skip-publish]

  --skip-publish   Use an existing publish folder (PUBLISH_DIR, default artifacts/linux-x64)

Environment:
  PUBLISH_DIR      dotnet publish output directory
  APPDIR           AppDir staging path
  APPIMAGE_OUT     Output AppImage path
  APPIMAGETOOL     Path to appimagetool AppImage (downloaded on first run)
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-publish) SKIP_PUBLISH=true; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage; exit 1 ;;
    esac
done

if [[ "$SKIP_PUBLISH" == false ]]; then
    mkdir -p "$PUBLISH_DIR"
    dotnet publish "$ROOT/Difflection.Desktop/Difflection.Desktop.csproj" \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:DebugType=None \
        -p:DebugSymbol=false \
        --output "$PUBLISH_DIR"
fi

if [[ ! -f "$PUBLISH_DIR/Difflection.Desktop" ]]; then
    echo "Expected publish binary at $PUBLISH_DIR/Difflection.Desktop" >&2
    exit 1
fi

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"

cp "$PUBLISH_DIR/Difflection.Desktop" "$APPDIR/usr/bin/difflection"
chmod +x "$APPDIR/usr/bin/difflection"

cat > "$APPDIR/difflection.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Difflection
Comment=Visual regression review and image comparison
Exec=difflection
Icon=difflection
Categories=Graphics;Development;
Terminal=false
EOF

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "${HERE}/usr/bin/difflection" "$@"
EOF
chmod +x "$APPDIR/AppRun"

mkdir -p "$ROOT/artifacts"
if [[ ! -x "$APPIMAGETOOL" ]]; then
    curl -fsSL -o "$APPIMAGETOOL" "$APPIMAGETOOL_URL"
    chmod +x "$APPIMAGETOOL"
fi
echo "$APPIMAGETOOL_SHA256  $APPIMAGETOOL" | sha256sum -c -

ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$APPIMAGE_OUT"
echo "Wrote $APPIMAGE_OUT"
