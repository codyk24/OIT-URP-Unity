## Tech Stack
- Unity 6.3 LTS
- Rendering with Unity's Universal Render Pipeline (URP)
- WebGPU graphics API for web builds
- GitHub for repository hosting

## Project Structure
- Assets/Scripts - Houses C# scripts
- Assets/Textures - Model textures to be used in scene
- Assets/Materials - Materials to be used for rendering models in the scene
- Assets/Shaders - Houses shader code

## Glossary
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