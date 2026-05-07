# OIT-URP-Unity Verification Strategy

## Context

The project is a Unity 6 URP application targeting WebGPU/Chrome that implements Order-Independent Transparency (OIT) via per-pixel linked lists and screen-space CSG with stencil buffer tricks. The project is at the foundation stage (no OIT implementation yet, no tests). This strategy establishes a complete verification plan from unit isolation through browser-level acceptance, designed to be built in parallel with the OIT implementation.

**Primary source of truth:** [`docs/prd/CSG_OIT_Viewer_PRD.md`](docs/prd/CSG_OIT_Viewer_PRD.md)  
**Existing runtime script:** [`Assets/Scripts/OrbitCamera.cs`](Assets/Scripts/OrbitCamera.cs)  
**URP renderer asset (where render features register):** [`Assets/Settings/URP-Balanced-Renderer.asset`](Assets/Settings/URP-Balanced-Renderer.asset)  
**Test packages already in manifest:** `com.unity.test-framework@1.6.0`, `com.unity.test-framework.performance@3.4.0`

---

## Rendering Architecture Under Test

**OIT Algorithm:** Per-pixel linked lists using WebGPU storage buffers  
- `OIT_HeadBuffer`: `uint` per pixel (linked list head, sentinel = `0xFFFFFFFF`)  
- `OIT_NodeBuffer`: MAX_LAYERS=24 nodes per pixel × 12 bytes each (packed RGBA8 color, float depth, uint next)  
- `OIT_AtomicCounter`: single `uint` for node allocation  
- Buffer size at 1080p, MAX_LAYERS=24: ~640 MB; fallback to MAX_LAYERS=16 on memory pressure

**6-Pass Pipeline:**
1. Opaque Pass
2. CSG Stencil Pass (back-face increment, front-face decrement per cutter)
3. Cap Face Pass (solid color at cut boundary)
4. OIT Geometry Pass (fragment append to linked lists)
5. OIT Resolve Pass (compute shader: insertion sort + under-compositing)
6. Final Composite Pass (blend OIT resolve over opaque + cap)

---

## Part 1 — Test Infrastructure

### Assembly Definitions

Create the following `.asmdef` files:

| Assembly | Path | References |
|----------|------|-----------|
| `OIT.Runtime` | `Assets/OIT/Runtime/` | `Unity.RenderPipelines.Universal.Runtime` |
| `OIT.Editor` | `Assets/OIT/Editor/` | `OIT.Runtime`, `Unity.RenderPipelines.Universal.Editor` |
| `OIT.Tests.EditMode` | `Assets/Tests/EditMode/` | `OIT.Runtime`, `NUnit.Framework`; editor-only |
| `OIT.Tests.PlayMode` | `Assets/Tests/PlayMode/` | `OIT.Runtime`, `Unity.PerformanceTesting` |
| `OIT.Tests.Performance` | `Assets/Tests/Performance/` | `OIT.Runtime`, `Unity.PerformanceTesting` |

### Test Scenes

Never test in `SampleScene.unity`. Create:

| Scene | Purpose |
|-------|---------|
| `Assets/Tests/Scenes/OIT_Unit_Scene.unity` | Minimal (1 camera, 1 light); PlayMode unit tests spawn objects procedurally |
| `Assets/Tests/Scenes/OIT_Integration_Scene.unity` | 2 opaque + 2 transparent objects, 1 cutter, all 6 passes active |
| `Assets/Tests/Scenes/OIT_Performance_Scene.unity` | 4 transparent objects (≤50k tris each), 2 cutters — mirrors production |
| `Assets/Tests/Scenes/OIT_EdgeCase_Scene.unity` | 0 transparent objects, 24-layer overflow rig, fully opaque variant |

### Reference Images

- Directory: `Assets/Tests/ReferenceImages/` tracked in Git-LFS  
- Format: PNG, 1920×1080, 32-bit RGBA  
- Naming: `{TestName}_{Platform}_{GPU}_{yyyy-MM-dd}.png`  
- Author `Assets/OIT/Editor/ReferenceImageGenerator.cs` — editor menu item that runs each visual test scene and saves captures to this directory  
- Commit new references when visual changes are intentional; include Unity version, GPU model, and PRD version in commit message

