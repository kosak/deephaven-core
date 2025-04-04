//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
package io.deephaven.engine.table.impl.select.codegen;

import io.deephaven.engine.context.ExecutionContext;
import io.deephaven.engine.context.QueryCompilerImpl;
import io.deephaven.engine.context.QueryCompilerRequest;
import io.deephaven.engine.table.impl.QueryCompilerRequestProcessor;
import io.deephaven.util.CompletionStageFuture;
import io.deephaven.vector.Vector;
import io.deephaven.engine.context.QueryScopeParam;
import io.deephaven.engine.table.impl.select.Formula;
import io.deephaven.engine.table.impl.select.DhFormulaColumn;
import io.deephaven.engine.table.impl.select.FormulaCompilationException;
import io.deephaven.engine.table.impl.select.FormulaEvaluationException;
import io.deephaven.engine.table.impl.select.formula.FormulaKernel;
import io.deephaven.engine.table.impl.select.formula.FormulaKernelFactory;
import io.deephaven.engine.table.impl.util.codegen.CodeGenerator;
import io.deephaven.engine.table.impl.util.codegen.TypeAnalyzer;
import io.deephaven.util.type.TypeUtils;
import org.jetbrains.annotations.NotNull;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.function.Function;

import static io.deephaven.engine.util.IterableUtils.makeCommaSeparatedList;

public class JavaKernelBuilder {
    private static final String FORMULA_KERNEL_FACTORY_NAME = "__FORMULA_KERNEL_FACTORY";

    public static CompletionStageFuture<Result> create(
            @NotNull final String originalFormulaString,
            @NotNull final String cookedFormulaString,
            @NotNull final Class<?> returnedType,
            @NotNull final String timeInstanceVariables,
            @NotNull final Map<String, RichType> columns,
            @NotNull final Map<String, Class<?>> arrays,
            @NotNull final Map<String, Class<?>> params,
            @NotNull final QueryCompilerRequestProcessor compilationRequestProcessor) {
        final JavaKernelBuilder jkf = new JavaKernelBuilder(
                originalFormulaString, cookedFormulaString, returnedType, timeInstanceVariables, columns, arrays,
                params);
        final String classBody = jkf.generateKernelClassBody();

        return compilationRequestProcessor.submit(QueryCompilerRequest.builder()
                .description("FormulaKernel: " + originalFormulaString)
                .className("Formula")
                .classBody(classBody)
                .packageNameRoot(QueryCompilerImpl.FORMULA_CLASS_PREFIX)
                .build()).thenApply(clazz -> {
                    final FormulaKernelFactory fkf;
                    try {
                        fkf = (FormulaKernelFactory) clazz.getField(FORMULA_KERNEL_FACTORY_NAME).get(null);
                    } catch (ReflectiveOperationException e) {
                        throw new FormulaCompilationException(
                                "Formula compilation error for: " + cookedFormulaString, e);
                    }
                    return new Result(classBody, clazz, fkf);
                });
    }

    private final String originalFormulaString;
    private final String cookedFormulaString;
    private final Class<?> returnedType;
    private final String timeInstanceVariables;

    /**
     * name -> type
     */
    private final Map<String, RichType> columns;
    /**
     * name -> type
     */
    private final Map<String, Class<?>> arrays;
    /**
     * name -> type
     */
    private final Map<String, Class<?>> params;

    private JavaKernelBuilder(
            @NotNull final String originalFormulaString,
            @NotNull final String cookedFormulaString,
            @NotNull final Class<?> returnedType,
            @NotNull final String timeInstanceVariables,
            @NotNull final Map<String, RichType> columns,
            @NotNull final Map<String, Class<?>> arrays,
            @NotNull final Map<String, Class<?>> params) {
        this.originalFormulaString = originalFormulaString;
        this.cookedFormulaString = cookedFormulaString;
        this.returnedType = returnedType;
        this.timeInstanceVariables = timeInstanceVariables;
        this.columns = columns;
        this.arrays = arrays;
        this.params = params;
    }

    @NotNull
    private String generateKernelClassBody() {
        // params = QueryScope.getDefaultInstance().getParams(userParams);

        final TypeAnalyzer ta = TypeAnalyzer.create(returnedType);

        final CodeGenerator g = CodeGenerator.create(
                CodeGenerator.create(ExecutionContext.getContext().getQueryLibrary().getImportStrings().toArray()), "",
                "public class $CLASSNAME$ implements [[FORMULA_KERNEL_INTERFACE_CANONICAL]]", CodeGenerator.block(
                        generateFactoryLambda(), "",
                        CodeGenerator.repeated("instanceVar", "private final [[TYPE]] [[NAME]];"),
                        timeInstanceVariables,
                        generateKernelConstructor(), "",
                        generateMakeFillContext(), "",
                        generateApplyFormulaChunk(ta), "",
                        generateApplyFormulaPerItem(ta), "",
                        generateKernelContextClass(), ""));
        g.replace("FORMULA_KERNEL_INTERFACE_CANONICAL", FormulaKernel.class.getCanonicalName());
        visitFormulaParameters(null,
                ca -> {
                    final CodeGenerator fc = g.instantiateNewRepeated("instanceVar");
                    fc.replace("TYPE", ca.arrayTypeAsString);
                    fc.replace("NAME", ca.name);
                    return null;
                },
                p -> {
                    final CodeGenerator fc = g.instantiateNewRepeated("instanceVar");
                    fc.replace("TYPE", p.typeString);
                    fc.replace("NAME", p.name);
                    return null;
                });
        return g.build();
    }

