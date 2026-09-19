using UnityEngine;
using UnityEngine.UI;

namespace RunRich
{
    // Экран победы / поражения. На победе игрок забирает заработанные на уровне деньги и идёт дальше;
    // на поражении уровень можно переиграть. Собирает редакторский сборщик уровней.
    [DisallowMultipleComponent]
    public sealed class ResultScreenController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameObject root;
        [SerializeField] private Image backdrop;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text moneyText;
        [SerializeField] private Text keysText;
        [SerializeField] private Button takeButton;
        [SerializeField] private Text takeLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Text retryLabel;

        [Header("Colours")]
        [SerializeField] private Color winColor = new Color(0.98f, 0.82f, 0.2f);
        [SerializeField] private Color loseColor = new Color(0.95f, 0.3f, 0.26f);

        private int _pendingMoney;
        private bool _bound;

        private void Awake()
        {
            if (takeLabel != null) takeLabel.text = GameConfig.TakeLabel;
            if (retryLabel != null) retryLabel.text = GameConfig.RetryLabel;
            Hide();
        }

        // Заполняет экран итогом забега.
        public void ShowResult(bool won, LevelDefinition level, int money, int keys, int keysTotal)
        {
            Bind();

            _pendingMoney = money;

            if (root != null) root.SetActive(true);
            if (backdrop != null) backdrop.color = won ? new Color(0.05f, 0.28f, 0.12f, 0.72f) : new Color(0.22f, 0.05f, 0.05f, 0.72f);

            if (titleText != null)
            {
                titleText.text = won ? GameConfig.WinLabel : GameConfig.LoseLabel;
                titleText.color = won ? winColor : loseColor;
            }

            if (subtitleText != null)
            {
                subtitleText.text = string.Format(GameConfig.LevelLabelFormat, level != null ? level.LevelNumber : 1) +
                                    (won ? " · " + GameConfig.CompletedLabel : string.Empty);
            }

            if (moneyText != null) moneyText.text = "+" + money + "$";
            if (keysText != null) keysText.text = "КЛЮЧИ " + keys + "/" + Mathf.Max(1, keysTotal);

            if (takeButton != null) takeButton.gameObject.SetActive(won);
            if (retryButton != null) retryButton.gameObject.SetActive(!won);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void Bind()
        {
            if (_bound) return;
            _bound = true;

            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null) return;

            if (takeButton != null) takeButton.onClick.AddListener(OnTake);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        }

        private void OnTake()
        {
            if (gameManager == null) return;
            gameManager.ClaimMoney(_pendingMoney);
            Hide();
            gameManager.LoadNextLevel();
        }

        private void OnRetry()
        {
            if (gameManager == null) return;
            Hide();
            gameManager.RestartLevel();
        }
    }
}
