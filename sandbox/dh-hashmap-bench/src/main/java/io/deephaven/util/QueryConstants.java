//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util;

/**
 * Minimal stand-in for deephaven-core's io.deephaven.util.QueryConstants; only NULL_LONG is used here.
 */
public final class QueryConstants {
    private QueryConstants() {}

    public static final long NULL_LONG = Long.MIN_VALUE;
}