### Pixel Comparison Utility

`Assets/Tests/PlayMode/Utilities/RenderingTestUtility.cs`:
- `CaptureFrame(Camera, width, height) → Texture2D` — renders to RenderTexture, reads via `ReadPixels`
- `CompareToReference(Texture2D actual, string refName, float tolerancePct) → float` — per-pixel RGBA L1 distance; returns fraction of pixels exceeding tolerance; fail if > threshold (default 0.1%)

---

## Part 2 — Unit Tests (EditMode, No GPU)

**Concept:** Pure C# logic, no Unity player loop. Runs in milliseconds.

### 2.1 Color Packing — `OITColorPackingTests.cs`

| ID | Description | Assert |
|----|-------------|--------|
| `UNIT-OIT-01` | `PackRGBA(1,0,0,1)` round-trips through `UnpackRGBA` | Per-channel error < 1/255 |
| `UNIT-OIT-02` | `PackRGBA(0,0,0,0)` → `0x00000000` | Exact uint equality |
| `UNIT-OIT-03` | `PackRGBA(1,1,1,1)` → `0xFFFFFFFF` | Exact uint equality |
| `UNIT-OIT-04` | 16 boundary alpha values pack/unpack | Relative error < 0.5% per channel |
| `UNIT-OIT-05` | 256 random colors pack/unpack | No channel exceeds 1/255 error |

### 2.2 Insertion Sort (CPU Simulation) — `OITInsertionSortTests.cs`

Write a C# copy of the GPU insertion sort for isolated unit testing.

| ID | Description | Assert |
|----|-------------|--------|
| `UNIT-SORT-01` | Sort 1 fragment | Output equals input |
| `UNIT-SORT-02` | Sort 2 fragments, near in front | Correct front-to-back order |
| `UNIT-SORT-03` | Sort 2 fragments, far in front (reverse) | Swapped to correct order |
| `UNIT-SORT-04` | Sort 24 fragments, pre-sorted | Output equals input |
| `UNIT-SORT-05` | Sort 24 fragments, reverse-sorted | Monotonically non-decreasing depths |
| `UNIT-SORT-06` | Sort 24 fragments, random depths | Monotonically non-decreasing depths |
| `UNIT-SORT-07` | Equal depth values | Original order preserved (stable) |
| `UNIT-SORT-08` | Sort 0 fragments | No exception; empty output |

### 2.3 Under-Compositing (CPU Simulation) — `OITUnderCompositingTests.cs`

| ID | Description | Assert |
|----|-------------|--------|
| `UNIT-COMP-01` | 1 fully opaque fragment over black | Output equals fragment color |
| `UNIT-COMP-02` | 1 fully transparent fragment | Output equals background |
| `UNIT-COMP-03` | Red α=0.5 over Blue α=0.5 | Matches hand-calculated under-composite formula |
| `UNIT-COMP-04` | 24 layers, all α=0.1 | Accumulated alpha within expected bounds |
| `UNIT-COMP-05` | Red-front vs blue-front produce different outputs | Results not equal |
| `UNIT-COMP-06` | All fragments α=0 | Output equals opaque background |
| `UNIT-COMP-07` | nodeIndex >= MAX_NODES (overflow path) | No crash; result matches truncated composite |

### 2.4 Buffer Sizing — `OITBufferSizeTests.cs`

| ID | Description | Assert |
|----|-------------|--------|
| `UNIT-BUF-01` | HeadBuffer at 1920×1080 | = 8,294,400 bytes (exact) |
| `UNIT-BUF-02` | NodeBuffer at 1920×1080, MAX_LAYERS=24 | ≈ 640 MB |
| `UNIT-BUF-03` | NodeBuffer at 1920×1080, MAX_LAYERS=16 | ≈ 426 MB |
| `UNIT-BUF-04` | NodeBuffer at 1280×720, MAX_LAYERS=24 | Correct scaled value |
| `UNIT-BUF-05` | AtomicCounter buffer | = 4 bytes (exact) |
| `UNIT-BUF-06` | Memory pressure fallback: mock `graphicsMemorySize` below threshold | Returns MAX_LAYERS=16 |

