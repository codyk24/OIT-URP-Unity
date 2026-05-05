# 3D CSG-OIT Viewer — Product Requirements Document

**Version:** 1.0  
**Date:** 2026-04-22  
**Status:** Ready for Implementation  
**Target Engine:** Unity (URP) — WebGPU Build Target  
**Target Browser:** Google Chrome  

---

## 1. Executive Summary

This document defines the requirements for a real-time 3D viewer web application embedded in an existing website. The application renders 3D meshes with physically accurate order-independent transparency (OIT) and screen-space constructive solid geometry (CSG) boolean subtraction. Users interact with the scene via orbit/pan/zoom camera controls and real-time cutter object manipulation. The application targets Chrome via Unity's WebGPU build pipeline, with integrated graphics (Intel Iris, Apple M-series) as the minimum supported hardware.

---

## 2. Goals and Non-Goals

### 2.1 Goals

- Render a set of objects in real-time in the browser.
- Support all objects being simultaneously transparent with physically accurate front-to-back OIT compositing.
- Support up to two invisible CSG cutter objects performing visual boolean subtraction on distinct target objects, with a solid-colored cap face rendered at the cut boundary.
- Update CSG cut results continuously and in real-time as cutters are dragged by the user.
- Provide orbit, pan, and zoom camera controls.
- Perform acceptably on integrated GPU hardware (Intel Iris, Apple M-series).
- Deploy as a hosted web application accessible via Chrome from an existing website.

### 2.2 Non-Goals

- Geometrically exact CSG (no runtime mesh topology modification).
- Runtime import of user-supplied mesh files.
- Complex interior texturing on CSG cap faces.
- Authentication or user account management.
- Support for browsers other than Chrome.
- Support for CSG cutter-on-cutter interaction.
- Mobile or native application builds.

---

## 3. User Stories

| ID | As a... | I want to... | So that... |
|----|---------|-------------|-----------|
| US-01 | Viewer | See objects in scene rendered in 3D with correct transparency | I can understand spatial relationships between objects |
| US-02 | Viewer | Make all objects simultaneously transparent | I can see through all the objects in the scene at once |
| US-03 | Viewer | See correct front-to-back transparency ordering | I can accurately judge which object is closer/further |
| US-04 | Viewer | Orbit, pan, and zoom around the 3D models | I can examine 3D objects from any angle |
| US-05 | Viewer | Move a CSG cutter object within the scene and see the target object subtracted in real time | I can simulate target object resection interactively |
| US-06 | Viewer | See a solid colored cross-section face at the cut plane | I can understand the geometry of the resection |
| US-07 | Viewer | Have the cut update every frame as I drag the CSG cutter object | I can interactively explore resection placement |

---

## 4. System Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                   Chrome Browser (WebGPU)               │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │              Unity WebGPU Runtime                │   │
│  │                                                  │   │
│  │  ┌─────────────┐    ┌─────────────────────────┐  │   │
│  │  │  Input &    │    │   Rendering Pipeline     │  │   │
│  │  │  Interaction│───▶│   (URP + Custom Passes)  │  │   │
│  │  │  Manager    │    │                          │  │   │
│  │  └─────────────┘    │  1. Opaque Pass          │  │   │
│  │                     │  2. CSG Stencil Pass      │  │   │
│  │  ┌─────────────┐    │  3. Cap Face Pass         │  │   │
│  │  │  Scene &    │    │  4. OIT Geometry Pass     │  │   │
│  │  │  object       │───▶│  5. OIT Resolve Pass      │  │   │
│  │  │  Manager    │    │  6. Composite Pass        │  │   │
│  │  └─────────────┘    └─────────────────────────┘  │   │
│  │                                                  │   │
│  │  ┌─────────────────────────────────────────────┐ │   │
│  │  │         Bundled Asset Store (Addressables)  │ │   │
│  │  │   3D Meshes | Cutter Meshes | Materials   │ │   │
│  │  └─────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### 4.1 Key Subsystems

| Subsystem | Responsibility |
|-----------|---------------|
| **Scene Manager** | Instantiates objects and cutters from bundled assets at startup, manages scene state |
| **Camera Manager** | Handles orbit/pan/zoom camera input and cutter drag input |
| **Rendering Pipeline** | Custom URP render pipeline with multi-pass OIT and screen-space CSG |
| **OIT System** | Per-pixel linked list OIT using WebGPU compute shaders and storage buffers |
| **CSG System** | Screen-space stencil-based boolean subtraction with cap face rendering |
| **Asset Pipeline (Offline)** | Preprocessing tool for mesh optimization prior to Unity build |

---

## 5. Rendering Pipeline Design

