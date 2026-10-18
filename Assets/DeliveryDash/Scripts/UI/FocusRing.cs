using UnityEngine;
using UnityEngine.UI;

namespace DeliveryDash
{
    /// <summary>Shows a highlight ring while its button has keyboard/gamepad focus.</summary>
    public class FocusRing : MonoBehaviour
    {
        public Selectable Target;
        Image img;
        void Awake() { img = GetComponent<Image>(); }
        void LateUpdate()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            bool on = es && es.currentSelectedGameObject == Target.gameObject;
            img.enabled = on;
        }
    }
}