### 2.5 CSG Stencil Logic (State Machine) — `CSGStencilLogicTests.cs`

| ID | Description | Assert |
|----|-------------|--------|
| `UNIT-CSG-01` | Pixel inside convex cutter volume | Net stencil > 0 |
| `UNIT-CSG-02` | Pixel outside cutter volume | Net stencil = 0 |
| `UNIT-CSG-03` | Two separate cutters on two objects | No cross-contamination |
| `UNIT-CSG-04` | Cutter outside scene (no geometry at pixel) | Depth test fails; no mark |
| `UNIT-CSG-05` | Cutter fully enclosing target | All target pixels marked |

### 2.6 Camera Math — `OrbitCameraTests.cs`

Tests target the existing [`Assets/Scripts/OrbitCamera.cs`](Assets/Scripts/OrbitCamera.cs).

| ID | Description | Assert |
|----|-------------|--------|
| `UNIT-CAM-01` | `RotateAround` 360° returns to start | Distance error < 0.001f |
| `UNIT-CAM-02` | Orbit distance maintained after rotation | Distance from pivot = `orbitDistance` ± 0.001f |
| `UNIT-CAM-03` | LookAt pivot stays on screen center | Forward vector points toward pivot |
| `UNIT-CAM-04` | `lookAtTransform = null` | No NullReferenceException in `Update()` |

---

## Part 3 — Integration Tests (PlayMode)

**Concept:** Full URP player loop in editor. Uses `[UnityTest]` + `IEnumerator`, `SceneManager.LoadScene`, and ReadBuffer calls.

### 3.1 Pipeline Initialization — `OITPipelineInitTests.cs`

| ID | Assert |
|----|--------|
| `INT-INIT-01` | All 3 OIT buffers non-null after scene load |
| `INT-INIT-02` | HeadBuffer.length == screenWidth × screenHeight |
| `INT-INIT-03` | NodeBuffer.length == screenWidth × screenHeight × MAX_LAYERS |
| `INT-INIT-04` | AtomicCounter[0] == 0 after buffer clear pass |
| `INT-INIT-05` | HeadBuffer all-sentinel (0xFFFFFFFF) after clear pass |
| `INT-INIT-06` | Low-VRAM mock → NodeBuffer uses MAX_LAYERS=16 |

### 3.2 OIT Geometry Pass — `OITGeometryPassTests.cs`

| ID | Assert |
|----|--------|
| `INT-GEO-01` | AtomicCounter > 0 after 1 transparent sphere rendered |
| `INT-GEO-02` | AtomicCounter = 0 with no transparent objects |
| `INT-GEO-03` | Node at center pixel of sphere has correctly packed material color |
| `INT-GEO-04` | CSG-masked pixel: no node appended for the masked object |
| `INT-GEO-05` | Two overlapping transparent objects: linked list at overlap pixel has 2 nodes |

### 3.3 OIT Resolve Pass — `OITResolvePassTests.cs`

| ID | Assert |
|----|--------|
| `INT-RES-01` | Resolve texture pixel alpha > 0 at known transparent object location |
| `INT-RES-02` | Fully opaque scene: all resolve texture pixels are zero |
| `INT-RES-03` | MAX_LAYERS=24 fragments: no GPU error, frame completes |
| `INT-RES-04` | 30 overlapping quads (overflow): counter clamped to MAX_NODES; no crash |

### 3.4 CSG Stencil Pass — `CSGStencilPassTests.cs`