The rendering pipeline is implemented as a set of custom URP `ScriptableRenderPass` instances injected into URP's frame loop. The full pipeline executes in the following order each frame:

### 5.1 Pass Order

```
Frame Start
    │
    ▼
┌─────────────────────────────────────┐
│ Pass 1: Opaque Pass                 │
│ Render all opaque objects │
│ to color + depth buffer             │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ Pass 2: CSG Stencil Pass            │
│ For each active cutter:             │
│  - Render cutter back faces to      │
│    stencil only (no color write)    │
│  - Render cutter front faces to     │
│    stencil only (no color write)    │
│  - Mark subtracted pixels in        │
│    stencil buffer                   │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ Pass 3: Cap Face Pass               │
│ For each active cutter:             │
│  - Render a screen-aligned or       │
│    world-space cap quad/mesh at     │
│    cutter boundary planes           │
│  - Write solid cap color only where │
│    stencil marks subtraction AND    │
│    target object depth is present     │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ Pass 4: OIT Geometry Pass           │
│ For each transparent object:          │
│  - Disable depth write              │
│  - Per-pixel: append fragment       │
│    (color, depth, alpha) into       │
│    WebGPU storage buffer            │
│    (per-pixel linked list)          │
│  - Skip fragments in CSG-subtracted │
│    stencil regions                  │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ Pass 5: OIT Resolve Pass (Compute)  │
│ WebGPU Compute Shader:              │
│  - Per pixel: sort linked list by   │
│    depth (insertion sort, max ~20   │
│    fragments)                       │
│  - Composite fragments              │
│    front-to-back using              │
│    under-compositing                │
│  - Write final resolved color       │
│    to OIT resolve texture           │
└─────────────────────────────────────┘
    │
    ▼
┌─────────────────────────────────────┐
│ Pass 6: Final Composite Pass        │
│ Full-screen blit:                   │
│  - Blend OIT resolve texture over   │
│    opaque + cap color buffer        │
│  - Output to backbuffer             │
└─────────────────────────────────────┘
    │
    ▼
Frame End
```

### 5.2 OIT Implementation: Per-Pixel Linked Lists (WebGPU)

**Why:** Integrated GPUs cannot sustain 20+ depth peeling passes per frame. Per-pixel linked lists using WebGPU storage buffers and atomic operations provide accurate OIT in a bounded number of passes regardless of layer count.

**WebGPU Resources Required:**
- `OIT_HeadBuffer`: `RWStructuredBuffer<uint>` — one uint per pixel, stores index of first fragment node. Size: `screenWidth * screenHeight * 4 bytes`.
- `OIT_NodeBuffer`: `RWStructuredBuffer<OITNode>` — fixed-size pool of fragment nodes. Each node contains: `uint color (packed RGBA8)`, `float depth`, `uint next (linked list pointer)`. Size: `screenWidth * screenHeight * MAX_LAYERS * 12 bytes`. Set `MAX_LAYERS = 24`.
- `OIT_AtomicCounter`: `RWStructuredBuffer<uint>` — single atomic counter for node allocation.

**OIT Geometry Pass (Fragment Shader):**
```hlsl
// Per transparent fragment:
uint nodeIndex = atomicAdd(OIT_AtomicCounter[0], 1);
if (nodeIndex < MAX_NODES) {
    OIT_NodeBuffer[nodeIndex].color = PackRGBA(color, alpha);
    OIT_NodeBuffer[nodeIndex].depth = fragDepth;
    uint prevHead;
    InterlockedExchange(OIT_HeadBuffer[pixelIndex], nodeIndex, prevHead);
    OIT_NodeBuffer[nodeIndex].next = prevHead;
}
```

**OIT Resolve Pass (Compute Shader, 1 thread per pixel):**
```hlsl
// Gather all fragments for this pixel
// Insertion sort by depth (front to back)
// Under-compositing: result = front + back * (1 - front.alpha)
// Write to OIT resolve texture
```

### 5.3 CSG Implementation: Screen-Space Stencil Boolean Subtraction

**Why screen-space:** Avoids runtime geometry modification entirely, runs on GPU, updates every frame at zero CPU mesh cost.

**Method (per cutter, per frame):**

1. **Stencil Setup:** Clear stencil to 0 for cutter region.
2. **Back Face Pass:** Render cutter back faces. Depth test ON (less). Stencil: increment on pass.
3. **Front Face Pass:** Render cutter front faces. Depth test ON (less). Stencil: decrement on pass.
4. **Result:** Pixels inside the cutter volume where target object depth exists have stencil != 0.
5. **Suppress OIT and Opaque fragments** in stencil-marked regions for target object using stencil test in Pass 4.

**Cutter Visibility:** Cutter meshes have `ColorWriteMask = 0` — they write only to stencil and depth, never to color. They are invisible to the user.

