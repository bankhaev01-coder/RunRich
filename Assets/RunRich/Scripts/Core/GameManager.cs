using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ButchersGames;

namespace RunRich
{
    /// Состояние забега.
    public enum GameState
    {
        /// Уровень загружен, ждём первый свайп (виден туториал).
        Idle = 0,
        /// Бегун едет.
        Running = 1,
        /// Бегун прошёл финишную арку, играется результат.
        Finishing = 2,
        /// Показан экран победы.
        Won = 3,
        /// Показан экран поражения.
        Lost = 4
    }

    /// Логика уровня клона: грузит уровень через ButchersGames LevelManager, ведёт забег,
    /// разбирает подборы / ворота / финиш и отдаёт всё в HUD.
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        private const string TotalMoneyKey = "RunRich.TotalMoney";
        private const string AutopilotKey = "RunRich.Autopilot";

        public static GameManager Instance { get; private set; }

        [Header("Scene wiring")]
        [SerializeField] private ButchersGames.LevelManager levelManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private DragInput input;
        [SerializeField] private CameraFollow cameraRig;
        [SerializeField] private HudController hud;
        [SerializeField] private ResultScreenController resultScreen;
        [SerializeField] private FxManager fx;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private Autopilot autopilot;

        [Header("Tuning")]
        [SerializeField] private float finishCelebrationTime = 1.35f;
        [SerializeField] private float collectWindowBehind = 1.6f;

        public GameState State { get; private set; } = GameState.Idle;
        public LevelDefinition Level { get; private set; }
        public int Money { get; private set; }
        public int TotalMoney { get; private set; }
        public int Keys { get; private set; }
        public int KeysTotal { get; private set; }

        /// Прогресс уровня, 0..1.
        public float Progress01 => Level != null && Level.Length > 0f
            ? Mathf.Clamp01((player?.Distance ?? 0f) / Level.Length)
            : 0f;

        public int ProgressPercent => Mathf.RoundToInt(Progress01 * 100f);

        public int TierIndex { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> MoneyChanged;
        public event Action<int, int> TierChanged;
        public event Action<int, int> KeysChanged;
        public event Action<LevelDefinition> LevelLoaded;

        private float _lastDistance;
        private float _footstepTimer;
        private bool _footstepFlip;
        private bool _selfTest;

        private void Awake()
        {
            Instance = this;
            TotalMoney = PlayerPrefs.GetInt(TotalMoneyKey, 0);
        }

        private void OnEnable()
        {
            if (input != null) input.FirstInput += OnFirstInput;
        }

        private void OnDisable()
        {
            if (input != null) input.FirstInput -= OnFirstInput;
        }

        private void Start()
        {
            // Самотест из командной строки: Unity.exe ... -selftest (или --selftest).
            _selfTest = HasArgument("-selftest") || HasArgument("--selftest");
            StartCoroutine(LoadFirstLevel());
        }

        private static bool HasArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private IEnumerator LoadFirstLevel()
        {
            if (levelManager != null)
            {
                // Init() учитывает сохранение ButchersGames, а RestartLevel() гарантирует,
                // что загрузится именно выбранный в инспекторе уровень.
                levelManager.Init();
                levelManager.RestartLevel();
            }

            yield return null;
            BindLevel();

            // Автопилот включаем после BindLevel: иначе EnableProgrammaticInput() поднимет
            // FirstInput на состоянии Idle, и BindLevel() снова сбросит забег в Idle.
            bool autopilotRequested = PlayerPrefs.GetInt(AutopilotKey, 0) == 1
                || HasArgument("-autopilot") || _selfTest;
            if (autopilotRequested && autopilot != null) autopilot.SetActive(true);
        }

        /// Находит созданный уровень, разводит ссылки и сбрасывает забег.
        public void BindLevel()
        {
            Level = FindLevel();
            if (Level == null)
            {
                Debug.LogError("[RunRich] No LevelDefinition found in the scene.");
                return;
            }

            if (player != null)
            {
                player.Setup(Level.Track, input, null);
                player.ResetToStart(0f, 0f);
            }

            if (cameraRig != null && player != null) cameraRig.SetTarget(player.transform);

            if (autopilot != null) autopilot.SetLevel(Level);

            Level.ResetContent();

            Money = 0;
            Keys = 0;
            KeysTotal = GameConfig.KeysPerLevel;
            TierIndex = WealthStages.IndexFor(0);
            _lastDistance = 0f;
            _footstepTimer = 0f;

            SetState(GameState.Idle);
            hud?.ShowTutorial(true);
            hud?.RefreshAll();
            resultScreen?.Hide();
            LevelLoaded?.Invoke(Level);
            audioManager?.PlayLevelStart();
        }

        private LevelDefinition FindLevel()
        {
            if (levelManager != null)
            {
                LevelDefinition nested = levelManager.GetComponentInChildren<LevelDefinition>();
                if (nested != null) return nested;
            }
            return FindAnyObjectByType<LevelDefinition>();
        }

        private void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            Debug.Log("[RunRich] state=" + state + " money=" + Money + " tier=" + TierIndex);
            StateChanged?.Invoke(state);
        }