| ID | Assert |
|----|--------|
| `INT-CSG-01` | Stencil != 0 at pixel inside cutter projection |
| `INT-CSG-02` | Stencil == 0 at pixel outside cutter projection |
| `INT-CSG-03` | Non-target object pixel never marked by cutter |
| `INT-CSG-04` | Two cutters on two objects: independent stencil values, no crossover |
| `INT-CSG-05` | Cutter ColorWriteMask=0: no color written to render target |

### 3.5 Cap Face Pass — `CapFacePassTests.cs`

| ID | Assert |
|----|--------|
| `INT-CAP-01` | Cap color visible at known cut-boundary pixel |
| `INT-CAP-02` | Outside cut boundary: object color, not cap color |
| `INT-CAP-03` | 10 consecutive frames at same position: cap pixel stable (no z-fighting) |
| `INT-CAP-04` | Cap face composites correctly under transparent object alpha |
| `INT-CAP-05` | Cutter in empty space: cap pixel = background |

### 3.6 Final Composite Pass — `FinalCompositePassTests.cs`

| ID | Assert |
|----|--------|
| `INT-COMP-01` | Backbuffer alpha-blended result at transparent object pixel |
| `INT-COMP-02` | Opaque object pixels unchanged in final composite |
| `INT-COMP-03` | Final pixel color is between OIT color and opaque color (blended intermediate) |
| `INT-COMP-04` | Fully opaque scene: final buffer = opaque pass output (pixel-exact, 5 samples) |

### 3.7 Pass Ordering — `PassOrderingTests.cs`

| ID | Assert |
|----|--------|
| `INT-ORDER-01` | All 6 passes execute in correct order each frame (instrument with frame counters) |
| `INT-ORDER-02` | OIT Geometry runs after CSG Stencil |
| `INT-ORDER-03` | OIT Resolve runs after OIT Geometry (NodeBuffer populated at dispatch) |
| `INT-ORDER-04` | Final Composite executes last |

---

## Part 4 — Rendering Correctness (Visual Regression)

### OIT Sorting Artifact Tests — `OITVisualCorrectnessTests.cs`

| ID | Scene | Camera | Pass Criteria |
|----|-------|--------|---------------|
| `VIS-OIT-01` | 2 spheres: red (near), blue (far), 50% alpha | Front view | Reference match ≤ 0.1% pixel error |
| `VIS-OIT-02` | Same scene, camera 180° (reverse apparent order) | Back view | Same result as VIS-OIT-01 |
| `VIS-OIT-03` | Same scene | 8 camera positions (45° increments) | All 8 frames within 0.1% of respective references |
| `VIS-OIT-04` | 24 quads, alternating red/blue, 5% alpha | Top-down | Reference match ≤ 0.5% |
| `VIS-OIT-05` | 0 transparent objects | Any | Reference match ≤ 0.05% |

### CSG Artifact Tests

| ID | Scene | Expected |
|----|-------|----------|
| `VIS-CSG-01` | Cutter halfway into sphere | Clean cut boundary at 6 camera angles; no bleed |
| `VIS-CSG-02` | Two cutters on two adjacent spheres | Cut on A not visible on B and vice versa |
| `VIS-CSG-03` | Cutter outside object bounds | No visible cut |
| `VIS-CSG-04` | Cutter fully inside object | Object fully removed in cutter region |

### Cap Face Tests

| ID | Expected |
|----|----------|
| `VIS-CAP-01` | Cap color matches `configuredCapColor` exactly at cut-boundary pixels |
| `VIS-CAP-02` | Sharp boundary (no sub-pixel bleed) |
| `VIS-CAP-03` | 30 frames at same position: cap pixel std dev = 0 (no flicker) |

---

## Part 5 — Performance Benchmarking

All performance tests use `Unity.PerformanceTesting`. Instrument each `ScriptableRenderPass.Execute()` with `ProfilingSampler("OIT.{PassName}")`.

### Frame Rate — `FrameRateTests.cs`

```csharp
yield return Measure.Frames()
    .WarmupCount(60)
    .MeasurementCount(300)
    .ProfilerMarkers("OIT.OpaquePass", "OIT.CSGStencilPass", /* ... */)
    .Run();
```

