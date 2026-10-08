#!/usr/bin/env bash
set -euo pipefail
repo=$(cygpath -u "$1")
label="$2"
[[ "$label" =~ ^[A-Za-z0-9_-]+$ ]] || exit 2
root="$repo/.local/native-build"
work="$root/replacements/$label"
[[ ! -e "$work" ]] || { printf 'Refusing to overwrite replacement work\n'; exit 2; }
mkdir -p "$work/src" "$work/prefix" "$work/audit"
exec >"$work/build.log" 2>&1
set -x
export CONFIG_SITE=/dev/null
export CC=gcc CXX=g++ AR=ar RANLIB=ranlib
export CFLAGS="-O2 -D_WIN32_WINNT=0x0A00 -ffunction-sections -fdata-sections -ffile-prefix-map=$work=/cardryft-build -fdebug-prefix-map=$work=/cardryft-build -fmacro-prefix-map=$work=/cardryft-build"
export CXXFLAGS="$CFLAGS"
export LDFLAGS="-Wl,--no-insert-timestamp -Wl,--build-id=none -Wl,--gc-sections -static-libgcc"
export AUTOCONF_VERSION=2.73 AUTOMAKE_VERSION=1.18
export PKG_CONFIG_LIBDIR="$work/prefix/lib/pkgconfig"
export PKG_CONFIG_PATH="$PKG_CONFIG_LIBDIR"
tar -xf "$root/downloads/libplist-2.7.0.tar.bz2" -C "$work/src"
cd "$work/src/libplist-2.7.0"
patch --batch --fuzz=0 -p1 <"$repo/native-build/patches/libplist-cardryft-bounds.patch"
# Demonstration recipient modification: compatible API, observably different DLL.
# This does not change limits/parsing semantics or touch official runtime pins.
sed -i 's/return PACKAGE_VERSION;/return PACKAGE_VERSION "-recipient-rebuild";/' src/plist.c
grep -q 'return PACKAGE_VERSION "-recipient-rebuild";' src/plist.c
./configure --host=x86_64-w64-mingw32 --prefix="$work/prefix" --enable-shared --disable-static --without-cython --without-tests
make -j4 -C libcnary
make -j4 -C src libplist-2.0.la
make -C src lib_LTLIBRARIES=libplist-2.0.la install-libLTLIBRARIES install-pkgconfigDATA
make -C include install
cp config.log "$work/audit/config.log"
printf 'Compatible recipient-modified libplist compiled\n'
