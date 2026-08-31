//
// Copyright (c) 2016-2026 Deephaven Data Labs and Patent Pending
//
package io.deephaven.util.annotations;

import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Inherited;
import java.lang.annotation.Target;

/**
 * Minimal stand-in for deephaven-core's io.deephaven.util.annotations.TestUseOnly.
 */
@Documented
@Inherited
@Target({ElementType.METHOD, ElementType.FIELD, ElementType.TYPE})
public @interface TestUseOnly {
}
