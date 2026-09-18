using UnityEngine;

namespace RunRich
{
    // Что бегун может встретить на дороге.
    public enum PickupKind
    {
        // Стопка купюр - даёт деньги.
        Money = 0,
        // Бутылка вина - отнимает деньги и замедляет бегуна.
        Bottle = 1,
        // Золотой ключ - редкий бонусный подбор.
        Key = 2
    }

    // Один предмет на дороге. Подбирается по близости, а не физикой.
    [DisallowMultipleComponent]
    public sealed class PickupItem : MonoBehaviour
    {
        [SerializeField] private PickupKind kind = PickupKind.Money;
        [SerializeField] private int amount = GameConfig.MoneyPickupValue;
        [SerializeField] private float spinSpeed = 40f;
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float distance;
        [SerializeField] private float lateral;

        // Дистанция вдоль трека, где стоит предмет (сериализовано ради сохранённых префабов).
        public float Distance => distance;

        // Боковое смещение от центра дороги (сериализовано ради сохранённых префабов).
        public float Lateral => lateral;

        public PickupKind Kind => kind;
        public int Amount => amount;
        public bool Collected { get; private set; }

        private Vector3 _basePosition;
        private float _phase;

        private void Awake()
        {
            // префаб хранит поставленный трансформ, а рантайм-покачиванию нужна базовая точка
            if (_basePosition.sqrMagnitude <= 0.0001f) _basePosition = transform.position;
        }

        public void Configure(PickupKind newKind, int newAmount)
        {
            kind = newKind;
            amount = newAmount;
        }

        // Ставит предмет на дорогу и запоминает, где ему место.
        public void Place(TrackPath track, float distance, float lateral)
        {
            this.distance = distance;
            this.lateral = lateral;
            Collected = false;
            _phase = (distance * 0.37f) % (Mathf.PI * 2f);

            if (track != null && track.Spline != null)
            {
                transform.position = track.PointAt(distance, lateral);
                Quaternion rotation = track.RotationAt(distance);
                transform.rotation = kind == PickupKind.Bottle
                    ? rotation
                    : rotation * Quaternion.Euler(0f, distance * 24f, 0f);
                _basePosition = transform.position;
            }
            else
            {
                _basePosition = transform.position;
            }
        }

        private void Update()
        {
            if (Collected) return;

            _phase += Time.deltaTime * bobSpeed;
            float bob = Mathf.Sin(_phase) * bobHeight;

            if (kind == PickupKind.Money || kind == PickupKind.Key)
            {
                transform.position = _basePosition + Vector3.up * (0.15f + bob);
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            }
            else
            {
                transform.position = _basePosition + Vector3.up * bob * 0.35f;
            }
        }

        // Помечает предмет взятым; объект прячется, но остаётся ради рестартов.
        public void Collect()
        {
            Collected = true;
            gameObject.SetActive(false);
        }

        // Возвращает взятый предмет на дорогу (рестарт уровня).
        public void Respawn()
        {
            Collected = false;
            gameObject.SetActive(true);
            if (_basePosition.sqrMagnitude > 0f) transform.position = _basePosition;
        }
    }
}
