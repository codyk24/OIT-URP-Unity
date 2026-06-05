using UnityEngine;

namespace OITViewer
{
    /// <summary>
    /// Marker component placed on any GameObject that acts as a draggable cutter handle.
    /// InputManager raycasts for this component to identify when the user starts a cutter drag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CutterHandle : MonoBehaviour { }
}
