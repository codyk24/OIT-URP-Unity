using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OITViewer
{
    public class ToggleGameObject : MonoBehaviour
    {
		#region Fields

		[SerializeField]
		private Toggle m_toggle;

		[SerializeField]
		private List<GameObject> m_items = new List<GameObject>();

		#endregion

		#region Properties

		public Toggle toggle
		{
			get => m_toggle;
			set
			{
				if (m_toggle == value)
					return;

				Unregister();
				m_toggle = value;
				Register();
			}
		}

		public List<GameObject> items => m_items;

		#endregion

		#region Methods

		private void Register()
		{
			if (m_toggle != null)
			{
				m_toggle.onValueChanged.AddListener(ValueChanged);
				ValueChanged(toggle.isOn);
			}
		}

		private void Unregister()
		{
			if (m_toggle != null)
			{
				m_toggle.onValueChanged.RemoveListener(ValueChanged);
			}
		}

		private void OnEnable()
		{
			Register();
		}

		private void OnDisable()
		{
			Unregister();
		}

		private void ValueChanged(bool isOn)
		{
			foreach (var item in m_items)
			{
				item.SetActive(isOn);
			}
		}

		#endregion
    }
}
