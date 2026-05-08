# OIT + CSG Viewer — Implementation Strategy

## Context

The project is a Unity 6.3 LTS / URP 17.3.0 web application targeting Chrome via WebGPU. It renders 3D objects with order-independent transparency (OIT) using per-pixel linked lists and screen-space CSG boolean subtraction via stencil buffers. The repo is at foundation stage: documentation is complete, packages are installed, and one script (`Assets/Scripts/OrbitCamera.cs`) exists. No shaders, no custom render passes, and no tests exist yet.

PRD: `docs/prd/CSG_OIT_Viewer_PRD.md`  
Verification: `docs/verification/OIT_Verification_Strategy.md`  
Existing script to reuse: `Assets/Scripts/OrbitCamera.cs`  
URP renderer asset to extend: `Assets/Settings/URP-Balanced-Renderer.asset`

---

## Branching Strategy (Git Flow)

```
main          ← production-ready, tagged releases only
  └─ develop  ← integration; all features merge here
       ├─ feature/test-infrastructure
       ├─ feature/oit-runtime-core
       ├─ feature/oit-buffer-management
       ├─ feature/opaque-pass
       ├─ feature/csg-stencil-pass
       ├─ feature/cap-face-pass
       ├─ feature/oit-geometry-pass
       ├─ feature/oit-resolve-pass
       ├─ feature/final-composite-pass
       ├─ feature/scene-manager
       ├─ feature/input-manager
       ├─ feature/ui-transparency-toggle
       └─ feature/ci-cd
```

- All `feature/*` branches cut from `develop`, merged back via PR.
- Every PR must pass the active CI tier before merging.
- `release/x.y` cut from `develop` when a milestone is complete; merged to both `main` and `develop`.
- Hotfixes branch from `main` and merge to both `main` and `develop`.

**Milestone releases:**
| Tag | Contents |
|-----|----------|
| `v0.1-infra` | Test infrastructure + CI Tier 1 smoke |
| `v0.2-cpu-logic` | CPU-side OIT classes + all UNIT-* tests green |
| `v0.3-passes` | All 6 render passes + INT-INIT/GEO/RES/CSG/CAP/COMP green |
| `v0.4-scene` | Scene manager + input manager wired; visual regression baselines committed |
| `v1.0-rc` | Full PRD feature set; CI Tier 2 green; manual browser checklist passing |

---

## Feature Branches — Ordered by Dependency

### Phase 1 — Foundation (Week 1)

#### `feature/test-infrastructure`
**Goal:** Unity Test Runner discovers a trivial test in every assembly before any runtime code is written.

Files to create:
- `Assets/OIT/Runtime/OIT.Runtime.asmdef`
- `Assets/OIT/Editor/OIT.Editor.asmdef`
- `Assets/Tests/EditMode/OIT.Tests.EditMode.asmdef`
- `Assets/Tests/PlayMode/OIT.Tests.PlayMode.asmdef`
- `Assets/Tests/Performance/OIT.Tests.Performance.asmdef`
- `Assets/Tests/Scenes/OIT_Unit_Scene.unity`
- `Assets/Tests/Scenes/OIT_Integration_Scene.unity`
- `Assets/Tests/Scenes/OIT_Performance_Scene.unity`
- `Assets/Tests/Scenes/OIT_EdgeCase_Scene.unity`
- `Assets/Tests/PlayMode/Utilities/RenderingTestUtility.cs`
  - `CaptureFrame(Camera, width, height) → Texture2D`
  - `CompareToReference(Texture2D actual, string refName, float tolerancePct) → float`
- `Assets/OIT/Editor/ReferenceImageGenerator.cs` (editor menu item)
- `Assets/Tests/ReferenceImages/` (Git-LFS tracked)
- One trivial `[Test]` in each EditMode and PlayMode assembly to validate discovery

**Test gate before merge:** Unity Test Runner lists all 5 assemblies; trivial tests pass.

---

#### `feature/ci-cd` (starts in parallel with test-infrastructure)
**Goal:** Tier 1 smoke CI runs on every commit within the same week.

