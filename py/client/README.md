# Deephaven Python Client 

Deephaven Python Client is a Python package created by Deephaven Data Labs. It is a client API that allows Python applications to remotely access Deephaven data servers.

## `venv`

It's recommended to install in a Python virtual environment (venv). Use a command like the
below to create a venv. Then, activate the venv.

``` shell
python3 -m venv ~/py/dhenv
source ~/py/dhenv/bin/activate

Windows
%HOMEWHATVER%py\dhenv\Scripts\activate
```

    # Ensure distutils uses the compiler and linker in %PATH%
    # You need an installation of Visual Studio 2022 with
    # * Python Development Workload
    # * Option "Python native development tools" enabled
    # And this should be run from the "x64 Native Tools Command Prompt" installed by VS
    # Note "x64_x86 Cross Tools Command Prompt" will NOT work.



## Source Directory

### From the deephaven-core repository root
(clone from https://github.com/deephaven/deephaven-core)

It is assumed that you have the repository checked out at the location specified by
`${DHROOT}`

```
$ cd ~/dhsrc  # or another directory you choose
$ git clone https://github.com/deephaven/deephaven-core.git
$ cd deephaven-core
$ export DHROOT=`pwd`
```

## Change to the py/client directory inside the deephaven-core repository
``` shell
$ cd $DHROOT/py/client
```

## Dev environment setup
``` shell
$ pip3 install -r requirements-dev.txt
```

## Build
``` shell
### NO NO NO YOU HAVE TO TYPE PYTHON NOT PYTHON3 GOD ONLY KNOWS WHY

this is a hellscape for Windows. Install Java? Like no thanks
set DEEPHAVEN_VERSION=0.40.0-SNAPSHOT

JUST SAY PYTHON NOT PYTHON3
$ DEEPHAVEN_VERSION=$(../../gradlew :printVersion -q) python3 setup.py bdist_wheel
```

also if you mess up, you may want to git clean -xfd to clean up all the
state


and python not python3

## Run tests
windows (and linux): probably needs DH_HOST and DH_PORT again
``` shell
$ python -m unittest discover tests
```

failed test_systemic_scripts but I've stopped caring


xs!## Run examples
``` shell
$ python3 -m examples.demo_table_ops
   hardcoded to localhost
   but eventually runs if server is fixed... surprisingly slow startup
$ python3 -m examples.demo_query
   ditto... also surprisingly slow... like 23 seconds
$ python3 -m examples.demo_run_script
  ditto, has bug
      dh_session.run_script(server_script)
    ~~~~~~~~~~~~~~~~~~~~~^^^^^^^^^^^^^^^
  File "c:\Users\kosak\dhsrc\deephaven-core\py\client\pydeephaven\session.py", line 510, in run_script
    raise DHError("could not run script: " + response.error_message)
pydeephaven.dherror.DHError: could not run script: java.lang.RuntimeException: Error in Python interpreter:
Type: <class 'deephaven.dherror.DHError'>
Value: table sort operation failed. : deephaven.dherror.DHError: The sort direction must be either 'ASCENDING' or 'DESCENDING'. : The sort direction must be either 'ASCENDING' or 'DESCENDING'.
Traceback (most recent call last):
  File "/opt/deephaven/venv/lib/python3.10/site-packages/deephaven/table.py", line 1391, in sort
    raise DHError(message="The sort direction must be either 'ASCENDING' or 'DESCENDING'.")
deephaven.dherror.DHError: The sort direction must be either 'ASCENDING' or 'DESCENDING'. : The sort direction must be either 'ASCENDING' or 'DESCENDING'.
NoneType: None


Line: 1399
Namespace: sort
File: /opt/deephaven/venv/lib/python3.10/site-packages/deephaven/table.py
Traceback (most recent call last):
  File "<string>", line 2, in <module>
  File "/opt/deephaven/venv/lib/python3.10/site-packages/deephaven/table.py", line 1399, in sort

        at org.jpy.PyLib.executeCode(Native Method)
        at org.jpy.PyObject.executeCode(PyObject.java:133)
        at io.deephaven.engine.util.PythonEvaluatorJpy.evalScript(PythonEvaluatorJpy.java:73)
        at io.deephaven.integrations.python.PythonDeephavenSession.lambda$evaluate$1(PythonDeephavenSession.java:229)
        at io.deephaven.util.locks.FunctionalLock.doLockedInterruptibly(FunctionalLock.java:51)
        at io.deephaven.integrations.python.PythonDeephavenSession.evaluate(PythonDeephavenSession.java:229)
        at io.deephaven.engine.util.AbstractScriptSession.lambda$evaluateScript$0(AbstractScriptSession.java:168)
        at io.deephaven.engine.context.ExecutionContext.lambda$apply$0(ExecutionContext.java:196)
        at io.deephaven.engine.context.ExecutionContext.apply(ExecutionContext.java:207)
        at io.deephaven.engine.context.ExecutionContext.apply(ExecutionContext.java:195)
        at io.deephaven.engine.util.AbstractScriptSession.evaluateScript(AbstractScriptSession.java:168)
        at io.deephaven.engine.util.DelegatingScriptSession.evaluateScript(DelegatingScriptSession.java:77)
        at io.deephaven.engine.util.ScriptSession.evaluateScript(ScriptSession.java:90)
        at io.deephaven.server.console.ConsoleServiceGrpcImpl.lambda$executeCommand$7(ConsoleServiceGrpcImpl.java:202)
        at io.deephaven.server.session.SessionState$ExportObject.doExport(SessionState.java:1001)
        at java.base/java.util.concurrent.Executors$RunnableAdapter.call(Executors.java:572)
        at java.base/java.util.concurrent.FutureTask.run(FutureTask.java:317)
        at java.base/java.util.concurrent.ThreadPoolExecutor.runWorker(ThreadPoolExecutor.java:1144)
        at java.base/java.util.concurrent.ThreadPoolExecutor$Worker.run(ThreadPoolExecutor.java:642)
        at io.deephaven.server.runner.scheduler.SchedulerModule$ThreadFactory.lambda$newThread$0(SchedulerModule.java:100)
        at org.jpy.PyLib.callAndReturnObject(Native Method)
        at org.jpy.PyObject.call(PyObject.java:444)
        at io.deephaven.server.console.python.DebuggingInitializer.lambda$createInitializer$0(DebuggingInitializer.java:46)
        at java.base/java.lang.Thread.run(Thread.java:1583)
$ python3 -m examples.demo_merge_tables
works ok once I fix the server name but slow... all these examples based on taxi data are slow (21 sec)
$ python3 -m examples.demo_asof_join
works ok with server name fixed.  NOT slow because doesn't depend on taxi data



```
## Install

Note the actual name of the `.whl` file may be different depending on system details.

``` shell
$ pip3 install dist/pydeephaven-<x.y.z>-py3-none-any.whl
```
## Quick start

```python    
    >>> from pydeephaven import Session
    >>> session = Session() # assuming Deephaven Community Edition is running locally with the default configuration
    >>> table1 = session.time_table(period=1000000000).update(formulas=["Col1 = i % 2"])
    >>> df = table1.to_arrow().to_pandas()
    >>> print(df)
                        Timestamp  Col1
    0     1629681525690000000     0
    1     1629681525700000000     1
    2     1629681525710000000     0
    3     1629681525720000000     1
    4     1629681525730000000     0
    ...                   ...   ...
    1498  1629681540670000000     0
    1499  1629681540680000000     1
    1500  1629681540690000000     0
    1501  1629681540700000000     1
    1502  1629681540710000000     0

    >>> session.close()

```

## Initialize

The `Session` class is your connection to Deephaven. This is what allows your Python code to interact with a Deephaven server.

```
from pydeephaven import Session

session = Session()
```

## Ticking table

The `Session` class has many methods that create tables. This example creates a ticking time table and binds it to Deephaven.

```
from pydeephaven import Session

session = Session()

table = session.time_table(period=1000000000).update(formulas=["Col1 = i % 2"])
session.bind_table(name="my_table", table=table)
```

This is the general flow of how the Python client interacts with Deephaven. You create a table (new or existing), execute some operations on it, and then bind it to Deephaven. Binding the table gives it a named reference on the Deephaven server, so that it can be used from the Web API or other Sessions.

## Execute a query on a table

`table.update()` can be used to execute an update on a table. This updates a table with a query string.

```
from pydeephaven import Session

session = Session()

# Create a table with no columns and 3 rows
table = session.empty_table(3)
# Create derived table having a new column MyColumn populated with the row index "i"
table = table.update(["MyColumn = i"])
# Update the Deephaven Web Console with this new table
session.bind_table(name="my_table", table=table)
```

## Sort a table

`table.sort()` can be used to sort a table. This example sorts a table by one of its columns.

```
from pydeephaven import Session

session = Session()

table = session.empty_table(5)
table = table.update(["SortColumn = 4-i"])

table = table.sort(["SortColumn"])
session.bind_table(name="my_table", table=table)
```

## Filter a table

`table.where()` can be used to filter a table. This example filters a table using a filter string.

```
from pydeephaven import Session

session = Session()

table = session.empty_table(5)
table = table.update(["Values = i"])

table = table.where(["Values % 2 == 1"])
session.bind_table(name="my_table", table=table)
```

## Query objects

Query objects are a way to create and manage a sequence of Deephaven query operations as a single unit. Query objects have the potential to perform better than the corresponding individual queries, because the query object can be transmitted to the server in one request rather than several, and because the system can perform certain optimizations when it is able to see the whole sequence of queries at once. They are similar in spirit to prepared statements in SQL.

The general flow of using a query object is to construct a query with a table, call the table operations (sort, filter, update, etc.) on the query object, and then assign your table to the return value of `query.exec()`.

Any operation that can be executed on a table can also be executed on a query object. This example shows two operations that compute the same result, with the first one using the table updates and the second one using a query object.

```
from pydeephaven import Session

session = Session()

table = session.empty_table(10)

# executed immediately
table1= table.update(["MyColumn = i"]).sort(["MyColumn"]).where(["MyColumn > 5"]);

# create Query Object (execution is deferred until the "exec" statement)
query_obj = session.query(table).update(["MyColumn = i"]).sort(["MyColumn"]).where(["MyColumn > 5"]);
# Transmit the QueryObject to the server and execute it
table2 = query_obj.exec();

session.bind_table(name="my_table1", table=table1)
session.bind_table(name="my_table2", table=table2)
```

## Join two tables

`table.join()` is one of many operations that can join two tables, as shown below.

```
from pydeephaven import Session

session = Session()

table1 = session.empty_table(5)
table1 = table1.update(["Values1 = i", "Group = i"])
table2 = session.empty_table(5)
table2 = table2.update(["Values2 = i + 10", "Group = i"])

table = table1.join(table2, on=["Group"])
session.bind_table(name="my_table", table=table)
```

## Perform aggregations on a table

Aggregations can be applied on tables in the Python client. This example creates an aggregation that averages 
the `Count` column of a table, and aggregates it by the `Group` column.

```
from pydeephaven import Session, agg

session = Session()

table = session.empty_table(10)
table = table.update(["Count = i", "Group = i % 2"])

my_agg = agg.avg(["Count"])

table = table.agg_by(aggs=[my_agg], by=["Group"])
session.bind_table(name="my_table", table=table)
```

## Convert a PyArrow table to a Deephaven table

Deephaven natively supports [PyArrow tables](https://arrow.apache.org/docs/python/index.html). This example converts between a PyArrow table and a Deephaven table.

```
import pyarrow as pa
from pydeephaven import Session

session = Session()

arr = pa.array([4,5,6], type=pa.int32())
pa_table = pa.Table.from_arrays([arr], names=["Integers"])

table = session.import_table(pa_table)
session.bind_table(name="my_table", table=table)

#Convert the Deephaven table back to a pyarrow table
pa_table = table.to_arrow()
```

## Execute a script server side

`session.run_script()` can be used to execute code on the Deephaven server. This is useful when operations cannot be done on the client-side, such as creating a dynamic table writer. This example shows how to execute a script server-side and retrieve a table generated from the script.

```
from pydeephaven import Session

session = Session()

script = """
from deephaven import empty_table

table = empty_table(8).update(["Index = i"])
"""

session.run_script(script)

table = session.open_table("table")
print(table.to_arrow())
```

## Error handling

The `DHError` is thrown whenever the client package encounters an error. This example shows how to catch a `DHError`.

```
from pydeephaven import Session, DHError

try:
    session = Session(host="invalid_host")
except DHError as e:
    print("Deephaven error when connecting to session")
    print(e)
except Exception as e:
    print("Unknown error")
    print(e)
```

## Related documentation
* https://deephaven.io/
* https://arrow.apache.org/docs/python/index.html

## API Reference
[start here] https://deephaven.io/core/client-api/python/
