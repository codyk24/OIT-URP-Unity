using UnityEngine;

namespace OITViewer
{
	public abstract class BaseToggleColor : BaseToggleStyle
	{
		#region Fields

		[SerializeField]
		private Color m_offColor;

		[SerializeField]
		private Color m_onColor;

		#endregion

		#region Methods

		protected override void ValueChanged(bool isOn)
		{
			SetColor(isOn ? m_onColor : m_offColor);
		}

		protected abstract void SetColor(Color color);

		#endregion
	}
}