Files to create:
- `.github/workflows/oit-verification.yml`
  - `smoke-tests` job: `game-ci/unity-test-runner@v4`, `testMode: EditMode`, `testPathPattern: OIT.Tests.EditMode`, Unity `6000.3.14f1`
  - `integration-tests` job (gated on smoke): `testMode: PlayMode`, `testPathPattern: OIT.Tests.PlayMode`
  - `nightly-full` job (cron, self-hosted GPU runner): performance + visual tiers

Expand CI to Tier 2 after `v0.3-passes` milestone. Expand to Tier 3/4 after `v0.4-scene`.

---

### Phase 2 — CPU Logic (Week 2)

#### `feature/oit-runtime-core`
**Goal:** All `UNIT-*` tests (35 total) green against pure C# classes; no GPU required.

Files to create under `Assets/OIT/Runtime/`:
- `OITColorPacking.cs` — `PackRGBA(Color) → uint`, `UnpackRGBA(uint) → Color`
- `OITInsertionSort.cs` — C# mirror of the GPU insertion sort (used for unit testing and shader authoring reference)
- `OITUnderCompositing.cs` — `Composite(OITFragment[] sortedFragments, Color background) → Color`
- `OITBufferSizing.cs` — `HeadBufferBytes(int w, int h)`, `NodeBufferBytes(int w, int h, int maxLayers)`, `SelectMaxLayers(long graphicsMemoryBytes) → int`
- `CSGStencilLogic.cs` — CPU state machine for stencil rule validation (test harness only)

Files to create under `Assets/Tests/EditMode/`:
- `OITColorPackingTests.cs` — UNIT-OIT-01 through 05
- `OITInsertionSortTests.cs` — UNIT-SORT-01 through 08
- `OITUnderCompositingTests.cs` — UNIT-COMP-01 through 07
- `OITBufferSizeTests.cs` — UNIT-BUF-01 through 06
- `CSGStencilLogicTests.cs` — UNIT-CSG-01 through 05
- `OrbitCameraTests.cs` — UNIT-CAM-01 through 04 (target: `Assets/Scripts/OrbitCamera.cs`)

**Test gate before merge:** All 35 UNIT-* EditMode tests pass. CI Tier 1 green.

---

### Phase 3 — GPU Buffers (Week 3)

#### `feature/oit-buffer-management`
**Depends on:** `feature/oit-runtime-core` merged to develop

Files to create under `Assets/OIT/Runtime/`:
- `OITBufferManager.cs` — allocates/releases `OIT_HeadBuffer`, `OIT_NodeBuffer`, `OIT_AtomicCounter` as `GraphicsBuffer`s; calls `OITBufferSizing.SelectMaxLayers()` for fallback; exposes `ClearBuffers()` command
- `OITResources.cs` — singleton ScriptableObject holding buffer references; accessed by render passes

Files to create under `Assets/Tests/PlayMode/`:
- `OITPipelineInitTests.cs` — INT-INIT-01 through 06

Buffer sizes:
- `OIT_HeadBuffer`: `screenWidth × screenHeight × 4 bytes`
- `OIT_NodeBuffer`: `screenWidth × screenHeight × MAX_LAYERS × 12 bytes`; MAX_LAYERS=24, fallback to 16
- `OIT_AtomicCounter`: 4 bytes

**Test gate before merge:** INT-INIT-01 through 06 green in PlayMode.

---

### Phase 4 — Render Passes (Weeks 3–4, mostly parallel)

Each pass is a `ScriptableRenderPass` injected into `URP-Balanced-Renderer.asset`. Each pass instruments a `ProfilingSampler("OIT.{PassName}")` for performance tests. Passes 2–5 depend on Pass 1 being merged first; Passes 2 and 3 can be developed in parallel; Passes 4 and 5 depend on buffer management.

#### `feature/opaque-pass` ← merge first
**Goal:** Pass 1 scaffolding; establishes the `OITRenderFeature` hook into URP.

