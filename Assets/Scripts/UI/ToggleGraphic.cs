using UnityEngine;

namespace OITViewer
{
	public class ToggleGraphic : BaseToggleStyle
	{
		#region Fields

		[SerializeField]
		private Sprite m_offSprite;

		[SerializeField]
		private Sprite m_onSprite;

		#endregion

		#region Properties

		public Sprite offIcon { get => m_offSprite; set => SetSprite(ref m_offSprite, value); }
		public Sprite onIcon { get => m_onSprite; set => SetSprite(ref m_onSprite, value); }

		#endregion


		#region Methods

		private void SetSprite(ref Sprite target, Sprite value)
		{
			if (target == value)
				return;

			target = value;
		}

		protected override void ValueChanged(bool isOn)
		{
			toggle.image.sprite = isOn ? m_onSprite : m_offSprite;
			toggle.image.enabled = toggle.image.sprite != null;
		}

		#endregion
	}
}
