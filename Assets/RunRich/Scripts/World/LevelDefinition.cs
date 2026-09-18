using System.Collections.Generic;
using UnityEngine;

namespace RunRich
{
    // Корень префаба уровня: держит дорогу, предметы, ворота и метаданные
    // (номер уровня, порог победы, тема). Уровни печёт редакторский LevelBuilder.
    [DisallowMultipleComponent]
    public sealed class LevelDefinition : MonoBehaviour
    {
        [Header("Meta")]
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string title = "Пирс";
        [SerializeField] private int requiredTier = GameConfig.RequiredTierToWin;

        [Header("References")]
        [SerializeField] private TrackPath track;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform playerRoot;

        [Header("Content")]
        [SerializeField] private List<PickupItem> pickups = new List<PickupItem>();
        [SerializeField] private List<GateZone> gates = new List<GateZone>();
        [SerializeField] private List<ThemeGroup> themes = new List<ThemeGroup>();

        public int LevelNumber => levelNumber;
        public string Title => title;
        public int RequiredTier => requiredTier;
        public TrackPath Track => track;
        public Transform StartPoint => startPoint;
        public Transform PlayerRoot => playerRoot;
        public IReadOnlyList<PickupItem> Pickups => pickups;
        public IReadOnlyList<GateZone> Gates => gates;
        public float Length => track != null ? track.Length : 0f;

        public int KeysTotal
        {
            get
            {
                int count = 0;
                for (int i = 0; i < pickups.Count; i++)
                {
                    if (pickups[i].Kind == PickupKind.Key)
                        count++;
                }
                return count;
            }
        }

        // Хелпер настройки в редакторе.
        public void EditorConfigure(int number, string levelTitle, int tier, TrackPath trackPath,
            Transform start, Transform player, List<PickupItem> levelPickups, List<GateZone> levelGates)
        {
            levelNumber = number;
            title = levelTitle;
            requiredTier = tier;
            track = trackPath;
            startPoint = start;
            playerRoot = player;
            pickups = levelPickups;
            gates = levelGates;
        }

        // Возвращает все подборы и ворота в стартовое состояние.
        public void ResetContent()
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                if (pickups[i] != null) pickups[i].Respawn();
            }

            for (int i = 0; i < gates.Count; i++)
            {
                if (gates[i] != null) gates[i].ResetGate();
            }

            ApplyTheme(0);
        }

        // Включает набор декораций выбранных ворот («ВЕЧЕРИНКА» / «ШКОЛА» / ...).
        public void ApplyTheme(int themeIndex)
        {
            for (int i = 0; i < themes.Count; i++)
            {
                ThemeGroup group = themes[i];
                if (group?.Root == null) continue;
                group.Root.SetActive(group.Index == themeIndex);
            }
        }

        // Собирает все списки контента (нужно сборщику уровней).
        public void EditorSetContent(List<PickupItem> levelPickups, List<GateZone> levelGates, List<ThemeGroup> themeGroups)
        {
            pickups = levelPickups;
            gates = levelGates;
            themes = themeGroups;
        }

        // Набор декораций, появляющийся после ворот выбора.
        [System.Serializable]
        public sealed class ThemeGroup
        {
            [SerializeField] private int index;
            [SerializeField] private GameObject root;

            public int Index => index;
            public GameObject Root => root;

            public ThemeGroup(int groupIndex, GameObject groupRoot)
            {
                index = groupIndex;
                root = groupRoot;
            }
        }

        // Ворота, ближайшие к заданной дистанции (для подсказок HUD).
        public GateZone NextGate(float distance)
        {
            GateZone result = null;
            float best = float.MaxValue;
            for (int i = 0; i < gates.Count; i++)
            {
                GateZone gate = gates[i];
                if (gate == null || gate.Consumed) continue;
                float delta = gate.Distance - distance;
                if (delta < 0f || delta >= best) continue;
                best = delta;
                result = gate;
            }
            return result;
        }
    }
}