Files to create:
- `Assets/OIT/Runtime/Passes/OpaquePass.cs` — thin wrapper around URP's built-in opaque draw; ensures correct injection point (`BeforeRenderingOpaques` or reuse URP built-in)
- `Assets/OIT/Runtime/OITRenderFeature.cs` — `ScriptableRendererFeature` that registers all 6 passes; add to `URP-Balanced-Renderer.asset` manually after creation

**Test gate before merge:** Scene renders opaque geometry; no GPU errors.

---

#### `feature/csg-stencil-pass` (parallel with cap-face)
**Depends on:** `feature/opaque-pass` merged

Files to create:
- `Assets/OIT/Runtime/Passes/CSGStencilPass.cs`
  - Back-face sub-pass: stencil increment on depth pass, `ColorWriteMask = 0`
  - Front-face sub-pass: stencil decrement on depth pass, `ColorWriteMask = 0`
  - Supports up to 2 active cutters; gracefully ignores a 3rd (EDGE-CUT-02)
- `Assets/OIT/Runtime/CSGCutter.cs` — MonoBehaviour; registers self with `CSGSystem`; exposes `cutterMesh`, `capColor`
- `Assets/OIT/Runtime/CSGSystem.cs` — maintains list of active cutters; passes to render pass each frame
- `Assets/Shaders/CSGStencil.shader` — stencil-only shader (`ColorWriteMask 0`, stencil write)

Files to create under `Assets/Tests/PlayMode/`:
- `CSGStencilPassTests.cs` — INT-CSG-01 through 05

**Test gate before merge:** INT-CSG-01 through 05 green.

---

#### `feature/cap-face-pass` (parallel with csg-stencil)
**Depends on:** `feature/csg-stencil-pass` merged (needs stencil values)

Files to create:
- `Assets/OIT/Runtime/Passes/CapFacePass.cs` — renders cap geometry where `stencil != 0 AND target depth present`; applies small depth bias (PRD §9, cap z-fighting risk)
- `Assets/Shaders/CapFace.shader` — unlit, solid color; stencil read (`ref 0, comp NotEqual`)

Files to create under `Assets/Tests/PlayMode/`:
- `CapFacePassTests.cs` — INT-CAP-01 through 05

**Test gate before merge:** INT-CAP-01 through 05 green.

---

#### `feature/oit-geometry-pass`
**Depends on:** `feature/oit-buffer-management` merged, `feature/csg-stencil-pass` merged

Files to create:
- `Assets/OIT/Runtime/Passes/OITGeometryPass.cs` — disables depth write; draws all OIT-tagged objects; clears buffers at frame start; skips CSG-masked pixels via stencil test
- `Assets/Shaders/OITGeometry.shader`
  ```hlsl
  // Fragment: atomic append to OIT_HeadBuffer / OIT_NodeBuffer
  uint nodeIndex = atomicAdd(OIT_AtomicCounter[0], 1);
  if (nodeIndex < MAX_NODES) {
      OIT_NodeBuffer[nodeIndex].color = PackRGBA(color, alpha);
      OIT_NodeBuffer[nodeIndex].depth = fragDepth;
      uint prevHead;
      InterlockedExchange(OIT_HeadBuffer[pixelIndex], nodeIndex, prevHead);
      OIT_NodeBuffer[nodeIndex].next = prevHead;
  }
  ```
- `Assets/OIT/Runtime/OITObject.cs` — MonoBehaviour tag; registers with `OITSystem`; exposes alpha slider value

Files to create under `Assets/Tests/PlayMode/`:
- `OITGeometryPassTests.cs` — INT-GEO-01 through 05

**Test gate before merge:** INT-GEO-01 through 05 green.

---

#### `feature/oit-resolve-pass`
**Depends on:** `feature/oit-geometry-pass` merged

Files to create:
- `Assets/OIT/Runtime/Passes/OITResolvePass.cs` — dispatches compute shader; 1 thread per pixel; writes to `OIT_ResolveTexture`
- `Assets/Shaders/OITResolve.compute`
  - Gather fragments from linked list
  - Insertion sort by depth (front-to-back, max MAX_LAYERS iterations)
  - Under-compositing: `result = front + back * (1 - front.alpha)`
  - Write to resolve texture

