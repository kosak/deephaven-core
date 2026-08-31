//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.base.verify;

/**
 * Minimal stand-in for deephaven-core's io.deephaven.base.verify.Assert, providing only the methods used by the
 * HashMapLockFree family. Same semantics: always-on runtime checks that throw on failure.
 */
public final class Assert {
    private Assert() {}

    public static void eq(long o0, String name0, long o1, String name1) {
        if (o0 != o1) {
            fail("asserted " + name0 + " == " + name1 + ", instead " + name0 + " == " + o0 + ", " + name1 + " == "
                    + o1);
        }
    }

    public static void neq(long o0, String name0, long o1, String name1) {
        if (o0 == o1) {
            fail("asserted " + name0 + " != " + name1 + ", instead both == " + o0);
        }
    }

    public static void leq(long o0, String name0, long o1, String name1) {
        if (o0 > o1) {
            fail("asserted " + name0 + " <= " + name1 + ", instead " + name0 + " == " + o0 + ", " + name1 + " == "
                    + o1);
        }
    }

    private static void fail(String message) {
        throw new AssertionFailure("Assertion failed: " + message);
    }

    public static final class AssertionFailure extends RuntimeException {
        public AssertionFailure(String message) {
            super(message);
        }
    }
}