        private void OnFirstInput()
        {
            if (State != GameState.Idle) return;
            player?.BeginRun();
            hud?.ShowTutorial(false);
            SetState(GameState.Running);
        }

        private void Update()
        {
            if (player == null || Level == null) return;

            if (State == GameState.Running)
            {
                SweepTrack();
                UpdateFootsteps();
            }

            hud?.Tick(this, player);
        }

        // ------------------------------------------------------------------ столкновения
        private void SweepTrack()
        {
            float from = _lastDistance;
            float to = player.Distance;

            IReadOnlyList<PickupItem> pickups = Level.Pickups;
            for (int i = 0; i < pickups.Count; i++)
            {
                PickupItem item = pickups[i];
                if (item == null || item.Collected) continue;
                if (item.Distance > to + 0.6f || item.Distance < from - collectWindowBehind) continue;
                if (Mathf.Abs(item.Lateral - player.Lateral) > GameConfig.PickupRadius) continue;
                HandlePickup(item);
            }

            IReadOnlyList<GateZone> gates = Level.Gates;
            for (int i = 0; i < gates.Count; i++)
            {
                GateZone gate = gates[i];
                if (gate == null || gate.Consumed) continue;
                if (gate.Distance > to + 0.4f || gate.Distance < from - 0.6f) continue;
                if (gate.Kind != GateKind.Finish && !gate.CoversLateral(player.Lateral)) continue;
                HandleGate(gate);
            }

            _lastDistance = to;
        }

        private void HandlePickup(PickupItem item)
        {
            item.Collect();
            Vector3 position = item.transform.position;

            switch (item.Kind)
            {
                case PickupKind.Money:
                    AddMoney(item.Amount);
                    fx?.PlayPickup(position, item.Amount);
                    audioManager?.PlayCoin();
                    break;

                case PickupKind.Key:
                    Keys++;
                    AddMoney(GameConfig.KeyBonus);
                    fx?.PlayKeyPickup(position, GameConfig.KeyBonus);
                    audioManager?.PlayKey();
                    KeysChanged?.Invoke(Keys, KeysTotal);
                    break;

                case PickupKind.Bottle:
                    AddMoney(-GameConfig.BottlePenalty);
                    player?.ApplyBottlePenalty();
                    fx?.PlayBottleHit(position);
                    audioManager?.PlayRemoveMoney();
                    break;
            }
        }

        private void HandleGate(GateZone gate)
        {
            gate.MarkConsumed();
            Vector3 position = gate.transform.position;

            switch (gate.Kind)
            {
                case GateKind.Multiplier:
                    int multiplied = Mathf.Max(Money, 10) * Mathf.Max(1, gate.Multiplier);
                    AddMoney(multiplied - Money);
                    fx?.PlayMultiplier(position, gate.Multiplier);
                    audioManager?.PlayWin();
                    hud?.Toast("×" + gate.Multiplier);
                    break;

                case GateKind.Choice:
                    Level.ApplyTheme(gate.ThemeIndex);
                    player.ApplySpeedScale(1.06f);
                    fx?.PlayChoice(position);
                    audioManager?.PlayClick();
                    hud?.Toast(gate.Label);
                    break;

                case GateKind.Finish:
                    FinishRun();
                    break;
            }
        }

