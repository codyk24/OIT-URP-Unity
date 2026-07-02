using UnityEngine;
using UnityEngine.UI;

namespace OITViewer
{
	public abstract class BaseToggleStyle : MonoBehaviour
	{
		#region Fields

		[SerializeField]
		private Toggle m_toggle;

		#endregion

		#region Properties

		public Toggle toggle => m_toggle;

		#endregion

		#region Methods

		private void OnEnable()
		{
			toggle.onValueChanged.AddListener(ValueChanged);
			ValueChanged(toggle.isOn);
		}

		private void OnDisable()
		{
			toggle.onValueChanged.RemoveListener(ValueChanged);
		}

		protected abstract void ValueChanged(bool isOn);

		#endregion
	}
}
