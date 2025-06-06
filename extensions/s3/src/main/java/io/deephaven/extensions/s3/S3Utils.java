//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
package io.deephaven.extensions.s3;

import org.jetbrains.annotations.NotNull;
import software.amazon.awssdk.awscore.AwsRequestOverrideConfiguration;
import software.amazon.awssdk.profiles.ProfileFile;
import software.amazon.awssdk.profiles.ProfileFileLocation;

import java.nio.file.Path;
import java.time.Duration;
import java.util.Optional;

import static io.deephaven.extensions.s3.S3ClientFactory.RETRY_STRATEGY_MAX_ATTEMPTS;

class S3Utils {

    /**
     * Aggregates the profile files for configuration and credentials files into a single {@link ProfileFile}.
     *
     * @param configFilePath An {@link Optional} containing the path to the configuration file. If empty, the aws sdk
     *        default location is used.
     * @param credentialsFilePath An {@link Optional} containing the path to the credentials file, If empty, the aws sdk
     *        default location is used.
     *
     * @return A {@link ProfileFile} that aggregates the configuration and credentials files.
     */
    @SuppressWarnings("OptionalUsedAsFieldOrParameterType")
    static ProfileFile aggregateProfileFile(
            @NotNull final Optional<Path> configFilePath,
            @NotNull final Optional<Path> credentialsFilePath) {
        final ProfileFile.Aggregator builder = ProfileFile.aggregator();

        // Add the credentials file
        credentialsFilePath.or(ProfileFileLocation::credentialsFileLocation)
                .ifPresent(path -> addProfileFile(builder, ProfileFile.Type.CREDENTIALS, path));

        // Add the configuration file
        configFilePath.or(ProfileFileLocation::configurationFileLocation)
                .ifPresent(path -> addProfileFile(builder, ProfileFile.Type.CONFIGURATION, path));

        return builder.build();
    }

    private static void addProfileFile(
            @NotNull final ProfileFile.Aggregator builder,
            @NotNull final ProfileFile.Type type,
            @NotNull final Path path) {
        builder.addFile(ProfileFile.builder()
                .type(type)
                .content(path)
                .build());
    }

    /**
     * Helper function to add timeout to the builder.
     *
     * @param builder the {@link AwsRequestOverrideConfiguration.Builder} to add the timeout to
     * @param timeout the timeout to add
     */
    static void addTimeout(AwsRequestOverrideConfiguration.Builder builder, final Duration timeout) {
        builder.apiCallAttemptTimeout(timeout.dividedBy(RETRY_STRATEGY_MAX_ATTEMPTS))
                .apiCallTimeout(timeout);
    }
}
