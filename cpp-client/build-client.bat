set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
set VCPKG_ROOT=%DHSRC%\vcpkg
mkdir %DHSRC%
mkdir %DHINSTALL%

echo *** CLONING REPOSITORIES ***
cd /d %DHSRC%
git clone https://github.com/microsoft/vcpkg.git

echo *** WARNING FIX THIS REPOSITORY ***
echo *** WARNING FIX THIS REPOSITORY ***
echo *** WARNING FIX THIS REPOSITORY ***
git clone -b kosak_kosak-todo-fixes https://github.com/kosak/deephaven-core.git

echo *** BOOTSTRAPPING VCPKG ***
cd /d %VCPKG_ROOT%
call .\bootstrap-vcpkg.bat

echo *** BUILDING DEPENDENT LIBRARIES ***
cd /d %DHSRC%\deephaven-core\cpp-client\deephaven
%VCPKG_ROOT%\vcpkg.exe install --triplet x64-windows

echo *** CONFIGURING DEEPHAVEN BUILD ***
cmake -B build -S . -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%/scripts/buildsystems/vcpkg.cmake -DCMAKE_INSTALL_PREFIX=%DHINSTALL% -DX_VCPKG_APPLOCAL_DEPS_INSTALL=ON

ecoh *** BUILDING C++ CLIENT ***
cmake --build build --config RelWithDebInfo --target install -- /p:CL_MPCount=16 -m:1