**Limitation Handling:** This stencil approach is correct for convex cutter shapes. Trapezoidal prisms are convex, so this is fully valid.

### 5.4 Cap Face Rendering

- For each cutter, render a solid-colored polygon at the cutting boundary plane.
- Cap geometry is a world-space quad or low-poly mesh aligned to the cutter's cutting face(s).
- Cap fragment shader: output solid cap color only where `stencil != 0 AND target object depth <= fragment depth`.
- Cap color is a configurable solid color (e.g. a distinct indicator color). No texturing required.
- Cap is rendered in Pass 3 before OIT, so it composites correctly under transparent objects.

### 5.5 Material Summary

| Object | Render Mode | Depth Write | Color Write | Stencil |
|--------|------------|-------------|-------------|---------|
| Opaque objects | Opaque (URP Lit) | Yes | Yes | Read |
| Transparent objects | OIT Fragment Append | No | No (to RT) | Read (exclude CSG region) |
| Cutter mesh | Stencil Only | Yes | No | Write |
| Cap face | Opaque (Unlit) | Yes | Yes | Read (CSG region only) |

---

## 6. Asset Pipeline

### 6.1 Offline Mesh Preprocessing (Pre-Build)

All  meshes are preprocessed before inclusion in the Unity project:

| Step | Tool | Purpose |
|------|------|---------|
| Mesh Import | Unity FBX Importer | Import raw object meshes |
| Normal Recalculation | Unity Import Settings / MeshLab | Smooth normals for organic object surfaces; handle osteophyte regions |
| Mesh Optimization | Unity `Mesh.Optimize()` | Reorder vertices/indices for GPU cache efficiency |
| LOD Generation | Unity LOD Group or Simplygon | Generate 2–3 LOD levels per object for integrated GPU performance |
| Bounds Baking | Unity | Bake accurate bounds for frustum culling |

### 6.2 Runtime Asset Loading

- All meshes bundled using **Unity Addressables**.
- Assets loaded asynchronously at application startup.
- A loading screen is displayed until all assets are ready.
- No runtime mesh import from user-supplied files.

### 6.3 Cutter Mesh Assets

- Trapezoidal prism cutter meshes are authored in Unity or a DCC tool and bundled as standard Unity mesh assets.
- Cap face geometry (quad/polygon) is authored to match each cutter's cutting plane boundary.
- Cutter meshes must be **convex** to ensure stencil CSG correctness.

---

## 7. Interaction Design

### 7.1 Camera Controls

| Input | Action |
|-------|--------|
| Left Mouse Button Drag | Orbit camera around scene pivot |
| Right Mouse Button Drag | Pan camera (translate pivot) |
| Middle Mouse Scroll | Zoom (dolly camera toward/away from pivot) |
| Touch (optional) | Pinch to zoom, single-finger drag to orbit |

- Camera uses a standard **orbit rig**: camera positioned at a fixed radius from a world-space pivot point, with azimuth and elevation angles.
- Zoom is clamped to min/max distances to prevent clipping into geometry.

### 7.2 Cutter Interaction

| Input | Action |
|-------|--------|
| Left Mouse Button Drag on Cutter | Translate cutter along its constrained axis or plane |
| (Optional) Rotation Handle | Reorient cutter if rotation is required |

- Cutter drag uses raycasting against a world-space interaction plane to compute cutter translation each frame.
- Cut updates every frame during drag (continuous real-time update).
- Cutters snap to scene bounds constraints to prevent moving fully outside the object volume.
- Since cutters are invisible, interaction handles or gizmos (e.g., colored arrows or ring handles) must be rendered to indicate cutter position and allow user to grab it.

### 7.3 object Transparency Toggle

- UI controls (e.g., sliders or checkboxes per object) allow users to toggle individual object transparency on/off.
- Transparency alpha value is configurable per object via UI slider (0.0 = fully transparent, 1.0 = fully opaque).
- objects set to fully opaque bypass the OIT pass and render in the standard opaque pass.

---

## 8. Performance Targets and Constraints

### 8.1 Target Hardware

| Hardware Class | Example Devices |
|---------------|----------------|
| Integrated GPU (Minimum) | Intel Iris Xe, Apple M1/M2 integrated GPU |
| Discrete GPU (Optimal) | NVIDIA GeForce, AMD Radeon |

### 8.2 Performance Targets

| Metric | Target |
|--------|--------|
| Frame Rate | ≥ 30 FPS on integrated GPU at 1080p |
| OIT Buffer Size | MAX_LAYERS = 24 fragments per pixel |
| OIT Node Buffer | Pre-allocated; no runtime allocation |
| Input Latency | Cutter drag response < 1 frame (16–33ms) |
| Startup Load Time | < 5 seconds on broadband connection |

