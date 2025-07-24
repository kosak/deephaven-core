pushd .
call "c:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x64 || exit /b
popd

pushd .
call ".\build-cpp-client.bat" || exit /b
popd

pushd .
call "..\py\client\build-py-client.bat" || exit /b
popd

pushd .
call "..\py\client-ticking\build-py-client-ticking.bat" || exit /b
popd
