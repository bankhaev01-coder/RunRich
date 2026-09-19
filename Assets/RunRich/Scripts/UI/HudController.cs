using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RunRich
{
    // Экранный HUD забега: счётчик денег, шкала богатства с подписью стадии, ключи, титул уровня,
    // прогресс-бар с маркером бегуна, туториал «свайп для руления» и короткие тосты.
    // Всю иерархию строит редакторский сборщик уровней.
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameManager gameManager;

        [Header("Labels")]
        [SerializeField] private Text moneyText;
        [SerializeField] private Text keysText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text tierText;
        [SerializeField] private Text hintText;
        [SerializeField] private Text toastText;

        [Header("Bars")]
        [SerializeField] private Image tierGauge;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform progressMarker;
        [SerializeField] private GameObject tutorialRoot;

        [Header("Tuning")]
        [SerializeField] private float toastLifetime = 1.2f;
        [SerializeField] private float markerPadding = 26f;

        private Coroutine _toastRoutine;
        private float _progressBarWidth = 640f;

        private void Start()
        {
            if (gameManager == null) gameManager = GameManager.Instance;

            if (gameManager != null)
            {
                gameManager.MoneyChanged += OnMoneyChanged;
                gameManager.KeysChanged += OnKeysChanged;
            }

            if (progressFill != null && progressFill.rectTransform != null)
            {
                _progressBarWidth = progressFill.rectTransform.rect.width;
            }

            RefreshAll();
            ShowTutorial(true);
        }

        private void OnDestroy()
        {
            if (gameManager == null) return;
            gameManager.MoneyChanged -= OnMoneyChanged;
            gameManager.KeysChanged -= OnKeysChanged;
        }

        private void OnMoneyChanged(int money)
        {
            if (moneyText != null) moneyText.text = money + "$";
        }

        private void OnKeysChanged(int keys, int total)
        {
            if (keysText != null) keysText.text = keys + "/" + total;
        }

        // Переписывает все подписи из текущего состояния забега.
        public void RefreshAll()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null) return;

            if (moneyText != null) moneyText.text = gameManager.Money + "$";
            if (keysText != null) keysText.text = gameManager.Keys + "/" + Mathf.Max(1, gameManager.KeysTotal);

            LevelDefinition level = gameManager.Level;
            if (levelText != null)
            {
                levelText.text = level != null
                    ? string.Format(GameConfig.LevelLabelFormat, level.LevelNumber) + " · " + level.Title
                    : string.Empty;
            }

            TierInfo tier = WealthStages.Get(gameManager.TierIndex);
            if (tierText != null) tierText.text = tier.Label;
            if (tierGauge != null)
            {
                tierGauge.color = tier.GaugeColor;
                tierGauge.fillAmount = WealthStages.StageProgress(gameManager.Money);
            }
        }

        // Каждый кадр зовёт гейм-менеджер: прогресс-бар и подсказка «следующие ворота».
        public void Tick(GameManager manager, PlayerController player)
        {
            if (manager == null || player == null) return;

            float progress = manager.Progress01;

            if (progressFill != null) progressFill.fillAmount = progress;

            if (progressMarker != null)
            {
                Vector2 position = progressMarker.anchoredPosition;
                float half = Mathf.Max(0f, _progressBarWidth * 0.5f - markerPadding);
                position.x = Mathf.Lerp(-half, half, progress);
                progressMarker.anchoredPosition = position;
            }

            if (hintText == null) return;

            LevelDefinition level = manager.Level;
            GateZone gate = level != null ? level.NextGate(player.Distance) : null;
            if (gate == null || manager.State != GameState.Running)
            {
                hintText.text = string.Empty;
                return;
            }

            float remaining = Mathf.Max(0f, gate.Distance - player.Distance);
            string label = gate.Kind == GateKind.Finish
                ? "ФИНИШ"
                : gate.Kind == GateKind.Multiplier
                    ? "×" + gate.Multiplier
                    : gate.Label;

            hintText.text = label + " через " + Mathf.RoundToInt(remaining) + " м";
        }

        public void ShowTutorial(bool visible)
        {
            if (tutorialRoot != null) tutorialRoot.SetActive(visible);
        }

        // Короткое сообщение по центру, для бонусов ворот.
        public void Toast(string message)
        {
            if (toastText == null) return;

            toastText.text = message;
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            Color color = toastText.color;
            color.a = 1f;
            toastText.color = color;

            float life = 0f;
            while (life < toastLifetime)
            {
                life += Time.deltaTime;
                float fade = Mathf.Clamp01((toastLifetime - life) / 0.45f);
                color.a = fade;
                toastText.color = color;
                yield return null;
            }

            toastText.text = string.Empty;
            _toastRoutine = null;
        }
    }
}
