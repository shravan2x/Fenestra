# Fenestra Testing Plan

This document describes how to test Fenestra as it evolves from a bootstrap
project into a working X11 server with native Windows hosting and, later,
potential Wayland support.

## Testing goals

The testing strategy should focus on:

- protocol correctness
- server-state correctness
- stable request handling
- transport reliability
- native window integration
- rendering correctness

The project should not depend only on manual testing or only on end-to-end
tests. Most confidence should come from fast, deterministic automated tests.

## Testing pyramid

### 1. Protocol parser and encoder tests

These are the first tests to build and likely the highest-value tests in the
earliest phases.

Test cases:

- parse X11 setup requests
- handle little-endian and big-endian clients
- reject malformed setup packets
- encode setup replies
- encode errors, events, and replies with correct lengths and padding
- validate opcode decoding

Why these matter:

- X11 is a binary protocol
- off-by-one and padding bugs are common
- these tests are fast and deterministic

Recommended style:

- golden byte-array tests
- input bytes to decoded model
- model to exact output bytes

### 2. Server-state and object-model tests

These tests validate the internal server model without using sockets or native
windows.

Test cases:

- XID allocation
- client ownership validation
- atom intern behavior
- property storage and lookup
- window tree creation and mutation
- map and unmap state transitions
- geometry updates
- event mask subscriptions

Why these matter:

- most protocol handlers eventually mutate server state
- if the state model is reliable, the rest of the implementation is easier to
  reason about

### 3. Request-handler tests

These tests should operate on decoded request objects and verify server
behavior in-process.

Test cases:

- `CreateWindow` creates a window resource
- `DestroyWindow` removes a window and cleans up children as expected
- `MapWindow` changes window state and emits expected events
- `ConfigureWindow` changes geometry correctly
- `ChangeProperty` updates property values
- `GetProperty` returns the expected data
- `InternAtom` returns stable atom IDs

Why these matter:

- they validate the behavior layer directly
- they are more targeted and stable than full end-to-end tests

### 4. Transport and integration tests

These tests bring up a server instance and connect to it using sockets or test
streams.

Test cases:

- startup and shutdown
- setup handshake over transport
- invalid client behavior
- multi-client connection handling
- request/reply round-trips
- cancellation and graceful shutdown

Recommended approach:

- build a tiny internal protocol probe client
- use that client in automated tests to send raw X11 packets
- avoid depending too early on large external X11 applications

### 5. Native host smoke tests

Because Fenestra aims to host X11 top-level windows as native Windows windows,
the project should include a focused set of platform-specific smoke tests.

Windows-specific test cases:

- a mapped top-level X11 window produces a native host window
- native window title reflects X11 window metadata
- move and resize operations update server-side geometry
- host close requests produce the expected X11-side lifecycle behavior
- focus activation is synchronized correctly

Important note:

- keep this test set small and focused
- do not push too much logic into fragile UI automation

### 6. Real-client compatibility tests

These tests use real X11 applications against the server.

Good candidates:

- `xclock`
- `xeyes`
- `xlogo`
- `xterm` later
- tiny custom sample clients

Why these matter:

- real clients reveal protocol expectations that unit tests may miss
- they are especially useful for milestone validation

## Recommended testing by project phase

### Phase 1: Connection and setup

Automate:

- setup request parser tests
- setup reply encoder tests
- handshake integration tests

Manual:

- connect with a tiny probe client

### Phase 2: Resource and object model

Automate:

- resource allocation tests
- atom and property tests
- window-tree state tests

### Phase 3: Minimal window lifecycle

Automate:

- request-handler tests for create, map, destroy, configure, and property flows
- integration tests using the internal protocol probe

Manual:

- run a small sample client that creates and maps a window

### Phase 4: Native Windows hosting

Automate:

- fake native-host tests
- targeted Windows smoke tests

Manual:

- verify frame, caption buttons, taskbar presence, movement, resizing, and
  close behavior

### Phase 5: Rendering and presentation

Automate:

- rendering state tests
- invalidation and damage tests
- golden image tests for stable rendering scenarios

Manual:

- visually validate drawing and resizing behavior

### Phase 6 and beyond: Input and compatibility

Automate:

- input translation tests
- focus and event delivery tests
- clipboard and selection tests where feasible

Manual:

- exercise simple interactive X11 apps

## Test types to prioritize

### Golden byte tests

Best for:

- protocol parsing
- protocol encoding
- reply and event formatting

### State-machine tests

Best for:

- window states
- map and unmap transitions
- focus behavior
- property changes
- event generation

### Fake-host tests

Best for:

- verifying how server code interacts with the native host abstraction
- testing window-management behavior without real Win32 windows

### Integration probe tests

Best for:

- socket protocol validation
- testing handshake and selected requests end to end

### Golden image tests

Best for later phases:

- stable rendering scenarios
- visual regression checks for drawing paths

## Test infrastructure recommendations

Add these projects later:

- `Fenestra.Tests`
- `Fenestra.IntegrationTests`
- `Fenestra.Samples`

Useful helpers:

- a fake `INativeWindowHost`
- a tiny X11 protocol probe client
- byte-array builders for requests and expected replies
- image comparison utilities for rendering tests

## CI strategy

Use two lanes:

### Cross-platform CI

Run:

- parser tests
- state-model tests
- request-handler tests
- headless integration tests

### Windows CI

Run:

- Win32 host tests
- Windows-specific smoke tests
- rendering and presentation tests tied to native APIs

## Practical testing rules

- prefer fast deterministic tests over large fragile end-to-end suites
- test bytes, state, and request handlers before UI automation
- add compatibility tests gradually based on real client behavior
- keep manual smoke testing as a supplement, not the primary safety net
- use a software path first, then add rendering-specific regression tests once
  GPU-backed composition is introduced

## Recommended first testing milestone

The first testing milestone should cover:

1. setup request parsing
2. setup reply encoding
3. XID allocation
4. root window initialization
5. transport handshake integration

That combination gives strong confidence in the earliest server work without
requiring windowing or rendering to exist yet.
