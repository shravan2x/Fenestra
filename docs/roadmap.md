# Fenestra Roadmap

This roadmap is intended for someone who is comfortable with C# and Visual Studio
but is still learning the X11 protocol. Each phase produces a coherent checkpoint
that can be tested before moving on.

## Phase 0 - Bootstrap

Status: complete.

Notes:
- Repository structure, solution layout, and architecture/planning docs exist.
- The bootstrap host has been replaced by a functioning transport/server scaffold.

Goal:
- Establish the repository layout and solution structure.
- Clarify subsystem boundaries before protocol work begins.

Deliverables:
- Visual Studio solution and starter projects.
- Architecture notes and implementation roadmap.
- Basic application host that documents intended startup flow.

## Phase 1 - Learn the X11 connection lifecycle

Status: complete.

Notes:
- The server accepts TCP client connections, parses setup requests, and returns setup replies.
- Little-endian and big-endian setup flows are covered by focused automated tests.

Goal:
- Build the minimum server that accepts a client connection and parses the setup request.

Focus areas:
- X11 byte order handling.
- Setup request parsing.
- Setup response encoding.
- Display number binding and connection acceptance.

Deliverables:
- `Fenestra.Transport` can accept client sockets.
- `Fenestra.Protocol.X11` parses setup packets.
- `Fenestra.Server` returns a valid setup response.
- Logging for connection lifecycle and protocol errors.

Suggested validation:
- Connect with a simple X11 client or a custom probe.
- Confirm the server handles both little-endian and big-endian setup messages.

## Phase 2 - Core object model and resource tracking

Status: complete.

Notes:
- Implemented: per-client state, XID allocation, root window/screen/visual defaults, atom table, resource registry, non-root window storage, property storage, colormaps, cursors, and ownership validation helpers.
- Later phases still extend and use this model, but the Phase 2 core object-model deliverables themselves are now covered.

Goal:
- Represent the server-side state needed by real X11 clients.

Focus areas:
- Clients and sequence numbers.
- Resource IDs.
- Windows, pixmaps, graphics contexts, colormaps, cursors, atoms.
- Event masks and subscriptions.

Deliverables:
- A resource registry.
- A per-client state object.
- A root window model and screen description.
- Basic atom table and property storage.

Suggested validation:
- Unit tests for resource allocation and lookup.
- Golden tests for atom and property behavior.

## Phase 3 - Request dispatch and minimal usable windowing

Status: complete.

Notes:
- Implemented: opcode dispatch, error generation, `CreateWindow`, `DestroyWindow`, `MapWindow`, `UnmapWindow`, `ConfigureWindow`, `ReparentWindow`, `ChangeProperty`, `DeleteProperty`, `GetProperty`, `InternAtom`, `QueryTree`, `GetGeometry`, and `SelectInput`.
- Implemented: non-root window tree mutation, property storage over the wire, and structure/property event infrastructure needed by this phase.

Goal:
- Support the subset of requests needed for basic clients to create and manage windows.

High-value requests to prioritize:
- `CreateWindow`
- `DestroyWindow`
- `MapWindow`
- `UnmapWindow`
- `ConfigureWindow`
- `ReparentWindow`
- `ChangeProperty`
- `GetProperty`
- `InternAtom`
- `QueryTree`
- `GetGeometry`
- `SelectInput`

Deliverables:
- Opcode dispatcher.
- Error generation for unsupported or invalid requests.
- Window tree state transitions.
- Property and event infrastructure.

Suggested validation:
- A tiny sample client that creates and maps a window.
- Integration tests around request decoding and reply encoding.

## Phase 4 - Native window hosting on Windows

Status: partially complete.

Notes:
- Implemented: native host abstraction, server-side native window coordinator, Win32 host lifecycle methods, and bootstrap/root host window wiring.
- Missing relative to the full phase intent: mapped non-root X11 top-level windows affecting the native host via `MapWindow` / `ConfigureWindow`, plus host actions flowing back into real X11 window lifecycle behavior.

Goal:
- Make mapped X11 top-level windows appear as real Win32 windows with normal frame controls.

Focus areas:
- Mapping X11 top-level windows to HWNDs.
- Translating native close/minimize/move/resize actions back into X11 events.
- Maintaining child windows inside host surfaces or composited representations.

Design ideas:
- Use one native window per top-level X11 window.
- Preserve native caption bar, border, minimize/maximize/close buttons.
- Reflect X11 metadata like title, size hints, and icons onto the native window.
- Keep an internal scene graph for X11 child windows rendered within the top-level host.

Deliverables:
- `Fenestra.NativeHost.Win32` creates top-level native windows.
- `MapWindow` and `ConfigureWindow` affect the native host.
- WM_DELETE_WINDOW and focus changes are bridged to X11 client events.

Suggested validation:
- A sample X11 app appears as a normal Windows window.
- Moving and resizing the native window updates the X11-side geometry.

## Phase 5 - Drawing and presentation

Status: partially complete.

