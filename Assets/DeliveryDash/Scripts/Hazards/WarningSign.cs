using UnityEngine;

namespace DeliveryDash
{
    /// <summary>A bobbing "!" sign that faces the camera above a hazard about to do something.</summary>
    public class WarningSign : MonoBehaviour
    {
        public Transform Visual;
        Camera cam;
        float shownAt;
        bool shown;

        void Awake()
        {
            cam = Camera.main;
            if (Visual) Visual.gameObject.SetActive(false);
        }

        public void Show(bool on)
        {
            shown = on;
            shownAt = Time.time;
            if (Visual) Visual.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (!shown || !Visual) return;
            if (!cam) cam = Camera.main;
            if (cam) Visual.rotation = Quaternion.LookRotation(Visual.position - cam.transform.position);
            float t = Time.time - shownAt;
            float pop = t < 0.25f ? Mathf.Sin(t / 0.25f * Mathf.PI * 0.5f) * 1.2f : 1f + Mathf.Sin(t * 8f) * 0.06f;
            Visual.localScale = Vector3.one * pop;
            Visual.localPosition = new Vector3(0f, Mathf.Sin(t * 5f) * 0.12f, 0f);
        }
    }
}
