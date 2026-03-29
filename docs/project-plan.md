# Fenestra Project Plan

This document captures the original recommended plan for building Fenestra:
an X11 server in C#/.NET that runs well on Windows, aims to feel native on the
host operating system, and leaves a clean path for future Wayland support.

## High-level direction

The goal is not only to implement the X11 protocol, but to do it in a way that
lets X11 applications feel integrated with the host operating system.

The most distinctive long-term idea is:

- map top-level X11 windows to real native windows
- keep native frame controls like close, minimize, and maximize
- integrate with taskbar, focus, move/resize, and host window management
- still preserve an internal X11 object model for child windows, events, and
  drawing

That means Fenestra should be organized around clear architectural layers rather
than built as one large protocol interpreter.

## Recommended solution layout

### Start with one main solution

Keep a single Visual Studio solution at first:

- `Fenestra.sln`

Use the following projects early:

- `Fenestra.App`
  - startup, configuration, dependency wiring, diagnostics
- `Fenestra.Server`
  - server orchestration, state, dispatch, client lifecycle
- `Fenestra.Protocol.X11`
  - setup handshake, request decoding, replies, errors, events
- `Fenestra.Transport`
  - TCP and local transports, display binding, connection handling
- `Fenestra.NativeHost.Abstractions`
  - host-window abstractions for platform-specific implementations
- `Fenestra.NativeHost.Win32`
  - Windows implementation for native top-level windows

### Add more projects later

Once the basics are working, expand with:

- `Fenestra.Rendering`
  - drawing, pixmaps, image conversion, compositing
- `Fenestra.Wm`
  - window manager policy, decorations, focus, stacking
- `Fenestra.Extensions`
  - optional X11 extensions
- `Fenestra.Protocol.Wayland`
  - Wayland protocol layer in a future phase
- `Fenestra.Tests`
  - focused unit and protocol tests
- `Fenestra.IntegrationTests`
  - end-to-end protocol and transport tests
- `Fenestra.Samples`
  - small probes and diagnostic clients

## Major parts to build

## 1. Transport and client sessions

Build the code that:

- listens on an X11 display port or socket
- accepts client connections
- manages byte order and sequence numbers
- performs the X11 setup handshake
- routes requests to the correct client session

This is the entry point to the whole server.

## 2. X11 protocol layer

Build code to:

- parse setup requests
- decode requests by opcode
- encode replies, errors, and events
- manage request lengths and padding
- register extensions later

The protocol layer should focus on bytes and message shapes, not own server
state directly.

## 3. Server object model

Build the internal state model for:

- clients
- resource IDs
- windows
- pixmaps
- graphics contexts
- atoms
- properties
- screens, visuals, depths, and colormaps
- event subscriptions

This is one of the most important parts of the system because nearly every later
feature depends on it.

## 4. Minimal window lifecycle

Support the subset of requests needed for basic clients:

- `CreateWindow`
- `DestroyWindow`
- `MapWindow`
- `UnmapWindow`
- `ConfigureWindow`
- `ReparentWindow`
- `InternAtom`
- `ChangeProperty`
- `GetProperty`
- `QueryTree`
- `GetGeometry`
- `SelectInput`

The first real milestone is not "complete X11." It is:

1. accept a connection
2. complete setup handshake
3. create a root window and basic server state
4. support creating and mapping a top-level window
5. show that top-level window as a native window on Windows

## 5. Native window hosting

This is the most interesting feature area for the project.

Recommended model:

- top-level X11 windows become native OS windows
- child X11 windows remain server-managed objects inside the top-level surface
- override-redirect windows can map to special popup-style host windows when
  appropriate

Windows-specific goals:

- standard frame and title bar
- close, minimize, and maximize buttons
- taskbar presence
- native focus and z-order behavior
- proper resize and move integration

This gives the project a unique identity beyond a generic framebuffer-based X
server.

## 6. Rendering and presentation

After window lifecycle works, build the ability to show actual contents:

- pixmap storage
- image upload and copy operations
- graphics contexts
- expose/invalidation handling
- repaint scheduling
- presentation into the native host window

Recommended strategy:

- implement a software path first
- keep rendering interfaces clean
- add GPU-backed composition later

## 7. Input and events

Translate host input into X11 events:

- keyboard input
- pointer motion
- button press/release
- focus changes
- configure notifications
- expose and visibility events
- clipboard/selections later

This is where platform integration becomes especially important.

## 8. Compatibility and extensions

Do not try to implement every X11 feature early.

Add support based on target applications and observed failures. Useful later
extensions may include:

- `BIG-REQUESTS`
- `SHAPE`
- `XFIXES`
- `RANDR`
- `RENDER`
- `COMPOSITE`
- `DAMAGE`
- `MIT-SHM`

## Recommended build order

Use this implementation sequence:

### Phase 1: X11 connection and setup

- accept client connections
- parse setup request
- encode and send setup response
- handle little-endian and big-endian clients correctly

### Phase 2: Core server state

- build XID allocation
- add client/session state
- define root window, screen, visual, atoms, and properties

### Phase 3: Minimal request handling

- add opcode dispatch
- implement basic window/property requests
- support a tiny client that creates and maps a window

### Phase 4: Native Win32 hosting

- create a real Win32 window for each mapped X11 top-level window
- synchronize title, size, geometry, and close behavior
- translate host actions back into X11 events

### Phase 5: Rendering

- add pixmaps and image operations
- draw content into the host window
- start with software rendering

### Phase 6: Input and interaction

- keyboard and mouse input
- focus and activation handling
- basic clipboard/selection behavior

### Phase 7: Extensions and compatibility

- target real apps and implement what they require

### Phase 8: Wayland direction

- keep the architecture ready for a future Wayland frontend

## Wayland strategy

There are two sensible ways to think about Wayland:

### Short-term recommendation

Build X11 first and keep the design clean enough that Wayland can be added later.

### Long-term recommendation

Aim for a shared server/compositor/native-host core with:

- X11 frontend
- Wayland frontend

That means the project eventually becomes a protocol-flexible display server
architecture rather than a one-off X11 implementation.

## Cool ideas worth pursuing

## Native-decorated mode

Allow top-level X11 windows to use normal OS chrome:

- standard title bar
- standard buttons
- taskbar presence
- snap/tiling integration on Windows

## Server-decorated mode

Offer a mode where the server draws custom decorations for scenarios that need
closer X11/window-manager semantics.

## Headless mode

Allow a non-visual mode for:

- protocol tests
- rendering tests
- screenshot generation
- CI without a visible desktop

## Root-window-as-virtual-desktop

Treat the X11 root window as:

- a hidden coordination object for native top-level hosting, or
- an optional visible desktop/debug surface

## Per-window presentation strategies

Support multiple rendering/presentation modes over time:

- software bitmaps
- Skia
- Direct2D/DirectComposition on Windows
- GPU-accelerated composition later

## Practical guidance

When making design decisions, keep asking:

> Is this code protocol-specific, server-core-specific, rendering-specific, or
> host-specific?

That question will help keep the codebase understandable as it grows.

## Best immediate next step

The most productive next technical milestone is:

1. real transport listener
2. X11 setup handshake
3. basic server state and root window model
4. minimal window creation/mapping requests
5. native top-level Win32 window hosting

That sequence gives visible progress without skipping the foundations.