Files to create under `Assets/Tests/PlayMode/`:
- `OITResolvePassTests.cs` — INT-RES-01 through 04

**Test gate before merge:** INT-RES-01 through 04 green.

---

#### `feature/final-composite-pass`
**Depends on:** `feature/oit-resolve-pass` merged

Files to create:
- `Assets/OIT/Runtime/Passes/FinalCompositePass.cs` — full-screen blit; blends `OIT_ResolveTexture` over opaque + cap color buffer; outputs to backbuffer
- `Assets/Shaders/FinalComposite.shader` — full-screen blit shader

Files to create under `Assets/Tests/PlayMode/`:
- `FinalCompositePassTests.cs` — INT-COMP-01 through 04
- `PassOrderingTests.cs` — INT-ORDER-01 through 04

**Test gate before merge:** INT-COMP + INT-ORDER green.

**Milestone `v0.3-passes`:** Cut `release/0.3` from develop. All INT-* tests green. Merge to main and tag.

---

### Phase 5 — Scene, Input, and UI (Week 4)

#### `feature/scene-manager`
**Depends on:** `feature/final-composite-pass` merged

Files to create:
- `Assets/Scripts/SceneManager.cs` — instantiates objects and cutters from Addressables at startup; shows loading screen until complete
- `Assets/Scripts/LoadingScreen.cs` — simple UI overlay during asset loading
- Add `com.unity.addressables` to `Packages/manifest.json`
- Author cutter prefab assets (trapezoidal prism mesh, `CSGCutter` component, interaction handle gizmo)

Note: `Assets/Scripts/OrbitCamera.cs` is already authored — wire it to the scene pivot here.

**Test gate before merge:** Scene loads all objects within 5s (PERF-STARTUP-01); no console errors.

---

#### `feature/input-manager`
**Depends on:** `feature/scene-manager` merged (needs scene objects to target)

Files to create:
- `Assets/Scripts/InputManager.cs`
  - LMB drag → orbit (delegates to `OrbitCamera.cs`)
  - RMB drag → pan pivot
  - Scroll → zoom (dolly)
  - LMB drag on cutter interaction handle → raycast to world-space plane → translate cutter
  - Snap cutter to scene bounds

**Test gate before merge:** PERF-INPUT-01, PERF-INPUT-02 green; manual cutter drag smoke test passes.

---

#### `feature/ui-transparency-toggle`
**Depends on:** `feature/scene-manager` merged

Files to create:
- `Assets/Scripts/UI/TransparencyPanel.cs` — per-object alpha slider + opacity toggle; updates `OITObject.alpha`; routes fully-opaque objects to opaque pass

**Test gate before merge:** Objects toggled fully opaque route through opaque pass (EDGE-OPQ-01 green).

**Milestone `v0.4-scene`:** Cut `release/0.4`. Generate first visual regression reference images. Commit to `Assets/Tests/ReferenceImages/` (Git-LFS). Enable CI Tier 2 in GitHub Actions.

---

### Phase 6 — Edge Cases, Performance, and Visual Regression (Week 5)

All edge case and performance tests are authored in this phase and run against the completed feature set.

Files to create:
- `Assets/Tests/PlayMode/EdgeCases/BufferOverflowTests.cs` — EDGE-OVF-01 through 03
- `Assets/Tests/PlayMode/EdgeCases/ZeroTransparentTests.cs` — EDGE-ZERO-01, 02, EDGE-OPQ-01
- `Assets/Tests/PlayMode/EdgeCases/CutterLimitTests.cs` — EDGE-CUT-01 through 03
- `Assets/Tests/PlayMode/EdgeCases/DegenerateCameraTests.cs` — EDGE-CAM-01 through 03
- `Assets/Tests/PlayMode/EdgeCases/ResolutionEdgeCaseTests.cs` — EDGE-RES-01 through 03
- `Assets/Tests/Performance/FrameRateTests.cs` — PERF-FPS-01 through 04
- `Assets/Tests/Performance/PassBudgetTests.cs` — PERF-PASS-01 through 05
- `Assets/Tests/Performance/StartupLatencyTests.cs` — PERF-STARTUP-01, 02, PERF-INPUT-01, 02
- `Assets/Tests/Performance/MemoryTests.cs` — PERF-MEM-01 through 03
- `Assets/Tests/PlayMode/Visual/OITVisualCorrectnessTests.cs` — VIS-OIT-01 through 05, VIS-CSG-01 through 04, VIS-CAP-01 through 03
- `Assets/Tests/Performance/Baselines/` — capture baselines on reference hardware (Intel Iris Xe or Apple M1)