| ID | Scene | Metric | Target | Fail |
|----|-------|--------|--------|------|
| `PERF-FPS-01` | 4 transparent objects, 2 cutters, 1080p | Frame time median | ≤ 33.3 ms | > 40 ms |
| `PERF-FPS-02` | 1 transparent object, no cutters, 1080p | Frame time median | ≤ 20 ms | > 30 ms |
| `PERF-FPS-03` | Fully opaque scene, 1080p | Frame time median | ≤ 10 ms | > 15 ms |
| `PERF-FPS-04` | MAX_LAYERS=16 fallback, 1080p | Frame time median | ≤ 28 ms | > 35 ms |

### Per-Pass GPU Budget

| ID | Pass | Budget |
|----|------|--------|
| `PERF-PASS-01` | OIT Geometry | ≤ 8 ms at 4 transparent objects |
| `PERF-PASS-02` | OIT Resolve (compute) | ≤ 6 ms at 1080p, MAX_LAYERS=24 |
| `PERF-PASS-03` | CSG Stencil | ≤ 2 ms per active cutter |
| `PERF-PASS-04` | Cap Face | ≤ 1 ms per active cutter |
| `PERF-PASS-05` | Final Composite | ≤ 2 ms |

### Startup and Input Latency

| ID | Metric | Target |
|----|--------|--------|
| `PERF-STARTUP-01` | Addressables fully loaded from scene open | < 5 seconds |
| `PERF-STARTUP-02` | Buffer allocation time on first frame | < 200 ms |
| `PERF-INPUT-01` | 100 simulated cutter drag events: input event → transform update | < 16 ms each |
| `PERF-INPUT-02` | Frame time during 60-frame continuous drag | No frame > 33.3 ms |

### Memory

| ID | Assert |
|----|--------|
| `PERF-MEM-01` | GPU buffer total at 1080p, MAX_LAYERS=24 | ≤ 650 MB |
| `PERF-MEM-02` | GPU buffer total at 1080p, MAX_LAYERS=16 | ≤ 440 MB |
| `PERF-MEM-03` | No re-allocation during steady-state (300 frames) | GC delta < 1 MB |

---

## Part 6 — Edge Cases

### Buffer Overflow

| ID | Setup | Assert |
|----|-------|--------|
| `EDGE-OVF-01` | 25 overlapping quads at single pixel | Counter clamped to MAX_NODES; no crash |
| `EDGE-OVF-02` | 25 overlapping quads full-screen | Remaining fragments dropped; no exception |
| `EDGE-OVF-03` | Overflow at frame N, remove extras at frame N+1 | Counter resets; normal rendering resumes |

### Zero Transparent / Opaque-Only Scenes

| ID | Assert |
|----|--------|
| `EDGE-ZERO-01` | Opaque scene with OIT pipeline active | Counter = 0; resolve texture clear; composite = opaque pass |
| `EDGE-ZERO-02` | Disable all transparent objects mid-session | Next frame: no stale fragments |
| `EDGE-OPQ-01` | All α=1.0 objects routed to opaque pass | No node appended |

### Cutter Count Limits

| ID | Assert |
|----|--------|
| `EDGE-CUT-01` | 2 simultaneous cutters | Both cuts visible; no interference |
| `EDGE-CUT-02` | 3rd cutter activated | Graceful ignore or logged error; first 2 cuts unchanged; no crash |
| `EDGE-CUT-03` | Cutter deactivated mid-frame | Stencil cleared; target renders as uncut immediately |

### Degenerate Camera

| ID | Assert |
|----|--------|
| `EDGE-CAM-01` | orbitDistance = 0 | No NullReferenceException; clamps to minimum distance |
| `EDGE-CAM-02` | Camera looking along +Y (gimbal lock) | No NaN in transform |
| `EDGE-CAM-03` | Camera inside transparent object | OIT composites correctly; cap faces visible |

### Resolution Edge Cases

