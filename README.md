# Fenestra

Fenestra is an experimental X11 server written in C#/.NET with a long-term goal
of making X11 applications feel native on Windows and, where practical,
cross-platform.

## Project goals

- Implement the core X11 protocol in managed code.
- Run primarily on Windows, with an architecture that can evolve toward
  cross-platform hosting.
- Translate X11 top-level windows into native operating-system windows with the
  expected frame, caption buttons, taskbar presence, focus, sizing, and z-order
  behavior.
- Leave a clean path for adding a Wayland-facing layer in the future.

## Repository layout

- `src/Fenestra.App` - console entry point used to host the server.
- `src/Fenestra.Server` - orchestration, state, request dispatch, and lifecycle.
- `src/Fenestra.Protocol.X11` - packet models and protocol parsing.
- `src/Fenestra.Transport` - display listeners and client connection transport.
- `src/Fenestra.NativeHost.Abstractions` - interfaces for native window hosting.
- `src/Fenestra.NativeHost.Win32` - Windows-specific native host implementation.
- `docs/architecture.md` - subsystem responsibilities and solution strategy.
- `docs/roadmap.md` - phased implementation plan from bootstrap to real server.

## Opening in Visual Studio

Open `Fenestra.sln` in Visual Studio 2022 or newer. The repository includes
shared .NET build settings in `Directory.Build.props` and editor preferences in
`.editorconfig`.

This cloud environment does not currently have the .NET SDK installed, so the
project files were bootstrapped by hand. Build and run locally in Visual Studio
once the .NET 10 SDK is available.

## Suggested first milestone

The first practical milestone is not "full X11". It is:

1. Accept a client connection on an X11 display port.
2. Parse and respond to the setup handshake.
3. Maintain basic resource IDs and a minimal server state object.
4. Implement a tiny set of requests needed to create and map a top-level window.
5. Show that mapped window as a native window on Windows.

That path gives you visible progress early while keeping the design aligned with
the eventual full server.