Performance baseline filenames must include GPU model per verification strategy.

---

### Phase 7 — WebGPU Validation and Release (Week 6)

Files to create:
- `.github/workflows/webgpu-smoke.yml` — Playwright browser smoke (PLAT-GPU-01 through 04, PLAT-FLAG-01, 02)
  ```
  1. Unity CLI WebGPU build: -buildTarget WebGL -executeMethod BuildScript.BuildWebGPU
  2. python -m http.server 8080
  3. Playwright: navigate, wait for <canvas>, assert non-blank, assert no "GPUDevice lost"
  ```
- `Assets/Scripts/PlatformValidator.cs` — startup `SystemInfo.supportsComputeShaders` check; user-facing error if false (PLAT-GPU-03)

Enable CI Tier 3 (nightly, self-hosted GPU runner) and Tier 4 (manual release gate).

**Milestone `v1.0-rc`:** Cut `release/1.0-rc`. Full PRD success criteria verified. Manual browser acceptance checklist completed and documented.

---

## Critical File Paths

| File | Role |
|------|------|
| `Assets/Scripts/OrbitCamera.cs` | Existing — reuse directly in InputManager |
| `Assets/Settings/URP-Balanced-Renderer.asset` | Add `OITRenderFeature` here |
| `Assets/OIT/Runtime/OITRenderFeature.cs` | Registers all 6 passes into URP |
| `Assets/OIT/Runtime/OITBufferManager.cs` | Central GPU buffer lifecycle |
| `Assets/Shaders/OITGeometry.shader` | Per-pixel linked list append |
| `Assets/Shaders/OITResolve.compute` | Insertion sort + under-compositing |
| `Assets/Shaders/CSGStencil.shader` | Stencil-only cutter |
| `Assets/Shaders/CapFace.shader` | Solid cap color |
| `Assets/Shaders/FinalComposite.shader` | Full-screen OIT blend |
| `Packages/manifest.json` | Add `com.unity.addressables` |
| `.github/workflows/oit-verification.yml` | 4-tier CI pipeline |

---

## Testing Integration Summary

Every feature branch carries its own tests and must not be merged until they pass. The CI tier active at each milestone:

| Milestone | CI Tier Active | Gate |
|-----------|---------------|------|
| `v0.1-infra` | Tier 1 (smoke) | EditMode trivial tests pass |
| `v0.2-cpu-logic` | Tier 1 | All 35 UNIT-* pass |
| `v0.3-passes` | Tier 1 + Tier 2 | All INT-* pass |
| `v0.4-scene` | Tier 1 + Tier 2 | Visual baselines committed; EDGE/PERF pass |
| `v1.0-rc` | Tier 1–4 | Browser checklist complete |

Performance regression gates (from verification strategy):
- Median frame time +10% → fail PR
- P95 frame time +20% → fail PR
- Memory +5% → fail PR

---

## Verification Sign-Off

End-to-end confirmation steps after `v1.0-rc`:
1. Run full CI Tier 3 (nightly) on self-hosted Intel Iris Xe or Apple M1 runner — all tests green.
2. Execute manual browser acceptance checklist in Chrome 120+ on Windows and macOS — all 10 items checked.
3. Confirm `chrome://gpu` shows no `GPUDevice lost` after 5 minutes of cutter drag interaction.
4. Validate ≥ 30 FPS sustained on integrated GPU at 1080p with 4 transparent objects + 2 cutters active.
5. Confirm PRD success criteria table (§10) fully satisfied.
