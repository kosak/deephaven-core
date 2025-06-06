//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
package io.deephaven.engine.table.impl.sources.regioned;

import io.deephaven.base.string.EncodingInfo;
import io.deephaven.util.codec.ObjectCodec;
import io.deephaven.util.mutable.MutableLong;
import junit.framework.TestCase;
import org.jetbrains.annotations.NotNull;
import org.jetbrains.annotations.Nullable;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.util.stream.Stream;

/**
 * Tests for {@link RegionedColumnSourceObject} with variable length codec.
 */
@SuppressWarnings("Convert2Diamond")
public class TestRegionedColumnSourceObjectVariable extends TstRegionedColumnSourceObject<String> {

    private static final ObjectCodec<String> VARIABLE = new ObjectCodec<String>() {
        @NotNull
        @Override
        public byte[] encode(@Nullable String input) {
            assert input != null;
            return EncodingInfo.UTF_8.encode(input);
        }

        @Override
        public boolean isNullable() {
            return false;
        }

        @Override
        public int getPrecision() {
            return 0;
        }

        @Override
        public int getScale() {
            return 0;
        }

        @Override
        public String decode(@NotNull byte[] input, int offset, int length) {
            return EncodingInfo.UTF_8.decode(input, offset, length);
        }

        @Override
        public int expectedObjectWidth() {
            return VARIABLE_WIDTH_SENTINEL;
        }
    };

    private static final Value<String>[] REUSABLE_VALUES;
    static {
        final MutableLong length = new MutableLong(0);
        // noinspection unchecked
        REUSABLE_VALUES =
                Stream.of("12345", "000", "abcdefg", "ABC", "love", "hate", "nineteen", "ninety", "tuvwxyz", "Z").map(
                        s -> {
                            length.add(s.length());
                            return new Value<>(s, objectToBytes(s), length.get());
                        }).toArray(Value[]::new);
    }

    public TestRegionedColumnSourceObjectVariable() {
        super(REUSABLE_VALUES);
    }

    @Override
    public void setUp() throws Exception {
        super.setUp();
        SUT = new RegionedColumnSourceObject.AsValues<>(manager, String.class);
        TestCase.assertEquals(String.class, SUT.getType());
    }

    private static byte[] objectToBytes(String inObject) {
        try {
            ByteArrayOutputStream byteOutStream = new ByteArrayOutputStream();
            for (int ci = 0; ci < inObject.length(); ++ci) {
                byteOutStream.write((byte) inObject.charAt(ci));
            }
            byteOutStream.flush();
            return byteOutStream.toByteArray();
        } catch (IOException e) {
            throw new RuntimeException(e);
        }
    }
}
