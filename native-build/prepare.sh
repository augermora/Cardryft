#!/usr/bin/env bash
set -euo pipefail
repo=$(cygpath -u "$1")
root="$repo/.local/native-build"
tool="$root/toolchain/msys64"
exec >"$root/prepare.log" 2>&1
set -x
while IFS= read -r package; do
    package=${package%$'\r'}
    [[ "$package" =~ ^[A-Za-z0-9._+~-]+$ ]] || exit 2
    bsdtar -xf "$root/downloads/$package" -C "$tool" --exclude=.INSTALL --exclude=.MTREE --exclude=.BUILDINFO --exclude=.PKGINFO
done <"$root/package-list.txt"
# Native MinGW executables cannot use MSYS emulated symlinks. Resolve only links
# whose targets stay in the prepared tree, and replace them with ordinary copies.
while IFS= read -r -d '' link; do
    target=$(cygpath -u "$(cygpath -w "$(readlink -f "$link")")")
    [[ "$target" == "$tool/"* && -f "$target" ]] || { printf 'Invalid toolchain link: %s\n' "$link"; exit 2; }
    temporary="$link.cardryft-copy"
    cp "$target" "$temporary"
    rm "$link"
    mv "$temporary" "$link"
done < <(find "$tool/ucrt64" "$tool/usr/bin" -type l -print0)
