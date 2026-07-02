using UnityEngine;
using UnityEngine.UI;

namespace OITViewer
{
	public class ToggleGraphicColor : BaseToggleColor
	{
		#region Fields

		[SerializeField]
		private Graphic m_graphic;

		#endregion

		#region Methods

		protected override void SetColor(Color color)
		{
			m_graphic.color = color;
		}

		#endregion
	}
}