Notes:
- Implemented: software framebuffer for the root drawable, pixmaps, minimal graphics contexts, `CreatePixmap`, `FreePixmap`, `CreateGC`, `FreeGC`, `PutImage`, and `GetImage`.
- Implemented: root framebuffer presentation hook through the native host substrate.
- Missing relative to the full phase intent: `CopyArea`, richer GC/raster-op semantics, broader drawable/window presentation, and a fuller compositor/blitter story.

Goal:
- Render actual window contents rather than only window metadata.

Possible approaches:
1. Start with a software framebuffer per window and draw with SkiaSharp.
2. Later add GPU-backed paths via Direct2D, Direct3D, Vulkan, or OpenGL.
3. Support shared memory extensions later for performance-sensitive clients.

Focus areas:
- `PutImage`, `GetImage`, `CopyArea`, `CreatePixmap`, `FreePixmap`.
- Graphics contexts and raster ops.
- Expose and damage handling.
- Backing store strategy.

Deliverables:
- A minimal compositor or blitter.
- Window invalidation and repaint scheduling.
- A path from X11 drawing requests to pixels on screen.

Suggested validation:
- Render a test pattern window.
- Run simple X clients that draw lines, rectangles, and text.

## Phase 6 - Events, input, and window manager behavior

Status: partially complete.

Notes:
- Implemented: root-level `SelectInput`, `SetInputFocus`, `GetInputFocus`, queued outbound event delivery, host-input translation into key/button/motion/focus events, and event-mask gating.
- Missing relative to the full phase intent: window-specific input beyond the root, enter/leave/configure events tied to real window lifecycle, grabs, clipboard/selections, and broader window-manager behavior.

Goal:
- Handle user interaction well enough for real applications.

Focus areas:
- Keyboard and pointer input translation.
- Focus model.
- Enter/leave, expose, configure, button, and motion events.
- Selections and clipboard.
- ICCCM and EWMH basics where relevant to clients.

Deliverables:
- Native input translated into X11 events.
- Focus and activation state synchronization.
- Clipboard bridge between X11 selections and the OS clipboard.

Suggested validation:
- Keyboard input works in test clients.
- Mouse capture and movement are delivered correctly.

## Phase 7 - Compatibility and extensions

Status: not started.

Goal:
- Expand support to the X11 features that common applications expect.

Possible extension targets:
- MIT-SHM
- RANDR
- XFixes
- SHAPE
- RENDER
- XInput2

Note:
- Do not attempt every extension early. Build based on target applications and observed failures.

## Phase 8 - Wayland strategy

Status: planning only.

Notes:
- The roadmap still describes long-term architectural options, but there is no Wayland implementation yet.

There are several viable directions. Pick one based on your long-term goals.

### Option A - Shared compositor core, multiple frontends

Recommended for long-term architecture.

Structure:
- Keep a protocol-neutral scene/window/input core.
- Implement an X11 frontend that feeds that core.
- Later implement a Wayland frontend that feeds the same core.

Pros:
- Cleaner architecture.
- Cross-platform story is stronger.
- Shared rendering and native host layers.

Cons:
- Requires more discipline up front.

### Option B - X11-first with Wayland compatibility layer later

Structure:
- Build the X11 server to completion first.
- Later add a Wayland layer by adapting the common abstractions.

Pros:
- Faster path to first working X11 result.

Cons:
- More refactoring risk later.

### Option C - Hybrid desktop remoting model

Interesting for experimentation.

Structure:
- Treat X11 and Wayland both as client protocols.
- Emit both into a common "desktop object model".
- Host resulting windows as native OS windows.

Pros:
- Potentially novel and flexible.
- Good fit for Windows-native hosting.

Cons:
- Conceptually ambitious and more complex.

Recommendation:
- Start with Option B tactically, but keep the code shaped so it can evolve toward Option A.

## Suggested solution organization over time

Keep the current solution for now:
- `Fenestra.App`
- `Fenestra.Server`
- `Fenestra.Protocol.X11`
- `Fenestra.Transport`
- `Fenestra.NativeHost.Abstractions`
- `Fenestra.NativeHost.Win32`

Add these later when the codebase grows:
- `Fenestra.Rendering`
- `Fenestra.Compositor`
- `Fenestra.Input`
- `Fenestra.Protocol.Wayland`
- `Fenestra.NativeHost.Gtk` or `Fenestra.NativeHost.AppKit`
- `Fenestra.Tests`
- `Fenestra.Samples`

## Practical first milestones

If you want a concrete order to start coding:

1. Parse and answer the X11 setup handshake.
2. Define resource IDs, atoms, and the root window model.
3. Implement create/map/destroy/configure window requests.
4. Create real native Win32 windows for mapped top-level X11 windows.
5. Translate native close and resize actions back into X11 events.
6. Implement enough drawing to show pixels in those windows.
7. Add keyboard and mouse input.
8. Expand protocol coverage based on the first real applications you try.

## Useful development habits

- Keep a protocol reference open while implementing opcodes.
- Add request/reply trace logging early.
- Record unsupported opcodes seen from real clients.
- Build tiny protocol probes before trying large X11 applications.
- Prefer vertical slices: handshake -> create window -> map window -> draw -> input.
