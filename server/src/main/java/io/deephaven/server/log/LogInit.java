//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
package io.deephaven.server.log;

import io.deephaven.base.system.StandardStreamState;
import io.deephaven.engine.table.impl.util.AsyncClientErrorNotifier;
import io.deephaven.internal.log.InitSink;
import io.deephaven.internal.log.LoggerFactory;
import io.deephaven.io.log.LogSink;
import io.deephaven.io.logger.LogBuffer;
import io.deephaven.io.logger.Logger;

import javax.inject.Inject;
import javax.inject.Singleton;
import java.io.UnsupportedEncodingException;
import java.util.Set;

@Singleton
public class LogInit {

    private static final Logger log = LoggerFactory.getLogger(LogInit.class);

    private final StandardStreamState standardStreamState;
    private final LogBuffer logBuffer;
    private final LogSink logSink;
    private final Set<InitSink> sinkInits;

    @Inject
    public LogInit(StandardStreamState standardStreamState, LogBuffer logBuffer, LogSink logSink,
            Set<InitSink> sinkInits) {
        this.standardStreamState = standardStreamState;
        this.logBuffer = logBuffer;
        this.logSink = logSink;
        this.sinkInits = sinkInits;
    }

    @Inject
    public void run() {
        checkLogSinkIsSingleton();
        standardStreamState.setupRedirection();
        configureLoggerSink();
        Logger errLog = LoggerFactory.getLogger(AsyncClientErrorNotifier.class);
        AsyncClientErrorNotifier
                .setReporter(err -> errLog.error().append("Error in table update: ").append(err).endl());
    }

    private void configureLoggerSink() {
        for (InitSink init : sinkInits) {
            init.accept(logSink, logBuffer);
        }
    }

    private void checkLogSinkIsSingleton() {
        if (log.getSink() != logSink) {
            // If this contract is broken, we'll need to start attaching interceptors at LoggerFactory
            // Logger creation time, or have some sort of mechanism for LoggerFactory to notify us about
            // new log creations.
            throw new RuntimeException(String.format("Logger impl %s does not work with the current implementation.",
                    log.getClass().getName()));
        }
    }
}
