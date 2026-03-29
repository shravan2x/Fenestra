# Fenestra architecture

## Vision

Fenestra is an X11 server implemented in C#/.NET with a native-hosting layer that can
present X11 windows as real operating-system windows. The first priority is Windows,
but the long-term architecture should support cross-platform hosts and, later, Wayland.

The core idea is to keep protocol processing separate from composition and window hosting:

- **Protocol layer** understands X11 wire messages and future Wayland protocol messages.
- **Server core** owns object lifetimes, resources, state, events, and scheduling.
- **Rendering/composition layer** decides how pixels are produced and combined.
- **Native host layer** maps top-level surfaces to native windows and input events.

## Why this architecture fits the goal

If you want X11 client windows to appear with native title bars, close buttons, taskbar
entries, and normal OS behavior, then your server should not be tightly coupled to one
software framebuffer model. Instead:

1. X11 clients talk to your server using the X11 protocol.
2. The server tracks windows, pixmaps, graphics contexts, atoms, properties, and events.
3. Top-level X11 windows are mapped into host surfaces.
4. On Windows, those top-level surfaces can be represented by real Win32 windows.
5. Reparenting, decorations, focus, and sizing can be coordinated by a native-aware
   window management policy layer.

This lets you build something that feels more like "X11 apps integrated into Windows"
than "an X server trapped inside one bitmap".

## Recommended repository layout

### Core solution

- `Fenestra.App`
  - Entry point, configuration, dependency wiring, diagnostics.
- `Fenestra.Protocol.X11`
  - Handshake parsing, request decoding, replies/events/errors, extension registries.
- `Fenestra.Server`
  - Resource database, client sessions, screens, windows, pixmaps, atoms, properties.
- `Fenestra.Transport`
  - TCP and local transports, authentication hooks, display binding.
- `Fenestra.NativeHost.Abstractions`
  - Cross-platform interfaces for top-level windows, input, clipboard, cursor, monitors.
- `Fenestra.NativeHost.Win32`
  - Win32 implementation for native windows and event translation.

### Add later as the project grows

- `Fenestra.Rendering`
  - Drawing primitives, pixmap storage, image conversion, compositing pipeline.
- `Fenestra.Extensions`
  - XRandR, XFixes, Shape, Composite, Damage, Render, MIT-SHM.
- `Fenestra.Wm`
  - Window manager behavior for decorations, focus policy, stacking, EWMH/ICCCM.
- `Fenestra.Wayland`
  - Wayland compositor/server path or X11-to-Wayland bridge adapters.
- `Fenestra.Tests`
  - Focused unit and protocol tests.
- `Fenestra.IntegrationTests`
  - Launch server, connect sample clients, verify handshake and selected requests.

## Major subsystems to build

## 1. Transport and session management

Responsibilities:

- Listen on X11 display sockets/ports.
- Accept connections.
- Perform setup handshake.
- Route decoded requests to per-client sessions.
- Manage byte order and sequence numbers.

Key design points:

- Keep transport streams agnostic to protocol implementation.
- Make authentication pluggable; start with "no auth" in dev mode.
- Support cancellation and graceful shutdown from day one.

## 2. X11 protocol layer

Responsibilities:

- Parse setup requests.
- Decode requests.
- Encode replies, events, and errors.
- Track extension opcodes.

Key design points:

- Use explicit request/response types rather than "bag of bytes" handlers.
- Prefer a generated or table-driven opcode registry after the basic server works.
- Separate parsing from behavior; the parser should not mutate server state directly.

## 3. Resource and object model

Responsibilities:

- Manage XIDs.
- Store windows, pixmaps, GCs, cursors, fonts, atoms, colormaps, visuals.
- Validate object ownership and lookup rules.

Key design points:

- A clean resource model makes nearly every later feature easier.
- Build this carefully before chasing extensions.

## 4. Screen, visual, and framebuffer model

Responsibilities:

- Represent screens, roots, visuals, depths, and colormaps.
- Provide backing storage or delegated rendering surfaces.
- Handle exposure, damage, and invalidation.

Key design points:

- Start with one screen and one root window.
- Define abstractions that can later target software buffers, Direct2D, Skia, or GPU paths.

## 5. Rendering and drawing

Responsibilities:

- Implement core drawing requests.
- Manage pixmaps and image transfers.
- Handle copy/fill/text primitives.

Key design points:

- Build in layers:
  - image transfer and simple fills first
  - then pixmap copies
  - then graphics contexts
  - then text and more complex raster ops

## 6. Input and event delivery

Responsibilities:

- Translate native mouse/keyboard input into X11 events.
- Manage focus, grabs, pointer motion, button/key state.
- Deliver ConfigureNotify, Expose, MapNotify, DestroyNotify, and related events.

Key design points:

- Native-to-X11 input mapping is one of the most important integration points.
- Keep keyboard mapping configurable because layouts vary widely.

## 7. Window management policy

Responsibilities:

- Decide decoration strategy.
- Translate X11 top-level windows to host windows.
- Handle transient windows, modal behavior, focus, and stacking.

Key design points:

- Treat this as a separate policy layer, not just ad hoc code in the Win32 host.
- This is where you get native title bars, minimize/maximize, and taskbar integration.

## 8. Native host integration

Responsibilities:

- Create top-level host windows.
- Resize/move/show/hide host windows based on X11 state.
- Feed native input back to the server.
- Present pixels or retained surfaces.

Windows ideas:

- Use Win32 windows with standard styles for decorated top-level X11 windows.
- Map `_NET_WM_STATE`, ICCCM hints, and WM_NORMAL_HINTS into host-window behavior.
- Support undecorated or override-redirect windows for menus, tooltips, popups.
- Use a separate composition surface per top-level window so each app feels native.

Cross-platform ideas:

- macOS host via AppKit or a cross-platform UI surface backend.
- Linux host via Wayland/X11 native windows for testing server behavior cross-platform.

## 9. Extensions

Not all extensions are equal in value. Prioritize by unlock potential:

1. `BIG-REQUESTS`
2. `SHAPE`
3. `XFIXES`
4. `RANDR`
5. `RENDER`
6. `COMPOSITE`
7. `DAMAGE`
8. `MIT-SHM`

Support them only after the core object model and event system are stable.

## Suggested solution organization over time

### Phase A: one solution, few projects

Keep a single `Fenestra.sln` with the current projects. This reduces friction while you
are still learning the protocol and experimenting.

### Phase B: expand into feature-oriented projects

Once the basic server works, add:

- `Fenestra.Rendering`
- `Fenestra.Wm`
- `Fenestra.Extensions`
- `Fenestra.Tests`

### Phase C: Wayland support

At that point choose one of two models:

#### Option 1: separate Wayland solution folder in same repo

- `Fenestra.Protocol.Wayland`
- `Fenestra.Wayland.Compositor`
- `Fenestra.NativeHost.*`

This is a good fit if Wayland becomes a first-class peer to X11.

#### Option 2: keep one server core, multiple front doors

- shared server/resource/composition infrastructure
- X11 frontend
- Wayland frontend

This is a good fit if you want a unified display server architecture with different client
protocols sharing the same rendering and host layers.

I would lean toward **Option 2** long term, but only after the X11 path is solid.

## Cool implementation ideas

## Native top-level windows

Make each mapped top-level X11 window appear as a native OS window:

- native frame and title bar
- standard close/minimize/maximize
- taskbar or dock presence
- snap and tiling integration on Windows

This can make legacy X11 apps feel surprisingly at home on Windows.

## Hybrid decoration policy

Offer multiple modes:

- **native-decorated**: top-level X11 windows get OS chrome
- **server-decorated**: custom decorations for more accurate X11/WM behavior
- **headless/compositor**: no host windows, useful for testing and screenshots

## Per-window rendering backends

Allow different presentation strategies:

- software bitmap blit
- Skia surfaces
- Direct2D/DirectComposition on Windows
- OpenGL/Vulkan later

## X11 root as a virtual desktop

Experiment with mapping the X11 root window to:

- a hidden coordination surface for native top-level hosting, or
- an optional single desktop-host window for debugging.

## Wayland future

There are two interesting ways to support Wayland later:

1. Build a **Wayland compositor/server** using the same server-core and native-host ideas.
2. Build an **X11 compatibility layer over the Wayland-native backend**, where X11 windows
   and Wayland surfaces both become first-class host surfaces.

The safest path is:

- first build X11 correctly enough to run simple clients
- then stabilize host abstractions
- then introduce Wayland as another frontend protocol

## Design risks to watch

- X11 protocol scope is much larger than it looks at first.
- Native-window integration can conflict with exact X11 window-manager semantics.
- Input and focus behavior can become tricky quickly.
- Extensions can multiply complexity; avoid implementing too many too early.

## Practical rule of thumb

When uncertain, keep asking:

> Is this code protocol-specific, server-core-specific, rendering-specific, or host-specific?

If each piece stays in the right layer, the project will remain understandable even as it
grows to support both X11 and Wayland.
