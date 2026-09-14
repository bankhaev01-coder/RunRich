using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ButchersGames
{
    /// Управление набором уровней: выбор, перезапуск, переход к следующему,
    /// сохранение индекса последнего уровня в PlayerPrefs.
    public class LevelManager : MonoBehaviour
    {
        #region Singleton
        private static LevelManager _default;
        public static LevelManager Default => _default;
        private void Awake() => _default = this;
        #endregion

        // ---------------------------------------------------------------- ключи PlayerPrefs
        private const string CurrentLevelPrefsKey = "Current Level";
        private const string CompleteLevelCountPrefsKey = "Complete Lvl Count";
        private const string LastLevelIndexPrefsKey = "Last Level Index";
        private const string CurrentAttemptPrefsKey = "Current Attempt";

        /// Номер текущего уровня (нумерация с 1).
        public static int CurrentLevel
        {
            get
            {
                var dm = Default;
                if (dm == null)
                {
                    Debug.LogWarning("[LevelManager] Default не инициализирован - возвращаем 1.");
                    return 1;
                }

                var levels = dm.Levels;
                if (levels == null || levels.Count == 0)
                {
                    Debug.LogWarning("[LevelManager] Levels пуст - возвращаем 1.");
                    return 1;
                }

                return (CompleteLevelCount < levels.Count ? dm.CurrentLevelIndex : CompleteLevelCount) + 1;
            }
            set => PlayerPrefs.SetInt(CurrentLevelPrefsKey, value);
        }

        public static int CompleteLevelCount
        {
            get => PlayerPrefs.GetInt(CompleteLevelCountPrefsKey);
            set => PlayerPrefs.SetInt(CompleteLevelCountPrefsKey, value);
        }

        public static int LastLevelIndex
        {
            get => PlayerPrefs.GetInt(LastLevelIndexPrefsKey);
            set => PlayerPrefs.SetInt(LastLevelIndexPrefsKey, value);
        }

        public static int CurrentAttempt
        {
            get => PlayerPrefs.GetInt(CurrentAttemptPrefsKey);
            set => PlayerPrefs.SetInt(CurrentAttemptPrefsKey, value);
        }

        /// Индекс текущего уровня в списке (0-based).
        public int CurrentLevelIndex;

        [SerializeField] private bool editorMode = false;
        [SerializeField] private LevelsList levels;
        public List<Level> Levels => levels?.lvls;

        public event System.Action OnLevelStarted;

        private void Start()
        {
            Init();
        }

        private void OnDestroy()
        {
            LastLevelIndex = CurrentLevelIndex;
        }

        private void OnApplicationQuit()
        {
            LastLevelIndex = CurrentLevelIndex;
        }

        public void Init()
        {
            if (!editorMode)
                SelectLevel(LastLevelIndex, true);

            if (LastLevelIndex != CurrentLevel)
                CurrentAttempt = 0;
        }

        public void StartLevel()
        {
            OnLevelStarted?.Invoke();
        }

        public void RestartLevel()
        {
            SelectLevel(CurrentLevelIndex, false);
        }

        public void NextLevel()
        {
            if (!editorMode)
                CurrentLevel++;

            SelectLevel(CurrentLevelIndex + 1);
        }

        public void PrevLevel()
        {
            SelectLevel(CurrentLevelIndex - 1);
        }

        /// Выбирает уровень по индексу (с защитой границ и поддержкой рандомизации).
        public void SelectLevel(int levelIndex, bool indexCheck = true)
        {
            if (levels == null)
            {
                Debug.Log("<color=red>LevelsList не назначен!</color>");
                return;
            }

            if (indexCheck)
                levelIndex = GetCorrectedIndex(levelIndex);

            if (levelIndex < 0 || levelIndex >= levels.lvls.Count)
            {
                Debug.LogWarning($"Уровень с индексом {levelIndex} не найден (всего: {levels.lvls.Count})");
                return;
            }

            Level level = levels.lvls[levelIndex];

            if (level == null)
            {
                Debug.Log("<color=red>Нет префаба уровня!</color>");
                return;
            }

            SelLevelParams(level);
            CurrentLevelIndex = levelIndex;
        }

        private int GetCorrectedIndex(int levelIndex)
        {
            if (editorMode)
            {
                // В редакторе: если индекс вышел за границы - возврат к первому уровню.
                if (levelIndex > levels.lvls.Count - 1 || levelIndex < 0)
                    return 0;
                return levelIndex;
            }

            int levelId = CurrentLevel;
            if (levelId > levels.lvls.Count - 1)
            {
                if (levels.randomizedLvls)
                {
                    var available = Enumerable.Range(0, levels.lvls.Count)
                        .Where(i => i != CurrentLevelIndex)
                        .ToList();
                    if (available.Count == 0)
                        return levelIndex % levels.lvls.Count;
                    return available[UnityEngine.Random.Range(0, available.Count)];
                }
                return levelId % levels.lvls.Count;
            }

            return levelId;
        }

        /// Создаёт уровень как дочерний объект (префаб в редакторе, Instantiate в рантайме).
        private void SelLevelParams(Level level)
        {
            if (level == null) return;

            ClearChildren();

#if UNITY_EDITOR
            if (Application.isPlaying)
                Instantiate(level, transform);
            else
                PrefabUtility.InstantiatePrefab(level, transform);
#else
            Instantiate(level, transform);
#endif
        }

        /// Удаляет всех дочерних объектов (в игре через Destroy, в редакторе через DestroyImmediate).
        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    // В игре Destroy отложен до конца кадра: гасим объект сразу, чтобы
                    // на кадр не осталось двух активных экземпляров уровня.
                    child.SetActive(false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }
}