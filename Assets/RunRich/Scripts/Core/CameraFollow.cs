using UnityEngine;

namespace RunRich
{
    // Догоняющая камера: висит сзади-сверху бегуна, идёт за изгибами дороги.
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = GameConfig.CameraOffset;
        [SerializeField] private float lookAhead = GameConfig.CameraLookAhead;
        [SerializeField] private float followSharpness = GameConfig.CameraFollowSharpness;
        [SerializeField] private float lookHeight = 1.15f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapBehindTarget();
        }

        // Телепортирует камеру сразу за цель (старт / рестарт уровня).
        public void SnapBehindTarget()
        {
            if (target == null) return;
            Vector3 forward = FlatForward();
            transform.position = target.position + Quaternion.LookRotation(forward) * offset;
            Vector3 lookPoint = target.position + forward * lookAhead + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 forward = FlatForward();
            Vector3 desiredPosition = target.position + Quaternion.LookRotation(forward) * offset;
            float smoothing = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothing);

            Vector3 desiredLook = target.position + forward * lookAhead + Vector3.up * lookHeight;
            Quaternion desiredRotation = Quaternion.LookRotation(desiredLook - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-11f * Time.deltaTime));
        }

        private Vector3 FlatForward()
        {
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            return forward.normalized;
        }
    }
}
