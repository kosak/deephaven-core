//
// Copyright (c) 2016-2025 Deephaven Data Labs and Patent Pending
//
package io.deephaven.auth.codegen.impl;

import io.deephaven.auth.AuthContext;
import io.deephaven.auth.ServiceAuthWiring;
import io.grpc.ServerServiceDefinition;
import io.grpc.health.v1.HealthCheckRequest;
import io.grpc.health.v1.HealthGrpc;

/**
 * This interface provides type-safe authorization hooks for HealthGrpc.
 */
public interface HealthAuthWiring extends ServiceAuthWiring<HealthGrpc.HealthImplBase> {
    /**
     * Wrap the real implementation with authorization checks.
     *
     * @param delegate the real service implementation
     * @return the wrapped service implementation
     */
    default ServerServiceDefinition intercept(HealthGrpc.HealthImplBase delegate) {
        final ServerServiceDefinition service = delegate.bindService();
        final ServerServiceDefinition.Builder serviceBuilder =
                ServerServiceDefinition.builder(service.getServiceDescriptor());

        serviceBuilder.addMethod(ServiceAuthWiring.intercept(
                service, "Check", null, this::onMessageReceivedCheck));
        serviceBuilder.addMethod(ServiceAuthWiring.intercept(
                service, "Watch", null, this::onMessageReceivedWatch));

        return serviceBuilder.build();
    }

    /**
     * Authorize a request to Check.
     *
     * @param authContext the authentication context of the request
     * @param request the request to authorize
     * @throws io.grpc.StatusRuntimeException if the user is not authorized to invoke Check
     */
    void onMessageReceivedCheck(AuthContext authContext, HealthCheckRequest request);

    /**
     * Authorize a request to Watch.
     *
     * @param authContext the authentication context of the request
     * @param request the request to authorize
     * @throws io.grpc.StatusRuntimeException if the user is not authorized to invoke Watch
     */
    void onMessageReceivedWatch(AuthContext authContext, HealthCheckRequest request);

    class AllowAll implements HealthAuthWiring {
        public void onMessageReceivedCheck(AuthContext authContext, HealthCheckRequest request) {}

        public void onMessageReceivedWatch(AuthContext authContext, HealthCheckRequest request) {}
    }

    class DenyAll implements HealthAuthWiring {
        public void onMessageReceivedCheck(AuthContext authContext, HealthCheckRequest request) {
            ServiceAuthWiring.operationNotAllowed();
        }

        public void onMessageReceivedWatch(AuthContext authContext, HealthCheckRequest request) {
            ServiceAuthWiring.operationNotAllowed();
        }
    }

    class TestUseOnly implements HealthAuthWiring {
        public HealthAuthWiring delegate;

        public void onMessageReceivedCheck(AuthContext authContext, HealthCheckRequest request) {
            if (delegate != null) {
                delegate.onMessageReceivedCheck(authContext, request);
            }
        }

        public void onMessageReceivedWatch(AuthContext authContext, HealthCheckRequest request) {
            if (delegate != null) {
                delegate.onMessageReceivedWatch(authContext, request);
            }
        }
    }
}
