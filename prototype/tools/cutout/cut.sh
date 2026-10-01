#!/bin/bash
# Cut a portrait shot on a pure-black backdrop into the three site images.
# usage: prototype/tools/cutout/cut.sh <photo> [out dir, default prototype/assets/img]
#   -> omar-cutout.webp (transparent), omar-avatar.webp (240 px head), omar.jpg (original)
# Needs macOS 14+ (Vision), Xcode command line tools (swiftc) and python3 with Pillow.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
src="$1"; out="${2:-$here/../../assets/img}"
work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT
for t in masks matte; do [ "$here/$t" -nt "$here/$t.swift" ] || swiftc -O "$here/$t.swift" -o "$here/$t"; done
"$here/masks" "$src" "$work"                                      # Vision: subject + person masks
"$here/matte" "$src" "$work/mask_person.png" "$work/matte.rgba"   # exact edge coverage on black
python3 - "$src" "$work/matte.rgba" "$out" <<'PY'
import sys
from PIL import Image
src, raw, out = sys.argv[1:]
photo = Image.open(src).convert('RGB'); w, h = photo.size
cut = Image.frombytes('RGBA', (w, h), open(raw, 'rb').read())
cut.save(f'{out}/omar-cutout.webp', 'WEBP', quality=88, alpha_quality=100, method=6)
band = cut.getchannel('A').crop((0, 0, w, int(h * 0.26))).getbbox()      # the head
cx, side = (band[0] + band[2]) // 2 + 10, int(h * 0.36)
cut.crop((cx - side // 2, 0, cx + side // 2, side)).resize((240, 240), Image.LANCZOS) \
   .save(f'{out}/omar-avatar.webp', 'WEBP', quality=90, alpha_quality=100, method=6)
photo.save(f'{out}/omar.jpg', 'JPEG', quality=86, optimize=True, progressive=True)
print('written to', out)
PY
