if not defined DHINSTALL (
  set DHINSTALL=%HOMEDRIVE%%HOMEPATH%\dhinstall
)

cd /d %DHSRC% || exit /b

FOR /F "tokens=*" %%a IN ('.\gradlew :printVersion -q') DO (
  SET DEEPHAVEN_VERSION=%%a
)

if not defined DEEPHAVEN_VERSION (
  echo DEEPHAVEN_VERSION is not defined
  exit /b
)

cd %DHSRC%\py\client-ticking
python setup.py build_ext -i || exit /b

python setup.py bdist_wheel || exit /b

pip3 install --force --no-deps dist/pydeephaven_ticking-%DEEPHAVEN_VERSION%-cp310-cp310-linux_x86_64.whl
