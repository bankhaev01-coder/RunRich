using UnityEngine;

namespace RunRich
{
    // Тип чекпоинта, стоящего на дороге.
    public enum GateKind
    {
        // Две двери рядом: игрок выбирает тему следующего куска.
        Choice = 0,
        // Дверь со знаком xN: умножает собранные на уровне деньги.
        Multiplier = 1,
        // Финальная арка «1 %», закрывающая уровень.
        Finish = 2
    }

    // Дверь / чекпоинт, срабатывающий, когда бегун проходит сквозь.
    [DisallowMultipleComponent]
    public sealed class GateZone : MonoBehaviour
    {
        [SerializeField] private GateKind kind = GateKind.Multiplier;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private int multiplier = 2;
        [SerializeField] private int themeIndex;
        [SerializeField] private float distance;
        [SerializeField] private float lateralCenter;
        [SerializeField] private float halfWidth = 1.9f;

        public GateKind Kind => kind;
        public string Label => label;
        public int Multiplier => multiplier;
        public int ThemeIndex => themeIndex;
        public float Distance => distance;
        public float LateralCenter => lateralCenter;
        public float HalfWidth => halfWidth;
        public bool Consumed { get; private set; }

        public void Configure(GateKind gateKind, string gateLabel, int gateMultiplier, int gateTheme)
        {
            kind = gateKind;
            label = gateLabel;
            multiplier = gateMultiplier;
            themeIndex = gateTheme;
        }

        // Вызывает сборщик уровней, чтобы запомнить позицию на треке.
        public void SetTrackPosition(float distanceOnTrack, float lateral, float gateHalfWidth = 1.9f)
        {
            distance = distanceOnTrack;
            lateralCenter = lateral;
            halfWidth = gateHalfWidth;
        }

        public bool CoversLateral(float lateral) => Mathf.Abs(lateral - lateralCenter) <= halfWidth;

        public void MarkConsumed() => Consumed = true;

        public void ResetGate() => Consumed = false;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == GateKind.Finish ? Color.green : kind == GateKind.Choice ? Color.magenta : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, new Vector3(halfWidth * 2f, 4f, 0.4f));
        }
#endif
    }
}