| ID | Assert |
|----|--------|
| `EDGE-RES-01` | 1×1 screen resolution | Buffers allocate 1 element; no division-by-zero |
| `EDGE-RES-02` | Resolution changes at runtime | Buffers reallocated; next frame renders correctly |
| `EDGE-RES-03` | 75% resolution scaling | Buffer sizes reflect scaled res; upscale without artifacts |

---

## Part 7 — Regression Strategy

### Visual Regression Gate

- Every PR runs `OITVisualCorrectnessTests` suite
- Failure threshold: > 0.1% of pixels with per-channel L1 distance > 2/255
- Shader or pass code changes without updated reference images = CI failure
- Reference image updates require explicit commit with GPU/Unity version metadata

### Performance Regression Gate

Store baselines in `Assets/Tests/Performance/Baselines/` (filename includes GPU model). `IPostBuildCleanup` step compares:

- Median frame time regression > 10% → fail PR
- P95 frame time regression > 20% → fail PR  
- Memory allocation increase > 5% → fail PR

Baselines updated only with explicit CI flag (`-updateBaseline`) after deliberate tradeoff decision.

### CI Test Tiers

| Tier | Contents | Trigger | Duration |
|------|----------|---------|---------|
| Smoke (T1) | All EditMode + INT-INIT + EDGE-ZERO + EDGE-OVF-01 | Every commit | < 2 min |
| Integration (T2) | All PlayMode integration + visual correctness (5 views) | Every PR | < 15 min |
| Full (T3) | T2 + all performance + all edge cases + 8-view visual | Nightly | < 60 min |
| Release (T4) | T3 + manual browser acceptance checklist | Pre-release | 2–4 hrs |

---

## Part 8 — Platform Testing (WebGPU / Chrome)

### Compute Shader Validation

| ID | Method | Assert |
|----|--------|--------|
| `PLAT-GPU-01` | WebGPU build → Chrome 120+, Windows | Counter increments; resolve texture matches editor reference within 0.5% |
| `PLAT-GPU-02` | WebGPU build → Chrome 120+, macOS (M1/M2) | Same; validates Metal → WebGPU path |
| `PLAT-GPU-03` | `SystemInfo.supportsComputeShaders` check on startup | Logs check; shows user-facing error if false |
| `PLAT-GPU-04` | 30-frame run: check Chrome DevTools GPU crash log | No GPU process errors |

### Feature Flag

| ID | Method |
|----|--------|
| `PLAT-FLAG-01` | WebGPU flag disabled in Chrome | Graceful "WebGPU not supported" message; no white screen |
| `PLAT-FLAG-02` | Chrome 115+ (WebGPU default) | No flag prompt required |

### Windows vs macOS Parity

| Check | Criteria |
|-------|---------|
| Visual output | macOS within 1% of Windows reference (Metal vs D3D12 precision) |
| CSG stencil behavior | Identical |
| Frame rate at 1080p | ≥ 30 FPS on both |
| Startup time | < 5 s on both |

### Manual Browser Acceptance Checklist (Chrome, per release)

Record: Chrome version, OS, GPU, date.

- [ ] Page loads without JS console errors
- [ ] OIT scene renders within 5 seconds
- [ ] No sorting artifacts visible (human inspection)
- [ ] CSG cutter drag updates cut every visible frame
- [ ] Cap face visible at boundary, correct color
- [ ] Two simultaneous cutters behave independently
- [ ] Orbit / pan / zoom functional
- [ ] No `GPUDevice lost` in `chrome://gpu` after 5 minutes of use
- [ ] ≥ 30 FPS on integrated GPU (Intel Iris Xe or Apple M1/M2)
- [ ] No crash after 10 minutes of continuous use

---

## Part 9 — CI/CD Integration

### GitHub Actions Structure

