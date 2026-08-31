# dh-hashmap-bench

JMH scratch project for benchmarking and iterating on Deephaven's
`HashMapLockFreeK1V1` / `K2V2` / `K4V4` (`io.deephaven.util.datastructures.hash`).

The hashmap sources under `src/main/java/io/deephaven/util/datastructures/hash/`
were copied **verbatim** from deephaven-core @ 5d203764d9
(`Util/src/main/java/io/deephaven/util/datastructures/hash/`). Package names are
unchanged, so back-porting improvements is a straight file copy / diff.

Three tiny deephaven-core classes are shimmed locally instead of copied
(`Assert`, `QueryConstants`, `TestUseOnly`); `PrimeFinder` comes from the real
published `io.deephaven:hash` artifact.

## Running benchmarks

```bash
./gradlew run                                      # everything
./gradlew run --args="getHit"                      # one benchmark (regex)
./gradlew run --args="fill -p impl=K1V1,FASTUTIL -p size=4000000"
./gradlew run --args="get -prof gc"                # with allocation profiling
./gradlew run --args="-h"                          # JMH's full CLI help
```

In IntelliJ: open this directory as a Gradle project, then either run the
`run` Gradle task or make a plain run configuration with main class
`org.openjdk.jmh.Main` (args = same as above). The IntelliJ "JMH Java
Microbenchmark Harness" plugin also works — it puts run gutters on each
`@Benchmark` method.
