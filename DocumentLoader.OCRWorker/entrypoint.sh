#!/bin/sh
set -eu

echo "[entrypoint] Starting OCRWorker..."

# Find real library locations
LEPT="$(ldconfig -p | awk '/liblept\.so/{print $NF; exit}')"
TESS="$(ldconfig -p | awk '/libtesseract\.so/{print $NF; exit}')"

echo "[entrypoint] Leptonica: $LEPT"
echo "[entrypoint] Tesseract : $TESS"

# 1) Stable system folder
mkdir -p /usr/local/lib/ocr
ln -sf "$LEPT" /usr/local/lib/ocr/libleptonica-1.82.0.so
ln -sf "$TESS" /usr/local/lib/ocr/libtesseract50.so

# 2) App-local locations (InteropDotNet / Tesseract.NET commonly searches these)
APPDIR="/app"
mkdir -p "$APPDIR" \
         "$APPDIR/x64" \
         "$APPDIR/runtimes/linux-x64/native" \
         "$APPDIR/runtimes/linux/native"

for d in "$APPDIR" "$APPDIR/x64" "$APPDIR/runtimes/linux-x64/native" "$APPDIR/runtimes/linux/native"; do
  ln -sf "$LEPT" "$d/libleptonica-1.82.0.so" 2>/dev/null || true
  ln -sf "$TESS" "$d/libtesseract50.so"       2>/dev/null || true
done

# Ensure the dynamic loader also sees our stable folder
export LD_LIBRARY_PATH="/usr/local/lib/ocr:/app:/app/x64:/app/runtimes/linux-x64/native:${LD_LIBRARY_PATH:-}"
echo "[entrypoint] LD_LIBRARY_PATH=$LD_LIBRARY_PATH"

# Print what we created (helps debugging)
ls -la /usr/local/lib/ocr || true
ls -la /app/runtimes/linux-x64/native 2>/dev/null || true
ls -la /app/x64 2>/dev/null || true

exec dotnet DocumentLoader.OCRWorker.dll
