#!/usr/bin/env bash
set -euo pipefail
exec </dev/null
repo=$(cygpath -u "$1")
label=$2
[[ "$label" =~ ^[A-Za-z0-9_-]+$ ]] || exit 2
root="$repo/.local/native-build"
recipe=$(cygpath -u "$3")
work="$root/runs/$label"
[[ ! -e "$work" ]] || { printf 'Refusing to reuse non-clean build: %s\n' "$work"; exit 2; }
mkdir -p "$work/src" "$work/prefix" "$work/runtime" "$work/audit"
exec >"$work/build.log" 2>&1
set -x
prefix="$work/prefix"
export PATH="$prefix/bin:/ucrt64/bin:/usr/bin:/c/Windows/System32"
export PKG_CONFIG_LIBDIR="$prefix/lib/pkgconfig"
export PKG_CONFIG_PATH="$PKG_CONFIG_LIBDIR"
export CONFIG_SITE=/dev/null
export CC=gcc CXX=g++ AR=ar RANLIB=ranlib
export CFLAGS="-O2 -D_WIN32_WINNT=0x0A00 -ffunction-sections -fdata-sections -ffile-prefix-map=$work=/cardryft-build -fdebug-prefix-map=$work=/cardryft-build -fmacro-prefix-map=$work=/cardryft-build"
export CXXFLAGS="$CFLAGS"
export LDFLAGS="-Wl,--no-insert-timestamp -Wl,--build-id=none -Wl,--gc-sections -static-libgcc"
export AUTOCONF_VERSION=2.73 AUTOMAKE_VERSION=1.18
gcc --version
ld --version
perl -v
make --version
cp "$recipe/toolchain-lock.json" "$work/audit/toolchain-lock.json"
cp "$recipe/sources-lock.json" "$work/audit/sources-lock.json"
tar -xf "$root/downloads/openssl-3.6.5.tar.gz" -C "$work/src"
cd "$work/src/openssl-3.6.5"
patch --batch --fuzz=0 -p1 <"$recipe/patches/openssl-build-info.patch"
patch --batch --fuzz=0 -p1 <"$recipe/patches/openssl-system-rng.patch"
# No engine/module/DSO/config/compression path. Default provider stays built in.
# No security-level, verification or cipher downgrade is applied by this recipe.
perl Configure mingw64 shared no-autoload-config no-dso no-module no-engine no-comp no-zlib no-legacy no-sock no-apps no-tests --with-rand-seed=os --prefix=/cardryft-native --libdir=lib --openssldir=/cardryft-native/no-config -lbcrypt
# Build shared runtime/import libraries and their actual prerequisites only.
# Upstream build_libs otherwise also compiles unused static libcrypto/libssl.
make -j4 build_libs LIBS=
make DESTDIR="$prefix" LIBS= INSTALL_LIBS= install_dev
mv "$prefix/cardryft-native/"* "$prefix/"
rmdir "$prefix/cardryft-native"
# OpenSSL pkgconfig uses the configured prefix. Make it describe this build's staging path.
sed -i "s|^prefix=.*|prefix=$prefix|" "$prefix/lib/pkgconfig/"*.pc
cp configdata.pm "$work/audit/openssl-configdata.pm"
for archive in libplist-2.7.0 libimobiledevice-glue-1.3.2 libusbmuxd-2.1.1 libimobiledevice-1.4.0; do
    tar -xf "$root/downloads/$archive.tar.bz2" -C "$work/src"
    cd "$work/src/$archive"
    if [[ "$archive" == libimobiledevice-glue-* ]]; then patch --batch --fuzz=0 -p1 <"$recipe/patches/glue-windows-library-case.patch"; fi
    if [[ "$archive" == libusbmuxd-* ]]; then patch --batch --fuzz=0 -p1 <"$recipe/patches/libusbmuxd-windows-library-case.patch"; fi
    options=(--host=x86_64-w64-mingw32 --prefix="$prefix" --enable-shared --disable-static)
    if [[ "$archive" == libplist-* ]]; then options+=(--without-cython --without-tests); fi
    if [[ "$archive" == libimobiledevice-1.4.0 ]]; then
        patch --batch --fuzz=0 -p1 <"$recipe/patches/libimobiledevice-minimal.patch"
        patch --batch --fuzz=0 -p1 <"$recipe/patches/libimobiledevice-windows-exports.patch"
        cp "$recipe/patches/cardryft.exports" cardryft.exports
        autoreconf -fi
        options+=(--without-cython --without-readline --disable-wireless-pairing --disable-debug --with-openssl --without-gnutls --without-mbedtls)
    fi
    ./configure "${options[@]}"
    if [[ "$archive" == libplist-* ]]; then
        make -j4 -C libcnary
        make -j4 -C src libplist-2.0.la
        make -C src lib_LTLIBRARIES=libplist-2.0.la install-libLTLIBRARIES install-pkgconfigDATA
        make -C include install
    elif [[ "$archive" == libimobiledevice-1.4.0 ]]; then
        make -j4
        make install
    else
        make -j4 -C src
        make -C src install
        make -C include install
    fi
    cp config.log "$work/audit/$archive-config.log"
    case "$archive" in
        libplist-*) expected=libplist-2.0.dll ;;
        libimobiledevice-glue-*) expected=libimobiledevice-glue-1.0.dll ;;
        libusbmuxd-*) expected=libusbmuxd-2.0.dll ;;
        libimobiledevice-1.4.0) expected=libimobiledevice-1.0.dll ;;
    esac
    [[ -f "$prefix/bin/$expected" ]] || { printf 'Required shared DLL missing: %s\n' "$expected"; exit 2; }
done
find "$prefix/bin" -name '*.dll' -exec cp {} "$work/runtime/" \;
for dll in "$work/runtime/"*.dll; do
    objdump -p "$dll" >"$work/audit/$(basename "$dll").objdump.txt"
done
printf 'Build complete: %s\n' "$label"
