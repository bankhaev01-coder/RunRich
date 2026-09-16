using System.Collections.Generic;
using UnityEngine;

namespace RunRich
{
    // Перекрашивает бегуна при смене стадии богатства (референс меняет лоупольные
    // костюмы; тут персонаж рисуется палитровым атласом, поэтому клон тонирует материалы
    // палитры в цвета стадии). На экране победы надевается коктейльная
    // палитра «конца уровня».
    [DisallowMultipleComponent]
    public sealed class RunnerOutfit : MonoBehaviour
    {
        private enum Part
        {
            Top = 0,
            Bottom = 1,
            Shoes = 2,
            Hair = 3,
            Skin = 4,
            Other = 5
        }

        [SerializeField] private GameManager gameManager;
        [SerializeField] private Material finaleMaterial;
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.8f;

        private readonly List<Part> _slots = new List<Part>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Dictionary<Material, Material> _instances = new Dictionary<Material, Material>();

        private bool _bound;

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.LevelLoaded += OnLevelLoaded;
            gameManager.TierChanged += OnTierChanged;
            gameManager.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.LevelLoaded -= OnLevelLoaded;
            gameManager.TierChanged -= OnTierChanged;
            gameManager.StateChanged -= OnStateChanged;
        }

        private void OnDestroy()
        {
            foreach (Material instance in _instances.Values)
            {
                if (instance != null) Destroy(instance);
            }

            _instances.Clear();
        }

        private void OnLevelLoaded(LevelDefinition level) => Apply(WealthStages.Get(gameManager.TierIndex));

        private void OnTierChanged(int tierIndex, int money) => Apply(WealthStages.Get(tierIndex));

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Won) ApplyFinaleLook();
        }

        // ------------------------------------------------------------------ материалы
        // Собирает материалы персонажа один раз и меняет их на тонируемые копии.
        private void Bind()
        {
            if (_bound) return;
            _bound = true;

            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++) BindRenderer(skinned[i]);

            var staticMeshes = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < staticMeshes.Length; i++) BindRenderer(staticMeshes[i]);
        }

        private void BindRenderer(Renderer renderer)
        {
            Material[] shared = renderer.sharedMaterials;
            Material[] copies = new Material[shared.Length];

            for (int slot = 0; slot < shared.Length; slot++)
            {
                Material source = shared[slot];
                if (source == null)
                {
                    copies[slot] = null;
                    continue;
                }

                if (!_instances.TryGetValue(source, out Material instance))
                {
                    instance = new Material(source);
                    instance.name = source.name + " (outfit)";
                    _instances.Add(source, instance);
                }

                copies[slot] = instance;
                _slots.Add(Classify(source.name));
                _materials.Add(instance);
            }

            renderer.sharedMaterials = copies;
        }

        private static Part Classify(string materialName)
        {
            string name = materialName.ToLowerInvariant();

            if (name.Contains("hair")) return Part.Hair;
            if (name.Contains("skin") || name.Contains("face") || name.Contains("head")) return Part.Skin;
            if (name.Contains("shoe") || name.Contains("boot") || name.Contains("heel") || name.Contains("foot")) return Part.Shoes;
            if (name.Contains("pant") || name.Contains("jean") || name.Contains("short") || name.Contains("skirt") ||
                name.Contains("trouser")) return Part.Bottom;
            if (name.Contains("shirt") || name.Contains("coat") || name.Contains("dress") || name.Contains("cloth") ||
                name.Contains("top")) return Part.Top;

            return Part.Other;
        }

        private void Apply(TierInfo tier)
        {
            if (tier == null) return;
            Bind();

            bool anythingClassified = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != Part.Other)
                {
                    anythingClassified = true;
                    break;
                }
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                Material material = _materials[i];
                if (material == null) continue;

                Part part = anythingClassified ? _slots[i] : Part.Top;
                Color tinted = Color.Lerp(Color.white, ColorFor(part, tier), tintStrength);

                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tinted);
                if (material.HasProperty("_Color")) material.SetColor("_Color", tinted);
            }
        }

        private static Color ColorFor(Part part, TierInfo tier)
        {
            switch (part)
            {
                case Part.Top: return tier.TopColor;
                case Part.Bottom: return tier.BottomColor;
                case Part.Shoes: return tier.ShoesColor;
                case Part.Hair: return tier.HairColor;
                default: return Color.white;
            }
        }

        // Меняет палитру на коктейльный костюм «конца уровня» (экран победы).
        private void ApplyFinaleLook()
        {
            Bind();

            if (finaleMaterial == null) return;

            Texture palette = finaleMaterial.HasProperty("_BaseMap")
                ? finaleMaterial.GetTexture("_BaseMap")
                : finaleMaterial.mainTexture;

            if (palette == null) return;

            for (int i = 0; i < _materials.Count; i++)
            {
                Material material = _materials[i];
                if (material == null) continue;

                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", palette);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            }
        }
    }
}
