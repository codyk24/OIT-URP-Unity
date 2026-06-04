using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OITViewer
{
    /// <summary>
    /// Simple full-screen UI overlay shown while Addressable assets are loading.
    /// Attach to a Canvas GameObject with a Panel child covering the full screen.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class LoadingScreen : MonoBehaviour
    {
        [Tooltip("Optional label displayed while loading.")]
        [SerializeField] private TextMeshProUGUI loadingLabel;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        public void Show()
        {
            _canvas.enabled = true;

            if (loadingLabel != null)
                loadingLabel.text = "Loading…";
        }

        public void Hide()
        {
            _canvas.enabled = false;
        }
    }
}