    private CodeGenerator generateFactoryLambda() {
        final CodeGenerator g = CodeGenerator.create(
                "public static final [[FORMULA_KERNEL_FACTORY_CANONICAL]] [[FORMULA_KERNEL_FACTORY_NAME]] = $CLASSNAME$::new;");
        g.replace("FORMULA_KERNEL_FACTORY_CANONICAL", FormulaKernelFactory.class.getCanonicalName());
        g.replace("FORMULA_KERNEL_FACTORY_NAME", FORMULA_KERNEL_FACTORY_NAME);
        return g.freeze();
    }

    private CodeGenerator generateKernelConstructor() {
        final CodeGenerator g = CodeGenerator.create(
                "public $CLASSNAME$([[VECTOR_CANONICAL]][] __vectors,", CodeGenerator.indent(
                        "[[PARAM_CANONICAL]][] __params)"),
                CodeGenerator.block(
                        CodeGenerator.repeated("getVector", "[[NAME]] = ([[TYPE]])__vectors[[[INDEX]]];"),
                        CodeGenerator.repeated("getParam", "[[NAME]] = ([[TYPE]])__params[[[INDEX]]].getValue();")));
        g.replace("VECTOR_CANONICAL", Vector.class.getCanonicalName());
        g.replace("PARAM_CANONICAL", QueryScopeParam.class.getCanonicalName());
        final int[] nextArrayIndex = {0};
        final int[] nextParamIndex = {0};
        visitFormulaParameters(null,
                ap -> {
                    final CodeGenerator ag = g.instantiateNewRepeated("getVector");
                    ag.replace("NAME", ap.name);
                    ag.replace("TYPE", ap.arrayTypeAsString);
                    ag.replace("INDEX", "" + nextArrayIndex[0]++);
                    return null;
                },
                pp -> {
                    final CodeGenerator pg = g.instantiateNewRepeated("getParam");
                    pg.replace("NAME", pp.name);
                    pg.replace("TYPE", pp.typeString);
                    pg.replace("INDEX", "" + nextParamIndex[0]++);
                    return null;
                });
        return g.freeze();
    }

    @NotNull
    private CodeGenerator generateKernelContextClass() {
        final CodeGenerator g = CodeGenerator.create(
                "private class FormulaFillContext implements [[FILL_CONTEXT_CANONICAL]]", CodeGenerator.block(
                        // constructor
                        "FormulaFillContext(int __chunkCapacity)", CodeGenerator.block()));
        g.replace("FILL_CONTEXT_CANONICAL", Formula.FillContext.class.getCanonicalName());
        return g.freeze();
    }

    @NotNull
    private CodeGenerator generateMakeFillContext() {
        final CodeGenerator g = CodeGenerator.create(
                "@Override",
                "public FormulaFillContext makeFillContext(final int __chunkCapacity)", CodeGenerator.block(
                        "return new FormulaFillContext(__chunkCapacity);"));
        return g.freeze();
    }

    @NotNull
    private CodeGenerator generateApplyFormulaChunk(TypeAnalyzer ta) {
        final CodeGenerator g = CodeGenerator.create(
                "@Override",
                "public void applyFormulaChunk([[CANONICAL_FORMULA_FILLCONTEXT]] __context,", CodeGenerator.indent(
                        "final WritableChunk<? super Values> __destination,",
                        "Chunk<? extends Values>[] __sources)"),
                CodeGenerator.block(
                        "final [[DEST_CHUNK_TYPE]] __typedDestination = __destination.[[DEST_AS_CHUNK_METHOD]]();",
                        CodeGenerator.repeated("getChunks",
                                "final [[CHUNK_TYPE]] [[CHUNK_NAME]] = __sources[[[SOURCE_INDEX]]].[[AS_CHUNK_METHOD]]();"),
                        "final int __size = __typedDestination.size();",
                        "for (int __chunkPos = 0; __chunkPos < __size; ++__chunkPos)", CodeGenerator.block(
                                CodeGenerator.repeated("setLocalVars",
                                        "final [[VAR_TYPE]] [[VAR_NAME]] = [[VAR_INITIALIZER]];"),
                                "__typedDestination.set(__chunkPos, applyFormulaPerItem([[APPLY_FORMULA_ARGS]]));")));

        g.replace("CANONICAL_FORMULA_FILLCONTEXT", Formula.FillContext.class.getCanonicalName());
        g.replace("DEST_CHUNK_TYPE", ta.writableChunkVariableType);
        g.replace("DEST_AS_CHUNK_METHOD", ta.asWritableChunkMethodName);
        final int[] chunkIndexHolder = {0};
        final List<String> args = visitFormulaParameters(
                cs -> {
                    final TypeAnalyzer tm = TypeAnalyzer.create(cs.type);
                    final String chunkName = "__chunk__col__" + cs.name;
                    final CodeGenerator getChunks = g.instantiateNewRepeated("getChunks");
                    getChunks.replace("CHUNK_NAME", chunkName);
                    getChunks.replace("SOURCE_INDEX", "" + chunkIndexHolder[0]++);
                    getChunks.replace("CHUNK_TYPE", tm.readChunkVariableType);
                    getChunks.replace("AS_CHUNK_METHOD", tm.asReadChunkMethodName);
                    return chunkName + ".get(__chunkPos)";
                },
                null,
                null);
        g.replace("APPLY_FORMULA_ARGS", makeCommaSeparatedList(args));
        return g.freeze();
    }