### 8.3 Integration GPU Optimizations

- **LOD system:** Use URP LOD Groups to reduce triangle count at distance.
- **Frustum Culling:** Enabled by default via Unity.
- **OIT Buffer Pre-allocation:** Allocate OIT node buffer once at startup; size = `width * height * MAX_LAYERS * 12 bytes`. At 1080p with MAX_LAYERS=24: ~640MB. **Reduce to MAX_LAYERS=16 if memory pressure is detected on device, with a runtime fallback check.**
- **Resolution Scaling:** Optionally render OIT passes at 75% resolution on low-end hardware, upscale for composite.
- **object Mesh Polycount:** Preprocessed LOD0 targets ≤ 50,000 triangles per object mesh.

---

## 9. Technical Risks and Mitigations

| Risk | Severity | Mitigation |
|------|----------|-----------|
| WebGPU compute shader support incomplete in Unity WebGPU build | High | Validate Unity WebGPU compute shader pipeline in a prototype before full build. Monitor Unity WebGPU changelog. |
| OIT node buffer exceeds integrated GPU VRAM | High | Implement runtime MAX_LAYERS fallback (16 or 12). Add resolution scaling option. Profile on Intel Iris target hardware early. |
| Stencil CSG artifacts on non-convex cutter shapes | Medium | Keep cutter geometry strictly convex (trapezoidal prism is convex — validate in asset pipeline). |
| Per-pixel linked list atomic operations causing GPU stalls on integrated hardware | Medium | Profile atomic contention. If severe, evaluate Weighted Blended OIT as a quality-degraded fallback mode with user toggle. |
| Cap face z-fighting with object surface | Medium | Apply a small depth bias to cap face pass. Tune per-scene. |
| Chrome WebGPU feature flag requirements | Low | Document that users must use Chrome with WebGPU enabled (standard in recent Chrome versions). Provide setup instructions on host website. |
| Addressables bundle download size impacting startup time | Low | Compress meshes. Stream non-critical auxiliary objects as secondary bundles after primary objects load. |

---

## 10. Success Criteria

| Criterion | Measure |
|-----------|---------|
| OIT correctness | All transparent objects composite in correct depth order with no visible sorting artifacts at any camera angle |
| CSG correctness | Cutter boolean subtraction visually matches cutter volume on target objects with no stencil leakage to non-target objects |
| Cap face rendering | Solid-color cap face is visible at cut boundary with no z-fighting or gaps |
| Real-time cutter drag | Cut updates every frame during drag with no perceived stutter on integrated GPU |
| Camera controls | Orbit, pan, zoom respond smoothly at ≥ 30 FPS |
| Cross-object independence | Two simultaneous cutters on distinct objects produce independent, non-interfering cuts |
| Browser compatibility | Application loads and runs correctly in Chrome on Windows and macOS |
| Startup time | Scene fully loaded and interactive within 5 seconds on broadband |

---

## 11. Out of Scope

- Geometrically exact / CPU-side CSG mesh boolean operations
- Runtime user mesh import (FBX, OBJ, DICOM, etc.)
- Measurement or annotation tools
- Mobile browser support
- Non-Chrome browser support
- Exporting or saving scene state
- Multi-user / collaborative viewing
- VR/AR modes
- Physics simulation

---

## 12. Glossary

| Term | Definition |
|------|-----------|
| **OIT** | Order-Independent Transparency — rendering technique that produces correct transparency compositing regardless of draw order |
| **Per-Pixel Linked List** | OIT technique using GPU storage buffers to collect all transparent fragments per pixel, then sort and composite them in a resolve pass |
| **CSG** | Constructive Solid Geometry — combining solid shapes using boolean operations (union, intersection, subtraction) |
| **Screen-Space CSG** | CSG effect achieved in the rendering pipeline using stencil and depth buffer tricks, without modifying mesh geometry |
| **Cap Face** | A solid polygon rendered at the cross-section plane of a CSG subtraction to simulate the interior surface of a cut object |
| **URP** | Unity Universal Render Pipeline — Unity's scriptable, cross-platform render pipeline |
| **WebGPU** | Modern browser graphics API exposing GPU compute and rendering capabilities; successor to WebGL |
| **Stencil Buffer** | GPU per-pixel integer buffer used to mask rendering operations |
| **LOD** | Level of Detail — using lower-polygon meshes at greater camera distances to improve performance |
| **Addressables** | Unity asset management system for bundling and loading assets at runtime |
| **Under-Compositing** | Front-to-back transparency blending formula: `result = front + back * (1 - front.alpha)` |

---

*End of Document*