        // ------------------------------------------------------------------ деньги
        private void AddMoney(int delta)
        {
            Money = Mathf.Max(0, Money + delta);
            MoneyChanged?.Invoke(Money);

            int tier = WealthStages.IndexFor(Money);
            if (tier != TierIndex)
            {
                TierIndex = tier;
                TierChanged?.Invoke(TierIndex, Money);
                fx?.PlayTierUp(player != null ? player.transform.position : Vector3.zero, TierIndex);
                audioManager?.PlayTierUp();
            }
        }

        /// Добавляет собранные на уровне деньги в кошелёк игрока.
        public void ClaimMoney(int amount)
        {
            TotalMoney += Mathf.Max(0, amount);
            PlayerPrefs.SetInt(TotalMoneyKey, TotalMoney);
            PlayerPrefs.Save();
            hud?.RefreshAll();
        }

        // ------------------------------------------------------------------ финиш
        private void FinishRun()
        {
            if (State == GameState.Finishing || State == GameState.Won || State == GameState.Lost) return;

            SetState(GameState.Finishing);
            player?.StopRun();
            fx?.PlayVictory(player?.transform.position ?? Vector3.zero);
            audioManager?.PlayVictory();

            StartCoroutine(FinishSequence());
        }

        private IEnumerator FinishSequence()
        {
            yield return new WaitForSeconds(finishCelebrationTime);

            bool won = TierIndex >= Level.RequiredTier;
            Debug.Log("[RunRich] finish: won=" + won + " money=" + Money + " keys=" + Keys + "/" + KeysTotal +
                      " tier=" + TierIndex + " required=" + Level.RequiredTier);
            SetState(won ? GameState.Won : GameState.Lost);
            resultScreen?.ShowResult(won, Level, Money, Keys, KeysTotal);
            if (!won) audioManager?.PlayLose();

            if (!_selfTest) yield break;

            // Драйвер смоук-теста (игрок запущен с -selftest): снимаем кадр и выходим.
            yield return new WaitForSeconds(1.6f);
            string path = SelfTestScreenshotPath();
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[RunRich] self-test screenshot: " + path);
            // CaptureScreenshot пишет файл асинхронно, даём плееру время его дописать.
            float deadline = Time.time + 2f;
            while (Time.time < deadline) yield return null;

            if (System.IO.File.Exists(path))
                Debug.Log("[RunRich] self-test screenshot OK: " + path);
            else
                Debug.LogWarning("[RunRich] self-test screenshot NOT found: " + path);

            Application.Quit();
        }

        private static string SelfTestScreenshotPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-screenshot", StringComparison.OrdinalIgnoreCase))
                {
                    // следующий аргумент должен быть путём, а не очередным флагом
                    string candidate = args[i + 1];
                    if (!string.IsNullOrEmpty(candidate) && !candidate.StartsWith("-"))
                        return candidate;
                }
            }
            return System.IO.Path.Combine(Application.persistentDataPath, "selftest.png");
        }

        // ------------------------------------------------------------------ смена уровня
        public void LoadNextLevel()
        {
            if (levelManager == null) return;
            resultScreen?.Hide();
            levelManager.NextLevel();
            StartCoroutine(RebindAfterLevelChange());
        }

        public void RestartLevel()
        {
            if (levelManager == null) return;
            resultScreen?.Hide();
            levelManager.RestartLevel();
            StartCoroutine(RebindAfterLevelChange());
        }

        private IEnumerator RebindAfterLevelChange()
        {
            yield return null;
            BindLevel();
        }

        // ------------------------------------------------------------------ шаги
        private void UpdateFootsteps()
        {
            if (audioManager == null || player == null) return;

            float interval = Mathf.Lerp(0.36f, 0.22f, player.Speed01);
            _footstepTimer += Time.deltaTime;
            if (_footstepTimer < interval) return;

            _footstepTimer = 0f;
            _footstepFlip = !_footstepFlip;
            audioManager.PlayFootstep(_footstepFlip);
        }
    }
}
