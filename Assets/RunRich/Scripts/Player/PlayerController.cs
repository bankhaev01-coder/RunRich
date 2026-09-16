using UnityEngine;

namespace RunRich
{
    // Бегун: сам бежит вперёд по дороге, вбок рулится свайпом.
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private DragInput input;
        [SerializeField] private RunnerAnimator animator;

        private TrackPath _track;
        private float _speedScale = 1f;
        private float _lateral;
        private float _lateralVelocity;
        private float _previousLateral;

        // Пройденная дистанция вдоль дороги (метры от старта).
        public float Distance { get; private set; }

        // Боковое смещение от центра дороги.
        public float Lateral => _lateral;

        // Текущая скорость вперёд, м/с.
        public float Speed { get; private set; }

        // Флаг, что забег идёт.
        public bool Running { get; private set; }

        // Скорость относительно базовой, 0..1.
        public float Speed01 => Mathf.Clamp01(Speed / GameConfig.BaseSpeed);

        // Крен корпуса при рулении (-1..1).
        public float Lean { get; private set; }

        public TrackPath Track => _track;

        public void Setup(TrackPath track, DragInput dragInput, RunnerAnimator runnerAnimator)
        {
            _track = track;
            if (dragInput != null) input = dragInput;
            if (runnerAnimator != null) animator = runnerAnimator;

            if (animator != null)
            {
                animator.CaptureRestPose();
                animator.RestPose();
            }
        }

        public void BeginRun()
        {
            Running = true;
            Speed = GameConfig.BaseSpeed * 0.35f;
        }

        public void StopRun()
        {
            Running = false;
            Speed = 0f;
        }

        // Ставит бегуна в начало дороги.
        public void ResetToStart(float distance = 0f, float lateral = 0f)
        {
            Distance = distance;
            _lateral = lateral;
            _previousLateral = lateral;
            Speed = 0f;
            _speedScale = 1f;
            Lean = 0f;

            if (input != null) input.ResetSteering();
            if (animator != null) animator.RestPose();

            ApplyPose(0f);
        }

        // Ненадолго замедляет бегуна (удар бутылкой).
        public void ApplyBottlePenalty()
        {
            _speedScale = Mathf.Max(0.6f, _speedScale * GameConfig.BadHitSpeedPenalty);
        }

        // На время умножает скорость вперёд (бонусы ворот выбора).
        public void ApplySpeedScale(float scale)
        {
            _speedScale = Mathf.Clamp(scale, 0.5f, 2f);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (Running)
            {
                float targetSpeed = GameConfig.BaseSpeed * _speedScale;
                Speed = Mathf.MoveTowards(Speed, targetSpeed, GameConfig.Acceleration * deltaTime);
                Distance += Speed * deltaTime;
                _speedScale = Mathf.MoveTowards(_speedScale, 1f, GameConfig.SpeedRecovery * deltaTime);
            }
            else
            {
                Speed = Mathf.MoveTowards(Speed, 0f, GameConfig.Acceleration * deltaTime);
            }

            UpdateSteering(deltaTime);
            ApplyPose(deltaTime);
        }

        private void UpdateSteering(float deltaTime)
        {
            float limit = Mathf.Max(0.5f, (_track != null ? _track.RoadHalfWidth : GameConfig.RoadHalfWidth) - GameConfig.PlayerCapsuleRadius);
            float steering = input != null ? Mathf.Clamp(input.Steering, -1f, 1f) : 0f;
            float target = steering * limit;
            float smoothing = 1f - Mathf.Exp(-GameConfig.SteerSmoothing * deltaTime);
            _previousLateral = _lateral;
            _lateral = Mathf.Lerp(_lateral, target, smoothing);

            float lateralSpeed = deltaTime > 0f ? (_lateral - _previousLateral) / deltaTime : 0f;
            float targetLean = Mathf.Clamp(-lateralSpeed * 0.35f, -10f, 10f);
            Lean = Mathf.Lerp(Lean, targetLean, 1f - Mathf.Exp(-8f * deltaTime));
        }

        private void ApplyPose(float deltaTime)
        {
            if (_track == null || _track.Spline == null) return;

            float distance = Mathf.Clamp(Distance, 0f, _track.Length);
            Vector3 position = _track.PointAt(distance, _lateral);
            transform.position = position;

            Quaternion baseRotation = _track.RotationAt(distance);
            Quaternion steeringRotation = Quaternion.Euler(0f, Lean * 0.45f, Lean * 0.5f);
            transform.rotation = baseRotation * steeringRotation;

            if (animator != null) animator.Tick(Speed01, deltaTime, Running);
        }
    }
}
