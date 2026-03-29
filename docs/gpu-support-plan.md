# Fenestra GPU Support Plan

This document describes how GPU acceleration could fit into Fenestra, an X11
server written in .NET, and recommends a staged path that does not compromise
correctness or architecture.

## Short answer

GPU support is very possible in .NET, but the difficulty depends on what is
being accelerated:

1. **GPU-accelerated composition**: practical and recommended.
2. **GPU-accelerated 2D drawing pipeline**: practical, but more complex.
3. **Full GLX/OpenGL hardware acceleration**: very difficult and should be
   deferred.

The best first target is **GPU-accelerated composition of already-rendered
window contents**, not full accelerated support for every graphics path from
day one.

## What "GPU acceleration" can mean here

### 1. GPU-accelerated composition

In this model:

- X11 clients still issue normal X11 drawing requests.
- The server produces window contents, initially through software buffers.
- The compositor uploads changed regions to GPU textures.
- The final scene is presented efficiently inside native host windows.

This is the safest and most useful early form of acceleration.

Benefits:

- Faster presentation of top-level windows.
- Good fit for native Win32 window hosting.
- Clean separation from X11 protocol logic.
- Easier path to animations, transparency, and scaling later.

### 2. GPU-accelerated rendering of X11 drawing operations

In this model:

- Pixmaps and window backing stores may live on GPU surfaces.
- Operations such as fills, copies, image transfer, clipping, and alpha
  composition can be executed on the GPU.
- Some requests may still need software fallback or readback.

Benefits:

- Better rendering performance.
- Lower CPU cost for frequent redraws.

Tradeoffs:

- More synchronization complexity.
- More surface lifetime management.
- Potential mismatch between X11 raster semantics and graphics APIs.

### 3. Full OpenGL/GLX acceleration

This is a separate difficulty tier.

It would require support for:

- GLX protocol handling
- context creation and lifetime management
- surface binding and swap behavior
- an OpenGL implementation or translation layer

This is a large project on its own and should be treated as a later
compatibility goal rather than an initial requirement.

## Why .NET is not the main obstacle

The challenge is not the choice of .NET itself. The challenge is:

- protocol complexity
- rendering architecture
- interop with platform graphics APIs
- synchronization between server state and GPU resources

.NET can support GPU work well through:

- P/Invoke or CsWin32-based access to DirectX and Win32 APIs
- SkiaSharp with GPU backends
- Silk.NET or Vortice.Windows for graphics API bindings
- native presentation through HWND-backed targets on Windows

So the question is less "can .NET do this?" and more "how should the rendering
layer be shaped so acceleration can be added cleanly later?"

## Recommended strategy

Build GPU support in stages.

### Stage 1 - Software-first architecture

Goal:

- Make the X11 protocol and server behavior correct before optimizing.

Approach:

- Represent windows and pixmaps through backend-neutral abstractions.
- Start with software buffers and a software compositor.
- Ensure top-level X11 windows can already map to native host windows.

Why:

- Correctness is much easier to verify in a software path.
- Software rendering gives a baseline for comparison when GPU support is added.

### Stage 2 - GPU-accelerated presentation

Goal:

- Present top-level window contents using GPU-backed composition.

Approach:

- Keep rendering data in software at first if needed.
- Upload changed regions to GPU textures.
- Composite child surfaces into the top-level host surface.
- Present through a native backend such as DirectComposition or Skia GPU.

Why:

- This delivers visible performance gains early.
- It fits the goal of showing X11 windows as native OS windows.

### Stage 3 - Selective GPU rendering

Goal:

- Move high-value drawing operations onto GPU-backed surfaces.

Approach:

- Accelerate common operations such as:
  - pixmap copy
  - fills
  - alpha composition
  - image upload paths
- Keep software fallback for unsupported or expensive-to-port cases.

Why:

- X11 semantics are broad, and some operations will be easier to keep in
  software.

### Stage 4 - Reassess GLX

Goal:

- Decide whether full OpenGL compatibility is worth the implementation cost.

Approach:

- Evaluate real applications you want to support.
- Only pursue GLX if target applications require it.

Why:

- Many early project milestones do not require GLX.
- It is better to grow based on real app compatibility needs.

## Suggested architecture for acceleration

The protocol layer should never know whether drawing is software or GPU-backed.
Instead, introduce backend-neutral rendering abstractions later in the project.

Suggested future projects:

- `Fenestra.Rendering`
- `Fenestra.Compositor`
- `Fenestra.Rendering.Software`
- `Fenestra.Rendering.Direct2D` or `Fenestra.Rendering.Skia`

Suggested responsibilities:

### `Fenestra.Rendering`

- surface abstraction
- pixmap storage abstraction
- drawing command interfaces
- image format conversion
- invalidation primitives

### `Fenestra.Compositor`

- scene graph for top-level and child windows
- damage tracking
- clipping and occlusion
- composition scheduling
- presentation to host surfaces

### `Fenestra.NativeHost.Win32`

- native window creation
- swap/present targets for each top-level host window
- native resize/move/focus integration

## Abstractions worth planning for

Potential interfaces:

- `ISurface`
- `IPixmapStore`
- `IRenderContext`
- `ICompositor`
- `IPresentationTarget`

These should let the server core ask for rendering work without depending on
one specific graphics API.

## Windows-specific implementation options

On Windows, these are the most realistic backend families:

### Direct2D + DirectComposition

Pros:

- good fit for 2D composition
- integrates well with native windows
- strong fit for per-window presentation

Cons:

- still requires careful interop design

### Direct3D 11 or 12

Pros:

- flexible and powerful
- useful for advanced composition and effects

Cons:

- lower-level API surface
- more engineering effort

### SkiaSharp with GPU backend

Pros:

- portable rendering story
- easier cross-platform aspirations
- good abstraction for 2D drawing

Cons:

- still needs host integration design
- some platform-specific behavior remains outside Skia

## Cross-platform implications

If cross-platform is a serious goal, separate:

- native host concerns
- rendering/compositor concerns
- protocol concerns

That way:

- Windows can use a Win32 + Direct2D/DirectComposition path
- other platforms can use alternative host and presentation backends
- Wayland support later can reuse more of the rendering and composition core

## Expected difficulty by tier

### GPU composition

Difficulty: **moderate**

This is the best early target and the most realistic payoff.

### GPU drawing pipeline

Difficulty: **moderately hard**

This is valuable, but should follow the software baseline.

### Full GLX acceleration

Difficulty: **very hard**

Treat this as a specialized, later compatibility milestone.

## Recommendation

The recommended plan is:

1. Build a correct software-rendered X11 server first.
2. Introduce backend-neutral rendering interfaces.
3. Add GPU-accelerated composition for top-level native windows.
4. Add selective GPU-backed drawing where it clearly helps.
5. Defer GLX until real application requirements justify it.

## Practical rule of thumb

When deciding where GPU support belongs, keep this boundary in mind:

- **Protocol layer**: X11 messages, requests, replies, events
- **Server core**: state, resources, object lifetime, policy
- **Rendering/compositor**: software vs GPU decision point
- **Native host**: actual presentation inside OS windows

If those boundaries stay intact, GPU acceleration can be introduced without
rewriting the protocol engine.
