using UnityEngine;

namespace OIT
{
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField]
        private Transform lookAtTransform;

        [SerializeField]
        private float orbitSpeed = 10f;

        [SerializeField, Range(1f, 100f)]
        private float minDistance = 1f;

        [SerializeField, Range(1f, 500f)]
        private float maxDistance = 50f;

        [SerializeField, Range(-89f, 0f)]
        private float minPitch = -80f;

        [SerializeField, Range(0f, 89f)]
        private float maxPitch = 80f;

        private float   _yaw;
        private float   _pitch;
        private float   _distance = 5f;
        // Panning shifts the virtual orbit centre without reparenting scene content.
        private Vector3 _pivotOffset = Vector3.zero;

        private void Start()
        {
            if (lookAtTransform == null)
                return;

            Vector3 offset = transform.position - lookAtTransform.position;
            _distance = offset.magnitude;
            _yaw   = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(offset.y / Mathf.Max(offset.magnitude, 0.0001f)) * Mathf.Rad2Deg;
        }

        /// <summary>Rotate the orbit by the given yaw (horizontal) and pitch (vertical) deltas, in degrees.</summary>
        public void Orbit(float deltaYaw, float deltaPitch)
        {
            _yaw   += deltaYaw   * orbitSpeed * Time.deltaTime;
            _pitch  = Mathf.Clamp(_pitch - deltaPitch * orbitSpeed * Time.deltaTime, minPitch, maxPitch);
            ApplyTransform();
        }

        /// <summary>Dolly the camera along its forward axis by scrollDelta units.</summary>
        public void Zoom(float scrollDelta)
        {
            _distance = Mathf.Clamp(_distance - scrollDelta, minDistance, maxDistance);
            ApplyTransform();
        }

        /// <summary>
        /// Shifts the virtual orbit centre by <paramref name="worldDelta"/> without moving
        /// <see cref="lookAtTransform"/> or any of its children.  The caller is responsible for
        /// converting screen-space deltas into camera-relative world-space before calling this.
        /// </summary>
        public void PanPivot(Vector3 worldDelta)
        {
            _pivotOffset += worldDelta;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (lookAtTransform == null)
                return;

            Vector3    effectivePivot = lookAtTransform.position + _pivotOffset;
            Quaternion rotation       = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position        = effectivePivot + rotation * new Vector3(0f, 0f, -_distance);
            transform.LookAt(effectivePivot);
        }
    }
}
