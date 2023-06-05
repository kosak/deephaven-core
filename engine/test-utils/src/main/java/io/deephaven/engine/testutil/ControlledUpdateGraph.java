package io.deephaven.engine.testutil;

import io.deephaven.engine.updategraph.impl.PeriodicUpdateGraph;

// TODO (deephaven-core#3886): Extract test functionality from PeriodicUpdateGraph
public class ControlledUpdateGraph extends PeriodicUpdateGraph {
    public ControlledUpdateGraph() {
        super("TEST", true, 1000, 25, -1);
    }
}