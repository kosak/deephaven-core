if not defined DHSRC (
  set DHSRC=%HOMEDRIVE%%HOMEPATH%\dhsrc
)

cd /d %DHSRC%\deephaven-core\py\client || exit /b

pip3 install -r requirements-dev.txt

FOR /F "tokens=*" %%a IN ('.\gradlew :printVersion -q') DO (
  SET DEEPHAVEN_VERSION=%%a
)

if not defined DEEPHAVEN_VERSION (
  echo DEEPHAVEN_VERSION is not defined
  exit /b
)

python setup.py bdist_wheel || exit /b