```yaml
# .github/workflows/oit-verification.yml
on: [push, pull_request]

jobs:
  smoke-tests:           # Tier 1
    runs-on: ubuntu-latest
    steps:
      - uses: game-ci/unity-test-runner@v4
        with:
          testMode: EditMode
          testPathPattern: "OIT.Tests.EditMode"
          unityVersion: 6000.3.14f1

  integration-tests:     # Tier 2
    needs: smoke-tests
    runs-on: ubuntu-latest
    steps:
      - uses: game-ci/unity-test-runner@v4
        with:
          testMode: PlayMode
          testPathPattern: "OIT.Tests.PlayMode"
          unityVersion: 6000.3.14f1

  nightly-full:          # Tier 3 (cron)
    if: github.event_name == 'schedule'
    runs-on: self-hosted   # GPU required
    steps:
      - uses: game-ci/unity-test-runner@v4
        with:
          testMode: PlayMode
          testPathPattern: "OIT.Tests.Performance|OIT.Tests.PlayMode"
          unityVersion: 6000.3.14f1
          customParameters: "-buildTarget WebGL"
```

**GPU runner note:** Performance and visual correctness tests require a real GPU. Use a self-hosted runner matching minimum hardware spec (Intel Iris Xe or Apple M1).

### WebGPU Browser Smoke (Playwright)

```
1. Build WebGPU target via Unity CLI: -buildTarget WebGL -executeMethod BuildScript.BuildWebGPU
2. Serve with: python -m http.server 8080
3. Playwright: navigate to localhost:8080, wait for <canvas>, capture screenshot
4. Assert: canvas non-blank; no console errors containing "GPUDevice lost"
```

### Artifacts to Upload

- NUnit XML results (GitHub test summary)
- Performance JSON diff vs baseline
- Screenshot artifacts on visual failure
- Chrome DevTools console log from Playwright session

### Failure Policy

| Situation | Action |
|-----------|--------|
| Tier 1/2 failure | Block PR merge |
| Tier 3 failure | Open GitHub issue with results; do not block PR |
| Performance regression > 20% | Block PR; require tech lead approval to override |

---

## Part 10 — Implementation Sequence

| Week | Focus |
|------|-------|
| 1 | Create all `.asmdef` files, test scene stubs, `RenderingTestUtility.cs`, reference image generator tool. Validate Unity Test Runner discovers a trivial test in each assembly. |
| 2 | Implement all `UNIT-*` tests against concurrently-authored runtime classes. These define the CPU-side contract for packing, sorting, compositing, and buffer sizing. |
| 3 | Implement `INT-INIT-*` tests (first GPU-touching tests). Validate buffer allocation before writing pass-level tests. |
| 4 | Implement remaining `INT-*` and visual correctness tests. Instrument profiler markers in each pass. Generate first reference images. |
| 5 | Implement all `EDGE-*` and `PERF-*` tests. Capture performance baselines on reference hardware. |
| 6 | Configure GitHub Actions with Tier 1 and Tier 2. Validate Playwright browser smoke against development WebGPU build. |
| Ongoing | Add reference images for new visual features. Update performance baselines for deliberate tradeoffs. Run manual checklist pre-release. |

---

## PRD Acceptance Criteria Coverage

| PRD Criterion | Primary Tests |
|---------------|--------------|
| OIT correctness — no sorting artifacts | `VIS-OIT-01–05`, `UNIT-SORT-01–08` |
| CSG correctness — no stencil leakage | `INT-CSG-03–04`, `VIS-CSG-02`, `UNIT-CSG-03` |
| Cap face — no z-fighting or gaps | `INT-CAP-03`, `VIS-CAP-01`, `VIS-CAP-03` |
| Real-time cutter drag — no stutter | `PERF-INPUT-01–02`, `PERF-FPS-01` |
| Camera ≥ 30 FPS | `PERF-FPS-01`, `PERF-FPS-04` |
| Cross-object cutter independence | `EDGE-CUT-01`, `INT-CSG-04`, `VIS-CSG-02` |
| Chrome on Windows and macOS | `PLAT-GPU-01–02`, `PLAT-FLAG-01–02`, browser checklist |
| Startup < 5 seconds | `PERF-STARTUP-01` |
