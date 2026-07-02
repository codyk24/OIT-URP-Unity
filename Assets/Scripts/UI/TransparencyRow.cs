using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OIT;

namespace OITViewer
{
    /// <summary>
    /// Attach to the row prefab. Serialize references to the row's Slider, Toggle, and
    /// label in the Inspector. Call <see cref="Initialize"/> once to bind to an
    /// <see cref="OITObject"/>.
    /// </summary>
    public sealed class TransparencyRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text  objectLabel;
        [SerializeField] private Slider    alphaSlider;
        [SerializeField] private Toggle    opaqueToggle;

        private OITObject _target;
        private float     _lastTransparentAlpha;

        public OITObject Target => _target;

        public void Initialize(OITObject target)
        {
            _target               = target;
            _lastTransparentAlpha = target.alpha;

            if (objectLabel  != null) objectLabel.text = target.gameObject.name;

            alphaSlider.minValue = 0f;
            alphaSlider.maxValue = 1f;
            alphaSlider.SetValueWithoutNotify(target.alpha);

            bool isFullyOpaque = !target.enabled;
            opaqueToggle.SetIsOnWithoutNotify(isFullyOpaque);
            alphaSlider.interactable = !isFullyOpaque;

            alphaSlider.onValueChanged.AddListener(OnSliderChanged);
            opaqueToggle.onValueChanged.AddListener(OnToggleChanged);
        }

        private void OnSliderChanged(float value)
        {
            _lastTransparentAlpha = value;
            if (_target != null && _target.enabled)
                _target.alpha = value;
        }

        private void OnToggleChanged(bool fullyOpaque)
        {
            if (_target == null) return;

            if (fullyOpaque)
            {
                _lastTransparentAlpha = _target.alpha;
                _target.alpha         = 1f;
                _target.enabled       = false;

                alphaSlider.SetValueWithoutNotify(1f);
                alphaSlider.interactable = false;
            }
            else
            {
                _target.alpha         = _lastTransparentAlpha;
                _target.enabled       = true;

                alphaSlider.SetValueWithoutNotify(_lastTransparentAlpha);
                alphaSlider.interactable = true;
            }
        }
    }
}
