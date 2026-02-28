@echo off
chcp 65001 >nul

set TARGET_DIR=HDiffPatch

%~d0

:: Check and create directory
if not exist "%TARGET_DIR%" (
    echo Target folder does not exist, creating: %TARGET_DIR%
    md "%TARGET_DIR%"
) else (
    echo Target folder already exists: %TARGET_DIR%
)

cd /d "%TARGET_DIR%"
echo Current working directory switched to: %cd%

echo Starting to clone repositories...

set repolibmd5=https://github.com/sisong/libmd5.git
set repoxxHash=https://github.com/sisong/xxHash.git
set repolzma=https://github.com/sisong/lzma.git
set repozstd=https://github.com/sisong/zstd.git
set repozlib=https://github.com/sisong/zlib.git
set repolibdeflate=https://github.com/sisong/libdeflate.git
set repobzip2=https://github.com/sisong/bzip2.git

:: Clone HDiffPatch (special case with branch and depth)
if not exist "HDiffPatch" (
    echo Cloning HDiffPatch...
    git clone --depth 1 -b v4.12.1 https://github.com/sisong/HDiffPatch.git
) else (
    echo HDiffPatch already exists, skipping.
)

:: Clone libmd5
if not exist "libmd5" (
    echo Cloning libmd5...
    git clone --depth 1 %repolibmd5%
) else (
    echo libmd5 already exists, skipping.
)

:: Clone xxHash
if not exist "xxHash" (
    echo Cloning xxHash...
    git clone --depth 1 %repoxxHash%
) else (
    echo xxHash already exists, skipping.
)

:: Clone lzma
if not exist "lzma" (
    echo Cloning lzma...
    git clone --depth 1 %repolzma%
) else (
    echo lzma already exists, skipping.
)

:: Clone zstd
if not exist "zstd" (
    echo Cloning zstd...
    git clone --depth 1 %repozstd%
) else (
    echo zstd already exists, skipping.
)

:: Clone zlib
if not exist "zlib" (
    echo Cloning zlib...
    git clone --depth 1 %repozlib%
) else (
    echo zlib already exists, skipping.
)

:: Clone libdeflate
if not exist "libdeflate" (
    echo Cloning libdeflate...
    git clone --depth 1 %repolibdeflate%
) else (
    echo libdeflate already exists, skipping.
)

:: Clone bzip2
if not exist "bzip2" (
    echo Cloning bzip2...
    git clone --depth 1 %repobzip2%
) else (
    echo bzip2 already exists, skipping.
)

echo.
echo Applying patches...
copy /Y "..\patches\libhdiffz.h" "HDiffPatch\libhdiffz.h"
copy /Y "..\patches\libhdiffz.cpp" "HDiffPatch\libhdiffz.cpp"
copy /Y "..\patches\HDiffZ.vcxproj" "HDiffPatch\builds\vc\HDiffZ.vcxproj"

echo.
echo Building hdiffz.dll Release x64...
msbuild "HDiffPatch\builds\vc\HDiffZ.vcxproj" "/p:Configuration=Release" "/p:Platform=x64" /m
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Copying hdiffz.dll to BundleDiff.Editor\libs\windows\x64\
if not exist "..\BundleDiff.Editor\libs\windows\x64" md "..\BundleDiff.Editor\libs\windows\x64"
copy /Y "HDiffPatch\builds\vc\Release\x64\hdiffz.dll" "..\BundleDiff.Editor\libs\windows\x64\hdiffz.dll"

echo.
echo All operations completed.
pause