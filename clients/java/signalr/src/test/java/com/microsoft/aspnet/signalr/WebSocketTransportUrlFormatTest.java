// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

package com.microsoft.aspnet.signalr;

import static org.junit.jupiter.api.Assertions.*;

import java.net.URISyntaxException;
import java.util.HashMap;
import java.util.stream.Stream;

import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;

class WebSocketTransportUrlFormatTest {
    private static Stream<Arguments> protocols() {
        return Stream.of(
                Arguments.of("http://example.com", "ws://example.com"),
                Arguments.of("https://example.com", "wss://example.com"),
                Arguments.of("ws://example.com", "ws://example.com"),
                Arguments.of("wss://example.com", "wss://example.com"));
    }

    @ParameterizedTest
    @MethodSource("protocols")
    public void checkWebsocketUrlProtocol(String url, String expectedUrl) throws URISyntaxException {
        WebSocketTransport webSocketTransport = new WebSocketTransport(url, new NullLogger(), new HashMap<>());
        assertEquals(expectedUrl, webSocketTransport.getUrl().toString());
    }
}