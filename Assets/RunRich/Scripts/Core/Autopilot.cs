using UnityEngine;

namespace RunRich
{
    // Бот, который проходит уровень без человека: рулит к деньгам, ключам и дверям ворот,
    // уворачивается от бутылок. Нужен авто-смоук-тесту (игрок стартует с -autopilot)
    // и удобен для быстрого просмотра уровней в редакторе.
    [DisallowMultipleComponent]
    public sealed class Autopilot : MonoBehaviour
    {
        [SerializeField] private DragInput input;
        [SerializeField] private PlayerController player;

        [Header("Tuning")]
        [SerializeField] private float lookAhead = 16f;
        [SerializeField] private float bottleAvoidDistance = 9f;
        [SerializeField] private float steeringSharpness = 6f;
        [SerializeField] private float wanderAmount = 0.12f;

        private LevelDefinition _level;
        private bool _active;
        private float _wanderPhase;
        private float _lastChoiceHint = -1f;
        private float _choiceSide = 1f;

        public bool Active => _active;

        public void SetActive(bool value)
        {
            _active = value;
            if (input == null) return;

            if (value)
            {
                input.EnableProgrammaticInput();
                input.SetProgrammaticSteering(0f);
            }
            else
            {
                input.ClearProgrammaticSteering();
            }
        }

        public void SetLevel(LevelDefinition level)
        {
            _level = level;
            _lastChoiceHint = -1f;
        }

        private void Update()
        {
            if (!_active || input == null || player == null || _level == null) return;

            TrackPath track = player.Track;
            if (track == null) return;

            float distance = player.Distance;
            float limit = Mathf.Max(0.5f, track.RoadHalfWidth - GameConfig.PlayerCapsuleRadius);

            float desired = ChooseLateral(distance, limit);
            float steering = Mathf.Clamp(desired / limit, -1f, 1f);

            _wanderPhase += Time.deltaTime * 0.7f;
            steering += Mathf.Sin(_wanderPhase) * wanderAmount;

            float smoothing = 1f - Mathf.Exp(-steeringSharpness * Time.deltaTime);
            input.SetProgrammaticSteering(Mathf.Lerp(input.Steering, Mathf.Clamp(steering, -1f, 1f), smoothing));
        }

        // Выбирает боковое смещение, куда бот хочет встать прямо сейчас.
        private float ChooseLateral(float distance, float limit)
        {
            float here = player.Lateral;

            // 1) бутылки прямо перед бегуном отталкивают его в сторону
            float avoid = 0f;
            float avoidDistance = float.MaxValue;

            var pickups = _level.Pickups;
            for (int i = 0; i < pickups.Count; i++)
            {
                PickupItem item = pickups[i];
                if (item == null || item.Collected || item.Kind != PickupKind.Bottle) continue;

                float delta = item.Distance - distance;
                if (delta < 0f || delta > bottleAvoidDistance) continue;

                float offset = Mathf.Abs(item.Lateral - here);
                if (offset > GameConfig.PickupRadius * 1.4f) continue;

                float urgency = 1f - delta / bottleAvoidDistance;
                if (delta < avoidDistance)
                {
                    avoidDistance = delta;
                    float side = item.Lateral >= here ? -1f : 1f;
                    avoid = side * Mathf.Max(0.6f, urgency) * 1.8f;
                }
            }

            if (avoidDistance < float.MaxValue) return Mathf.Clamp(here + avoid, -limit, limit);

            // 2) самое ценное в окне просмотра притягивает бегуна
            float bestScore = float.MaxValue;
            float target = 0f;

            for (int i = 0; i < pickups.Count; i++)
            {
                PickupItem item = pickups[i];
                if (item == null || item.Collected || item.Kind == PickupKind.Bottle) continue;

                float delta = item.Distance - distance;
                if (delta < 0f || delta > lookAhead) continue;

                float score = delta + Mathf.Abs(item.Lateral - here) * 1.5f;
                if (score >= bestScore) continue;

                bestScore = score;
                target = item.Lateral;
            }

            // 3) двери следующих ворот выбора решают, куда бегуну встать
            GateZone choice = NextChoiceGate(distance);
            if (choice != null && choice.Distance - distance < lookAhead)
            {
                if (Mathf.Abs(choice.Distance - _lastChoiceHint) > 0.01f)
                {
                    _lastChoiceHint = choice.Distance;
                    _choiceSide = Random.value < 0.5f ? -1f : 1f;
                }

                target = _choiceSide * limit * 0.85f;
                bestScore = 0f;
            }

            if (bestScore == float.MaxValue) target = Mathf.Lerp(here, 0f, 0.5f);

            return Mathf.Clamp(target, -limit, limit);
        }

        private GateZone NextChoiceGate(float distance)
        {
            float best = float.MaxValue;
            GateZone result = null;

            var gates = _level.Gates;
            for (int i = 0; i < gates.Count; i++)
            {
                GateZone gate = gates[i];
                if (gate == null || gate.Consumed || gate.Kind != GateKind.Choice) continue;

                float delta = gate.Distance - distance;
                if (delta < 0f || delta >= best) continue;

                best = delta;
                result = gate;
            }

            return result;
        }
    }
}