    private CodeGenerator generateApplyFormulaPerItem(final TypeAnalyzer ta) {
        final CodeGenerator g = CodeGenerator.create(
                "private [[RETURN_TYPE]] applyFormulaPerItem([[ARGS]])", CodeGenerator.block(
                        "try", CodeGenerator.block(
                                "return [[FORMULA_STRING]];"),
                        CodeGenerator.samelineBlock("catch (java.lang.Exception __e)",
                                "throw new [[EXCEPTION_TYPE]](\"In formula: \" + [[JOINED_FORMULA_STRING]], __e);")));
        g.replace("RETURN_TYPE", ta.typeString);
        final List<String> args = visitFormulaParameters(
                n -> n.typeString + " " + n.name,
                null,
                null);
        g.replace("ARGS", makeCommaSeparatedList(args));
        g.replace("FORMULA_STRING", ta.wrapWithCastIfNecessary(cookedFormulaString));
        final String joinedFormulaString = QueryCompilerImpl.createEscapedJoinedString(originalFormulaString);
        g.replace("JOINED_FORMULA_STRING", joinedFormulaString);
        g.replace("EXCEPTION_TYPE", FormulaEvaluationException.class.getCanonicalName());
        return g.freeze();
    }

    private <T> List<T> visitFormulaParameters(
            Function<ChunkParameter, T> chunkLambda,
            Function<ColumnArrayParameter, T> columnArrayLambda,
            Function<ParamParameter, T> paramLambda) {
        final List<T> results = new ArrayList<>();
        if (chunkLambda != null) {
            for (Map.Entry<String, RichType> entry : columns.entrySet()) {
                final String name = entry.getKey();
                final RichType rt = entry.getValue();
                final ChunkParameter cp = new ChunkParameter(name, rt.getBareType(), rt.getCanonicalName());
                addIfNotNull(results, chunkLambda.apply(cp));
            }
        }

        if (columnArrayLambda != null) {
            for (Map.Entry<String, Class<?>> entry : arrays.entrySet()) {
                final String name = entry.getKey() + DhFormulaColumn.COLUMN_SUFFIX;
                final Class<?> dataType = entry.getValue();
                final Class<?> vectorType = DhFormulaColumn.getVectorType(dataType);
                final String vectorTypeAsString = vectorType.getCanonicalName() +
                        (TypeUtils.isConvertibleToPrimitive(dataType) ? "" : "<" + dataType.getCanonicalName() + ">");
                final ColumnArrayParameter cap = new ColumnArrayParameter(name, vectorType, vectorTypeAsString);
                addIfNotNull(results, columnArrayLambda.apply(cap));
            }
        }

        if (paramLambda != null) {
            for (Map.Entry<String, Class<?>> entry : params.entrySet()) {
                final String name = entry.getKey();
                final Class<?> type = entry.getValue();
                final ParamParameter pp = new ParamParameter(name, type, type.getCanonicalName());
                addIfNotNull(results, paramLambda.apply(pp));
            }
        }
        return results;
    }


    public static class Result {
        public final String classBody;
        public final Class<?> clazz;
        public final FormulaKernelFactory formulaKernelFactory;

        public Result(
                @NotNull final String classBody,
                @NotNull final Class<?> clazz,
                @NotNull final FormulaKernelFactory formulaKernelFactory) {
            this.classBody = classBody;
            this.clazz = clazz;
            this.formulaKernelFactory = formulaKernelFactory;
        }
    }

    private static <T> void addIfNotNull(List<T> list, T item) {
        if (item != null) {
            list.add(item);
        }
    }

    private static class ChunkParameter {
        final String name;
        final Class<?> type;
        final String typeString;

        ChunkParameter(final String name, final Class<?> type, final String typeString) {
            this.name = name;
            this.type = type;
            this.typeString = typeString;
        }
    }

    private static class ColumnArrayParameter {
        final String name;
        final Class<?> arrayType;
        final String arrayTypeAsString;

        ColumnArrayParameter(String name, Class<?> arrayType, String arrayTypeAsString) {
            this.name = name;
            this.arrayType = arrayType;
            this.arrayTypeAsString = arrayTypeAsString;
        }
    }

    private static class ParamParameter {
        final String name;
        final Class<?> type;
        final String typeString;

        ParamParameter(String name, Class<?> type, String typeString) {
            this.name = name;
            this.type = type;
            this.typeString = typeString;
        }
    }
}
