using UnityEngine;

namespace RunRich
{
    /// Единая точка настройки клона механики "Run Rich" (мобильный раннер):
    /// здесь собраны все числа, задающие ощущение игры.
    public static class GameConfig
    {
        // ---------------------------------------------------------------- движение
        /// Скорость бегуна по дороге, м/с.
        public const float BaseSpeed = 11f;
        /// Разгон за секунду в начале забега.
        public const float Acceleration = 6f;
        /// Множитель скорости после попадания в «плохую» штуку (бутылка).
        public const float BadHitSpeedPenalty = 0.86f;
        /// Как быстро штраф скорости спадает.
        public const float SpeedRecovery = 1.6f;

        // ---------------------------------------------------------------- трасса
        /// Половина ширины дороги, где игрок может двигаться.
        public const float RoadHalfWidth = 3.66f;
        /// Доля ширины экрана, за которую руль проходит полный ход от -1 до +1.
        public const float SteerScreenSpan = 0.55f;
        /// Оборотность сглаживания поворота (экспоненциальный подход).
        public const float SteerSmoothing = 14f;

        // ---------------------------------------------------------------- камера
        public static readonly Vector3 CameraOffset = new Vector3(0f, 3.35f, -5.6f);
        public const float CameraLookAhead = 2.6f;
        public const float CameraFollowSharpness = 7.5f;
        public const float CameraFov = 55f;

        // ---------------------------------------------------------------- тело игрока
        public const float PlayerCapsuleRadius = 0.42f;
        /// Поперечное расстояние, в котором подбирается предмет (мировые единицы).
        public const float PickupRadius = 1.05f;

        // ---------------------------------------------------------------- прогресс
        /// Деньги за один прицеп с купюрами.
        public const int MoneyPickupValue = 5;
        /// Деньги, сгорающие при ударе об алкоголь.
        public const int BottlePenalty = 15;
        /// Бонус за ключ.
        public const int KeyBonus = 40;
        /// Ключей на уровне.
        public const int KeysPerLevel = 3;

        // ---------------------------------------------------------------- богатство
        /// Индекс стадии, нужный для победы (БОГАТЫЙ).
        public const int RequiredTierToWin = 2;

        // ---------------------------------------------------------------- карта / генерация
        /// Количество контрольных точек на уровень при генерации.
        public const int LevelControlPoints = 6;
        /// Базовый размах «качания» при генерации точек.
        public const float LevelGenerationSwingBase = 5.5f;
        /// Прирост размаха на уровень.
        public const float LevelGenerationSwingPerLevel = 1.4f;
        /// Частота синусоиды при генерации точек.
        public const float LevelGenerationSwingFreq = 1.9f;
        /// Фазовый сдвиг на уровень при генерации точек.
        public const float LevelGenerationSwingPhaseShift = 0.7f;
        /// Длина первого и последнего прямого отрезка трассы, м (разгон и подход к финишу).
        public const float FirstTrackSegmentLength = 45f;
        /// Шаг между средними контрольными точками трассы, м.
        public const float TrackSegmentStep = 55f;

        // ---------------------------------------------------------------- модель и уровни
        /// Ориентация модели перед размещением на дороге.
        public const float ModelYaw = 180f;
        /// Количество уровней по умолчанию.
        public const int DefaultLevelCount = 4;

        // ---------------------------------------------------------------- тексты интерфейса
        public const string LevelLabelFormat = "Уровень {0}";
        public const string CompletedLabel = "ЗАВЕРШЕНО";
        public const string WinLabel = "ВЫ ПОБЕДИЛИ!";
        public const string LoseLabel = "ВЫ НЕ СМОГЛИ!";
        public const string TutorialLabel = "ПРОВЕДИТЕ ПО ЭКРАНУ, ЧТОБЫ ПОВЕРНУТЬ";
        public const string RetryLabel = "ПОВТОРИТЬ";
        public const string TakeLabel = "ПОЛУЧИТЬ";
    }
}
