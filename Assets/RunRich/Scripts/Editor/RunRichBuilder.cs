using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ButchersGames;
using RunRich;

namespace RunRich.EditorTools
{
    // Сборщик контента в один клик: чинит привезённый арт (у референсного пака не было
    // .meta-файлов, поэтому битые материалы / спрайты / шейдеры выкидываем и пересоздаём
    // материалы из исходных текстур), собирает префабы, уровни, HUD и в конце сцену Main.
    // Запуск из меню «RunRich» или в батч-режиме:
    // Unity.exe -batchmode -quit -projectPath <path> -executeMethod RunRich.EditorTools.RunRichBuilder.BuildAll
    public static class RunRichBuilder
    {
        private const string Root = "Assets/RunRich";
        private const string ArtVisual = Root + "/Art/Visual";
        private const string ArtSounds = Root + "/Art/Sounds/AudioClip";
        private const string MaterialsRoot = Root + "/Art/Materials";
        private const string PrefabsRoot = Root + "/Prefabs";
        private const string DataRoot = Root + "/Data";
        private const string ScenesRoot = Root + "/Scenes";
        private const string ScenePath = ScenesRoot + "/Main.unity";

        private const float ModelYaw = GameConfig.ModelYaw;
        private const int LevelCount = GameConfig.DefaultLevelCount;

        private static readonly string[] LevelTitles = { "Пирс", "Вечеринка", "Пираты", "Богатый квартал" };
        private static readonly string[] ThemeNames = { "ВЕЧЕРИНКА", "ПИРАТЫ", "БОГАЧИ" };

        // ------------------------------------------------------------------ точка входа
        public static void BuildAll()
        {
            EnsureFolders();
            PruneBrokenAssets();
            PrepareModelAtlases();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureImporters();

            BuildMaterials();
            BuildEffectPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildLevelPrefabs();
            BuildMainScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RunRich] build finished");
        }

        private static void EnsureFolders()
        {
            string[] folders = { MaterialsRoot, PrefabsRoot, DataRoot, ScenesRoot };
            for (int i = 0; i < folders.Length; i++)
            {
                if (!AssetDatabase.IsValidFolder(folders[i]))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(folders[i]).Replace('\\', '/'), Path.GetFileName(folders[i]));
                }
            }
        }

        // Чистит ассеты, которые держались на .meta-файлах исходного проекта (спрайты,
        // материалы Toony Colors, кастомные шейдеры и SDF-шрифт) - тут их ссылки не резолвятся
        // и они только мусорят в консоль.
        private static void PruneBrokenAssets()
        {
            string[] folders = { ArtVisual + "/Sprite", ArtVisual + "/Material", ArtVisual + "/Shader", ArtVisual + "/Fonts" };

            for (int i = 0; i < folders.Length; i++)
            {
                string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folders[i] });
                for (int g = 0; g < guids.Length; g++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    if (path.EndsWith(".asset") || path.EndsWith(".mat") || path.EndsWith(".shader"))
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
            }

            string[] atlases = AssetDatabase.FindAssets("t:SpriteAtlas");
            for (int i = 0; i < atlases.Length; i++) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(atlases[i]));

            string[] looseMaterials = AssetDatabase.FindAssets("t:Material", new[] { ArtVisual + "/Texture2D" });
            for (int i = 0; i < looseMaterials.Length; i++) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(looseMaterials[i]));

            AssetDatabase.Refresh();
        }

        // Модели FBX ссылаются на <модель>.fbm/atlas.png; пересоздаём эти папки, чтобы Unity
        // сам подцепил палитровую текстуру в импортированный материал.
        private static void PrepareModelAtlases()
        {
            string lowPoly = ArtVisual + "/Mesh/LowPoly";
            string[] models = { "player", "photographer", "bills", "dollar", "bottle", "Door_Million" };
            string atlas = FindTexturePath("atlas.png");
            string atlasEnd = FindTexturePath("atlas_end.png");
            string doorTexture = FindTexturePath("door_texture.png");

            for (int i = 0; i < models.Length; i++)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), lowPoly, models[i] + ".fbm");
                Directory.CreateDirectory(folder);

                if (!string.IsNullOrEmpty(atlas)) File.Copy(Absolute(atlas), Path.Combine(folder, "atlas.png"), true);
                if (models[i] == "player" && !string.IsNullOrEmpty(atlasEnd))
                {
                    File.Copy(Absolute(atlasEnd), Path.Combine(folder, "atlas_end.png"), true);
                }

                if (models[i] == "Door_Million" && !string.IsNullOrEmpty(doorTexture))
                {
                    File.Copy(Absolute(doorTexture), Path.Combine(folder, "door_texture.png"), true);
                }
            }
        }

        private static string Absolute(string assetPath) => Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        private static string FindTexturePath(string fileName)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtVisual + "/Texture2D" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileName(path).Equals(fileName, System.StringComparison.OrdinalIgnoreCase)) return path;
            }

            return null;
        }

        // Настройки импорта, нужные клону: чёткие палитровые текстуры и UI-арт как спрайты.
        private static void EnsureImporters()
        {
            string[] paletteNames = { "atlas.png", "atlas_end.png", "atlas_bad.png", "atlas_0.png", "atlas_Pirate.png", "door_texture.png" };
            for (int i = 0; i < paletteNames.Length; i++) SetPaletteImporter(FindTexturePath(paletteNames[i]));

            string[] lowPolyAtlases = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtVisual + "/Mesh/LowPoly" });
            for (int i = 0; i < lowPolyAtlases.Length; i++) SetPaletteImporter(AssetDatabase.GUIDToAssetPath(lowPolyAtlases[i]));

            string[] spriteNames =
            {
                "50_coins.png", "1000_coins.png", "gold_bills.png", "key.png", "Circle.png", "circle_gauge.png",
                "finish.png", "hand.png", "arrow_left_right.png", "youwin.png", "restart.png", "window.png",
                "sparkle.png", "red_carpet.png", "Star.png", "tick.png", "button.png"
            };
            for (int i = 0; i < spriteNames.Length; i++) SetSpriteImporter(FindTexturePath(spriteNames[i]));

            SetRepeatingImporter(FindTexturePath("sky_blue.png"), 2048);
            SetRepeatingImporter(FindTexturePath("Water.png"), 1024);
            SetRepeatingImporter(FindTexturePath("water_brighter.png"), 1024);

            string[] models = { "player", "photographer", "bills", "dollar", "bottle", "Door_Million" };
            for (int i = 0; i < models.Length; i++) SetModelImporter(ArtVisual + "/Mesh/LowPoly/" + models[i] + ".fbx");
        }

        private static void SetPaletteImporter(string path)
        {
            TextureImporter importer = TextureImporterAt(path);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void SetSpriteImporter(string path)
        {
            TextureImporter importer = TextureImporterAt(path);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        private static void SetRepeatingImporter(string path, int maxSize)
        {
            TextureImporter importer = TextureImporterAt(path);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        private static void SetModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        private static TextureImporter TextureImporterAt(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return AssetImporter.GetAtPath(path) as TextureImporter;
        }

        // ------------------------------------------------------------------ хелперы для ассетов
        private static Texture2D LoadTexture(string fileName)
        {
            string path = FindTexturePath(fileName);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = FindTexturePath(fileName);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static AudioClip LoadClip(string fileName)
        {
            string path = ArtSounds + "/" + fileName;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning("[RunRich] audio clip not found: " + path);
            return clip;
        }

        private static GameObject LoadModel(string modelName) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(ArtVisual + "/Mesh/LowPoly/" + modelName + ".fbx");

        private static Mesh LoadMesh(string meshName) =>
            AssetDatabase.LoadAssetAtPath<Mesh>(ArtVisual + "/Mesh/" + meshName + ".asset");

        // ------------------------------------------------------------------ материалы
        private static void BuildMaterials()
        {
            CreateLit("M_Atlas", LoadTexture("atlas.png"), Color.white, 0.15f, 0f);
            CreateLit("M_Finale", LoadTexture("atlas_end.png"), Color.white, 0.15f, 0f);
            CreateLit("M_Door", LoadTexture("door_texture.png") ?? LoadTexture("atlas.png"), Color.white, 0.2f, 0f);
            CreateLit("M_Road", null, new Color(0.79f, 0.80f, 0.83f), 0.12f, 0f);
            CreateLit("M_RoadEdge", null, new Color(0.97f, 0.97f, 0.95f), 0.30f, 0f);
            CreateLit("M_Gold", null, new Color(1f, 0.79f, 0.22f), 0.85f, 1f, new Color(0.55f, 0.38f, 0.05f));
            CreateLit("M_Carpet", LoadTexture("red_carpet.png"), Color.white, 0.25f, 0f);
            CreateLit("M_Sign", null, new Color(0.13f, 0.16f, 0.24f), 0.45f, 0f);

            SetTransparent(CreateLit("M_Water", LoadTexture("Water.png"), new Color(0.33f, 0.72f, 0.92f, 0.82f), 0.92f, 0.05f));
            SetTransparent(CreateUnlit("M_Spark", LoadTexture("sparkle.png"), Color.white));
            SetTransparent(CreateUnlit("M_SparkGold", LoadTexture("sparkle.png"), new Color(1f, 0.85f, 0.30f)));
            SetTransparent(CreateUnlit("M_SparkRed", LoadTexture("sparkle.png"), new Color(1f, 0.35f, 0.30f)));
            CreateSky();
        }

        private static void CreateSky()
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogWarning("[RunRich] Skybox/Panoramic shader not found");
                return;
            }

            var material = new Material(shader) { name = "M_Sky" };
            material.SetTexture("_MainTex", LoadTexture("sky_blue.png"));
            material.SetFloat("_Mapping", 1f);    // latitude / longitude
            material.SetFloat("_ImageType", 1f);  // 2D texture
            material.SetFloat("_Exposure", 1f);
            SaveMaterial(material, "M_Sky");
        }

        private static Material CreateLit(string name, Texture2D texture, Color color, float smoothness, float metallic, Color? emission = null)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateUnlit(string name, Texture2D texture, Color color)
        {
            Material material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Unlit");
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            Material existing = LoadMaterial(name);
            if (existing != null) return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[RunRich] shader not found: " + shaderName);
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, MaterialsRoot + "/" + name + ".mat");
            return material;
        }

        private static void SaveMaterial(Material material, string name)
        {
            string path = MaterialsRoot + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
        }

        private static void SetTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
        }

        // ------------------------------------------------------------------ префабы уровней
        // Строит префабы уровней и ассет LevelsList, который ест LevelManager.
        private static void BuildLevelPrefabs()
        {
            for (int i = 0; i < LevelCount; i++) BuildLevelPrefab(i);

            string listPath = DataRoot + "/LevelsList.asset";
            AssetDatabase.DeleteAsset(listPath);

            LevelsList list = ScriptableObject.CreateInstance<LevelsList>();
            list.randomizedLvls = false;
            list.lvls = new List<Level>(LevelCount);
            for (int i = 0; i < LevelCount; i++)
            {
                GameObject prefab = LoadPrefab("Level_" + (i + 1));
                Level level = prefab != null ? prefab.GetComponent<Level>() : null;
                if (level != null) list.lvls.Add(level);
                else Debug.LogError("[RunRich] level prefab missing: Level_" + (i + 1));
            }
            AssetDatabase.CreateAsset(list, listPath);
        }

        private static void BuildLevelPrefab(int index)
        {
            var root = new GameObject("Level_" + (index + 1));
            Level levelTag = root.AddComponent<Level>();

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(root.transform, false);
            TrackPath track = trackGo.AddComponent<TrackPath>();
            track.SetControlPoints(MakeControlPoints(index));
            track.SetMaterials(LoadMaterial("M_Road"), LoadMaterial("M_RoadEdge"), LoadMaterial("M_Water"));
            track.Build();

            var items = new GameObject("Items");
            items.transform.SetParent(root.transform, false);
            var pickups = new List<PickupItem>();
            PlacePickups(track, items.transform, pickups);

            var gates = new List<GateZone>();
            PlaceGates(index, track, root.transform, gates);

            var themes = new List<LevelDefinition.ThemeGroup>();
            BuildThemeGroups(index, track, root.transform, themes);

            Transform start = new GameObject("StartPoint").transform;
            start.SetParent(root.transform, false);
            start.position = track.PointAt(0f, 0f);
            start.rotation = track.RotationAt(0f);

            Transform playerRoot = new GameObject("PlayerRoot").transform;
            playerRoot.SetParent(root.transform, false);

            // Меши дороги / моря пересоздаёт при старте TrackPath.Awake.
            track.ClearGeneratedMeshes();

            LevelDefinition definition = root.AddComponent<LevelDefinition>();
            definition.EditorConfigure(index + 1, LevelTitles[index], GameConfig.RequiredTierToWin,
                track, start, playerRoot, pickups, gates);
            definition.EditorSetContent(pickups, gates, themes);
            SetRef(levelTag, "playerSpawnPoint", start);

            SavePrefab(root, PrefabsRoot + "/Level_" + (index + 1) + ".prefab");
        }

        // Мягкая S-образная дорога; поздние уровни длиннее и сильнее виляют.
        private static Vector3[] MakeControlPoints(int levelIndex)
        {
            var points = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, GameConfig.FirstTrackSegmentLength) };
            float z = GameConfig.FirstTrackSegmentLength;
            int segments = GameConfig.LevelControlPoints - 1 + levelIndex * 2;
            float swing = GameConfig.LevelGenerationSwingBase + levelIndex * GameConfig.LevelGenerationSwingPerLevel;

            for (int i = 0; i < segments; i++)
            {
                z += GameConfig.TrackSegmentStep;
                points.Add(new Vector3(Mathf.Sin(i * GameConfig.LevelGenerationSwingFreq + levelIndex * GameConfig.LevelGenerationSwingPhaseShift) * swing, 0f, z));
            }

            z += GameConfig.FirstTrackSegmentLength;
            points.Add(new Vector3(0f, 0f, z));
            return points.ToArray();
        }

        // ------------------------------------------------------------------ содержимое трассы
        /// Ряды денег с редкими стенами из бутылок и тремя золотыми ключами.
        private static void PlacePickups(TrackPath track, Transform parent, List<PickupItem> pickups)
        {
            GameObject moneyPrefab = LoadPrefab("Pickup_Money");
            GameObject bottlePrefab = LoadPrefab("Pickup_Bottle");
            GameObject keyPrefab = LoadPrefab("Pickup_Key");

            if (moneyPrefab == null || bottlePrefab == null || keyPrefab == null)
            {
                Debug.LogError("[RunRich] pickup prefabs missing, level content skipped");
                return;
            }

            float length = track.Length;
            float limit = Mathf.Max(1f, track.RoadHalfWidth - 1.15f);
            const float rowStep = 7.5f;

            int rowCount = Mathf.Max(4, Mathf.FloorToInt((length - 16f) / rowStep));
            for (int r = 0; r < rowCount; r++)
            {
                float distance = 8f + r * rowStep;
                float centre = Mathf.Sin(r * 0.8f) * limit * 0.55f;

                if (r % 4 == 3)
                {
                    // бутылка сторожит одну сторону, купюры награждают за объезд по другой
                    float side = (r / 4) % 2 == 0 ? 1f : -1f;
                    SpawnPickup(bottlePrefab, parent, track, distance,
                        Mathf.Clamp(centre + side * 1.5f, -limit, limit), pickups);
                    SpawnPickup(moneyPrefab, parent, track, distance,
                        Mathf.Clamp(centre - side * 1.7f, -limit, limit), pickups);
                    SpawnPickup(moneyPrefab, parent, track, distance + 0.6f,
                        Mathf.Clamp(centre - side * 2.5f, -limit, limit), pickups);
                }
                else
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        SpawnPickup(moneyPrefab, parent, track, distance,
                            Mathf.Clamp(centre + k * 1.15f, -limit, limit), pickups);
                    }
                }
            }

            for (int k = 0; k < GameConfig.KeysPerLevel; k++)
            {
                float distance = length * (0.22f + 0.28f * k);
                SpawnPickup(keyPrefab, parent, track, distance, 0f, pickups);
            }
        }

        private static void SpawnPickup(GameObject prefab, Transform parent, TrackPath track,
            float distance, float lateral, List<PickupItem> pickups)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PickupItem item = go.GetComponent<PickupItem>();
            item.Place(track, distance, lateral);
            pickups.Add(item);
        }

        // Чекпоинты уровня: двойная дверь выбора (тема следующего отрезка),
        // дверь-удвоитель x2 и финишная арка.
        private static void PlaceGates(int index, TrackPath track, Transform parent, List<GateZone> gates)
        {
            float length = track.Length;
            float half = track.RoadHalfWidth;
            float choiceDistance = length * 0.3f;
            float multiplierDistance = length * 0.62f;
            float finishDistance = length * 0.965f;

            GateZone leftDoor = CreateGate(parent, track, "Gate_ChoiceLeft", GateKind.Choice,
                ThemeNames[0], 1, 0, choiceDistance, -half * 0.55f, half * 0.55f);
            AttachDoorVisual(leftDoor);

            GateZone rightDoor = CreateGate(parent, track, "Gate_ChoiceRight", GateKind.Choice,
                ThemeNames[1], 1, 1, choiceDistance, half * 0.55f, half * 0.55f);
            AttachDoorVisual(rightDoor);

            GateZone multiplier = CreateGate(parent, track, "Gate_Multiplier", GateKind.Multiplier,
                string.Empty, 2, 0, multiplierDistance, 0f, half);
            AttachDoorVisual(multiplier);

            GateZone finish = CreateGate(parent, track, "Gate_Finish", GateKind.Finish,
                string.Empty, 1, 0, finishDistance, 0f, half);
            AttachFinishArch(finish);

            gates.Add(leftDoor);
            gates.Add(rightDoor);
            gates.Add(multiplier);
            gates.Add(finish);
        }

        private static GateZone CreateGate(Transform parent, TrackPath track, string name, GateKind kind,
            string label, int multiplier, int theme, float distance, float lateral, float halfWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = track.PointAt(distance, lateral);
            go.transform.rotation = track.RotationAt(distance);

            GateZone gate = go.AddComponent<GateZone>();
            gate.Configure(kind, label, multiplier, theme);
            gate.SetTrackPosition(distance, lateral, halfWidth);
            return gate;
        }

        private static void AttachDoorVisual(GateZone gate)
        {
            Material material = LoadMaterial("M_Door");
            GameObject model = LoadModel("Door_Million");

            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Door";
                visual.transform.SetParent(gate.transform, false);
                visual.transform.localScale = Vector3.one * 1.05f;
                ApplyMaterial(visual, material);
            }
            else
            {
                MakePillar(gate.transform, "DoorPost", new Vector3(0f, 1.6f, 0f),
                    new Vector3(0.35f, 3.2f, 0.5f), material);
            }

            string caption = gate.Kind == GateKind.Multiplier ? "×" + gate.Multiplier : gate.Label;
            if (!string.IsNullOrEmpty(caption)) MakeSign(gate.transform, caption, 3.4f, Color.white);
        }

        // Золотая арка, закрывающая уровень.
        private static void AttachFinishArch(GateZone gate)
        {
            Material gold = LoadMaterial("M_Gold");
            MakePillar(gate.transform, "PillarLeft", new Vector3(-2.2f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 0.5f), gold);
            MakePillar(gate.transform, "PillarRight", new Vector3(2.2f, 1.75f, 0f), new Vector3(0.5f, 3.5f, 0.5f), gold);
            MakePillar(gate.transform, "TopBeam", new Vector3(0f, 3.7f, 0f), new Vector3(4.9f, 0.45f, 0.55f), gold);
            MakeSign(gate.transform, "ФИНИШ", 4.35f, new Color(1f, 0.85f, 0.3f));
        }

        private static GameObject MakePillar(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = name;
            pillar.transform.SetParent(parent, false);
            pillar.transform.localPosition = position;
            pillar.transform.localScale = scale;
            pillar.GetComponent<MeshRenderer>().sharedMaterial = material;
            return pillar;
        }

        private static void MakeSign(Transform parent, string caption, float height, Color color)
        {
            var sign = new GameObject("Sign");
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = new Vector3(0f, height, 0f);

            var text = sign.AddComponent<TextMesh>();
            text.text = caption;
            text.font = RuntimeFont.Default;
            text.fontSize = 64;
            text.characterSize = 0.42f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;

            MeshRenderer renderer = sign.GetComponent<MeshRenderer>();
            if (text.font != null) renderer.sharedMaterial = text.font.material;
        }

        // Наборы декораций, переключаемые дверью выбора: пирс вечеринки (ВЕЧЕРИНКА)
        // и пиратские обломки (ПИРАТЫ). Нулевой набор активен, пока не пройдена дверь.
        private static void BuildThemeGroups(int index, TrackPath track, Transform parent,
            List<LevelDefinition.ThemeGroup> themes)
        {
            float start = track.Length * 0.3f + 8f;
            float end = track.Length * 0.62f - 6f;
            if (end <= start) end = start + 12f;

            for (int theme = 0; theme < 2; theme++)
            {
                var root = new GameObject("Theme_" + ThemeNames[theme]);
                root.transform.SetParent(parent, false);
                root.SetActive(theme == 0);

                const int steps = 3;
                for (int s = 0; s < steps; s++)
                {
                    float distance = Mathf.Lerp(start, end, (float)s / (steps - 1));
                    AttachDecor(root.transform, track, theme, distance, s);
                }

                themes.Add(new LevelDefinition.ThemeGroup(theme, root));
            }
        }

        // Пара реквизита по бокам дороги: папарацци для вечеринки, бочки с бутылками для пиратов.
        private static void AttachDecor(Transform root, TrackPath track, int theme, float distance, int step)
        {
            string modelName = theme == 0 ? "photographer" : "bottle";
            GameObject model = LoadModel(modelName);
            Material atlas = LoadMaterial("M_Atlas");
            float side = step % 2 == 0 ? 1f : -1f;

            for (int i = 0; i < 2; i++)
            {
                float lateral = side * (track.RoadHalfWidth + 1.4f + i * 0.9f);
                if (model == null) continue;

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = modelName + "_" + step + "_" + i;
                visual.transform.SetParent(root.transform, false);
                visual.transform.position = track.PointAt(distance, lateral);
                visual.transform.rotation = track.RotationAt(distance) *
                    Quaternion.Euler(0f, (i == 0 ? ModelYaw : 0f) + side * 25f, 0f);
                visual.transform.localScale = Vector3.one * 1.1f;
                ApplyMaterial(visual, atlas);
            }

            // Золотой фонарный столб между реквизитом метит украшенный отрезок
            MakePillar(root.transform, "Lamp_" + step, track.PointAt(distance, 0f) + Vector3.up * 2.4f,
                new Vector3(0.16f, 4.8f, 0.16f), LoadMaterial("M_Gold"));
        }

        // ------------------------------------------------------------------ сборка плеера
        // Батч-хелпер: собирает Windows x64 плеер только со сценой Main.
        public static void BuildWindowsPlayer()
        {
            EnsureFolders();
            string output = "Builds/Win64/RunRich.exe";

            var report = BuildPipeline.BuildPlayer(
                new[] { new EditorBuildSettingsScene(ScenePath, true) },
                output, BuildTarget.StandaloneWindows64, BuildOptions.None);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("[RunRich] player built: " + output + " (" + report.summary.totalSize + " bytes)");
            }
            else
            {
                Debug.LogError("[RunRich] player build failed: " + report.summary.result);
            }
        }

        // ------------------------------------------------------------------ мелкие префабы
        private static void BuildEffectPrefabs()
        {
            Material atlas = LoadMaterial("M_Atlas");

            BuildModelPickup("Pickup_Money", LoadModel("bills"), atlas, PickupKind.Money, GameConfig.MoneyPickupValue, 0.5f, 0.55f, 20f);
            BuildModelPickup("Pickup_Bottle", LoadModel("bottle"), atlas, PickupKind.Bottle, GameConfig.BottlePenalty, 0.6f, 0.45f, 0f);
            BuildStarPickup();
            BuildModelPrefab("Bill", LoadModel("dollar"), atlas, 0.35f);
        }

        private static void BuildModelPickup(string prefabName, GameObject model, Material material, PickupKind kind,
            int amount, float modelScale, float yOffset, float yaw)
        {
            var root = new GameObject(prefabName);
            root.AddComponent<PickupItem>().Configure(kind, amount);

            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Model";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * modelScale;
                visual.transform.localPosition = new Vector3(0f, yOffset, 0f);
                visual.transform.localRotation = Quaternion.Euler(0f, ModelYaw + yaw, 0f);
                ApplyMaterial(visual, material);
            }

            SavePrefab(root, PrefabsRoot + "/" + prefabName + ".prefab");
        }

        /// Золотой ключ-бонус: звёздный меш из пака плюс золотой материал.
        private static void BuildStarPickup()
        {
            var root = new GameObject("Pickup_Key");
            root.AddComponent<PickupItem>().Configure(PickupKind.Key, GameConfig.KeyBonus);

            Mesh star = LoadMesh("Star");
            if (star != null)
            {
                var visual = new GameObject("Model");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * 0.75f;
                visual.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                visual.AddComponent<MeshFilter>().sharedMesh = star;
                visual.AddComponent<MeshRenderer>().sharedMaterial = LoadMaterial("M_Gold");
            }

            SavePrefab(root, PrefabsRoot + "/Pickup_Key.prefab");
        }

        private static void BuildModelPrefab(string prefabName, GameObject model, Material material, float scale)
        {
            var root = new GameObject(prefabName);

            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Model";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * scale;
                ApplyMaterial(visual, material);
            }

            SavePrefab(root, PrefabsRoot + "/" + prefabName + ".prefab");
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);
            if (!success) Debug.LogError("[RunRich] failed to save prefab: " + path);
        }

        private static void ApplyMaterial(GameObject root, Material material)
        {
            if (material == null) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int m = 0; m < materials.Length; m++) materials[m] = material;
                renderers[i].sharedMaterials = materials;
            }
        }

        private static Material LoadMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/" + name + ".mat");

        private static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/" + name + ".prefab");

        // ------------------------------------------------------------------ прокидывание ссылок
        internal static void SetRef(Object target, string fieldName, Object value)
        {
            SetValue(target, fieldName, property => property.objectReferenceValue = value);
        }

        internal static void SetArray(Object target, string fieldName, Object[] values)
        {
            SetValue(target, fieldName, property =>
            {
                property.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                }
            });
        }

        internal static void SetBool(Object target, string fieldName, bool value)
        {
            SetValue(target, fieldName, property => property.boolValue = value);
        }

        internal static void SetInt(Object target, string fieldName, int value)
        {
            SetValue(target, fieldName, property => property.intValue = value);
        }

        internal static void SetFloat(Object target, string fieldName, float value)
        {
            SetValue(target, fieldName, property => property.floatValue = value);
        }

        internal static void SetValue(Object target, string fieldName, System.Action<SerializedProperty> apply)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("[RunRich] field '" + fieldName + "' not found on " + target.GetType().Name);
                return;
            }

            apply(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ главная сцена
        // Собирает играбельную сцену Main: системы, камера, игрок, канвас, свет и небо.
        private static void BuildMainScene()
        {
            EnsureFolders();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var systems = new GameObject("[Systems]");

            // менеджер уровней + список
            var lmGo = new GameObject("LevelManager");
            lmGo.transform.SetParent(systems.transform, false);
            LevelManager levelManager = lmGo.AddComponent<LevelManager>();
            LevelsList levelsList = AssetDatabase.LoadAssetAtPath<LevelsList>(DataRoot + "/LevelsList.asset");
            if (levelsList == null) Debug.LogError("[RunRich] LevelsList asset missing");
            SetRef(levelManager, "levels", levelsList);
            SetBool(levelManager, "editorMode", false);
            SetInt(levelManager, "CurrentLevelIndex", -1);

            // ввод свайпом
            var inputGo = new GameObject("Input");
            inputGo.transform.SetParent(systems.transform, false);
            DragInput input = inputGo.AddComponent<DragInput>();

            // звук
            AudioManager audioManager = BuildAudio(systems.transform);

            // камера
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.transform.SetParent(systems.transform, false);
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = GameConfig.CameraOffset;
            cameraGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.fieldOfView = GameConfig.CameraFov;
            camera.clearFlags = CameraClearFlags.Skybox;
            CameraFollow cameraRig = cameraGo.AddComponent<CameraFollow>();

            // эффекты
            var fxGo = new GameObject("FxManager");
            fxGo.transform.SetParent(systems.transform, false);
            FxManager fx = fxGo.AddComponent<FxManager>();
            fx.EditorAssign(camera, RuntimeFont.Default, LoadPrefab("Bill"),
                LoadMaterial("M_Spark"), LoadMaterial("M_SparkGold"), LoadMaterial("M_SparkRed"),
                LoadMaterial("M_Atlas"));

            // игрок
            PlayerController player = BuildPlayer(systems.transform, input);

            // бот-автопилот (включается флагом -autopilot)
            var botGo = new GameObject("Autopilot");
            botGo.transform.SetParent(systems.transform, false);
            Autopilot autopilot = botGo.AddComponent<Autopilot>();
            SetRef(autopilot, "input", input);
            SetRef(autopilot, "player", player);

            // интерфейс
            GameObject canvas = BuildCanvas();
            HudController hud = BuildHud(canvas.transform);
            ResultScreenController resultScreen = BuildResultScreen(canvas.transform);

            // гейм-менеджер связывает всё воедино
            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(systems.transform, false);
            GameManager gameManager = gmGo.AddComponent<GameManager>();
            SetRef(gameManager, "levelManager", levelManager);
            SetRef(gameManager, "player", player);
            SetRef(gameManager, "input", input);
            SetRef(gameManager, "cameraRig", cameraRig);
            SetRef(gameManager, "hud", hud);
            SetRef(gameManager, "resultScreen", resultScreen);
            SetRef(gameManager, "fx", fx);
            SetRef(gameManager, "audioManager", audioManager);
            SetRef(gameManager, "autopilot", autopilot);

            SetRef(hud, "gameManager", gameManager);
            SetRef(resultScreen, "gameManager", gameManager);
            RunnerOutfit outfit = player.GetComponent<RunnerOutfit>();
            if (outfit != null)
            {
                SetRef(outfit, "gameManager", gameManager);
                SetRef(outfit, "finaleMaterial", LoadMaterial("M_Finale"));
            }

            BuildEnvironment();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[RunRich] main scene saved: " + ScenePath);
        }

        // Солнце, скайбокс и фоновый свет сцены пирса.
        private static void BuildEnvironment()
        {
            var lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            Material sky = LoadMaterial("M_Sky");
            if (sky != null) RenderSettings.skybox = sky;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.5f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.3f);
        }

        // Источники SFX + музыки и клипы из референсного пака.
        private static AudioManager BuildAudio(Transform parent)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);

            var sfxGo = new GameObject("Sfx");
            sfxGo.transform.SetParent(go.transform, false);
            AudioSource sfx = sfxGo.AddComponent<AudioSource>();
            sfx.playOnAwake = false;

            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(go.transform, false);
            AudioSource music = musicGo.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;

            AudioManager manager = go.AddComponent<AudioManager>();
            manager.EditorAssign(
                sfx, music,
                LoadClip("collect_coin.ogg"),
                LoadClip("RemoveMoney.ogg"),
                LoadClip("click.ogg"),
                LoadClip("shortcutrun_sfx_jingle_victory.ogg"),
                LoadClip("WinJackpot.ogg"),
                LoadClip("shortcutrun_sfx_joueur_bonus_multiplier_x01_to_x05_variation01.ogg"),
                LoadClip("coin.ogg"),
                LoadClip("knockemall_sfx_enemy_robot_fall02.ogg"),
                LoadClip("shortcutrun_sfx_joueur_boost_pont_V3.ogg"),
                new[]
                {
                    LoadClip("SFX_Footstep_1.ogg"), LoadClip("SFX_Footstep_2.ogg"),
                    LoadClip("SFX_Footstep_3.ogg"), LoadClip("SFX_Footstep_4.ogg")
                },
                new[] { LoadClip("HighHeels_1.ogg"), LoadClip("HighHeels_2.ogg") });
            manager.EditorSetHighHeels(false);
            return manager;
        }

        // Капсула игрока: контроллер + процедурная анимация + перекраска, внутри ригованный FBX.
        private static PlayerController BuildPlayer(Transform parent, DragInput input)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;

            PlayerController player = go.AddComponent<PlayerController>();
            RunnerAnimator animator = go.AddComponent<RunnerAnimator>();
            go.AddComponent<RunnerOutfit>();
            SetRef(player, "input", input);
            SetRef(player, "animator", animator);

            GameObject model = LoadModel("player");
            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Model";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
                ApplyMaterial(visual, LoadMaterial("M_Atlas"));
            }

            return player;
        }

        // Канвас-оверлей с телефонным референсным разрешением.
        private static GameObject BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventGo = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventGo.AddComponent<StandaloneInputModule>();
#endif
            return canvasGo;
        }

        // Растянутый на весь экран дочерний рект.
        private static RectTransform MakeRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Text MakeLabel(Transform parent, string name, string content, int fontSize, Color color,
            TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.font = RuntimeFont.Default;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            if (sprite != null) image.sprite = sprite;
            image.color = color;
            return image;
        }

        // Счётчик денег, шкала богатства, ключи, прогресс-бар, туториал и тост.
        private static HudController BuildHud(Transform canvas)
        {
            RectTransform hud = MakeRect(canvas, "Hud");
            HudController controller = hud.gameObject.AddComponent<HudController>();

            Text levelText = MakeLabel(hud, "LevelText", string.Empty, 52, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(820f, 70f));

            // круглая шкала богатства с подписью стадии внутри
            Image tierGauge = MakeImage(hud, "TierGauge", LoadSprite("circle_gauge.png"), Color.white,
                new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(260f, 260f));
            tierGauge.type = Image.Type.Filled;
            tierGauge.fillMethod = Image.FillMethod.Radial360;
            tierGauge.fillOrigin = (int)Image.Origin360.Top;
            tierGauge.fillAmount = 0f;
            Text tierText = MakeLabel(tierGauge.transform, "TierText", "БЕДНЫЙ", 44, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 60f));

            Text moneyText = MakeLabel(hud, "MoneyText", "0$", 84, new Color(1f, 0.82f, 0.16f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(90f, -420f), new Vector2(420f, 100f));

            Text keysText = MakeLabel(hud, "KeysText", "0/3", 64, Color.white,
                TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-90f, -420f), new Vector2(420f, 90f));

            Text hintText = MakeLabel(hud, "HintText", string.Empty, 54, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, -430f), new Vector2(900f, 80f));

            Text toastText = MakeLabel(hud, "ToastText", string.Empty, 72, new Color(1f, 0.85f, 0.3f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(900f, 110f));

            // прогресс-бар с маркером бегуна
            RectTransform barBack = MakeRect(hud, "ProgressBack");
            barBack.anchorMin = new Vector2(0.5f, 0f);
            barBack.anchorMax = new Vector2(0.5f, 0f);
            barBack.pivot = new Vector2(0.5f, 0.5f);
            barBack.anchoredPosition = new Vector2(0f, 170f);
            barBack.sizeDelta = new Vector2(880f, 44f);
            Image barImage = barBack.gameObject.AddComponent<Image>();
            barImage.color = new Color(0f, 0f, 0f, 0.45f);

            RectTransform fillRect = MakeRect(barBack, "ProgressFill");
            Image progressFill = fillRect.gameObject.AddComponent<Image>();
            progressFill.color = new Color(1f, 0.79f, 0.22f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0f;

            RectTransform marker = MakeRect(barBack, "ProgressMarker");
            marker.anchorMin = new Vector2(0.5f, 0.5f);
            marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.anchoredPosition = Vector2.zero;
            marker.sizeDelta = new Vector2(52f, 52f);
            Image markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.sprite = LoadSprite("Circle.png");
            markerImage.raycastTarget = false;

            // туториал: рука + подсказка свайпа
            RectTransform tutorial = MakeRect(hud, "Tutorial");
            Image hand = MakeImage(tutorial, "Hand", LoadSprite("hand.png"), Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(220f, 220f));
            hand.raycastTarget = false;
            MakeLabel(tutorial, "TutorialLabel", GameConfig.TutorialLabel, 46, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(940f, 200f));

            SetRef(controller, "moneyText", moneyText);
            SetRef(controller, "keysText", keysText);
            SetRef(controller, "levelText", levelText);
            SetRef(controller, "tierText", tierText);
            SetRef(controller, "hintText", hintText);
            SetRef(controller, "toastText", toastText);
            SetRef(controller, "tierGauge", tierGauge);
            SetRef(controller, "progressFill", progressFill);
            SetRef(controller, "progressMarker", marker);
            SetRef(controller, "tutorialRoot", tutorial.gameObject);
            return controller;
        }

        // Панель победы / поражения: заголовок, заработок и кнопки забрать / ещё раз.
        private static ResultScreenController BuildResultScreen(Transform canvas)
        {
            var screenGo = new GameObject("ResultScreen", typeof(RectTransform));
            RectTransform screen = (RectTransform)screenGo.transform;
            screen.SetParent(canvas, false);
            screen.anchorMin = Vector2.zero;
            screen.anchorMax = Vector2.one;
            screen.offsetMin = Vector2.zero;
            screen.offsetMax = Vector2.zero;
            ResultScreenController controller = screenGo.AddComponent<ResultScreenController>();

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            RectTransform panel = (RectTransform)panelGo.transform;
            panel.SetParent(screen, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            Image backdrop = panelGo.GetComponent<Image>();
            backdrop.color = new Color(0.05f, 0.05f, 0.1f, 0.75f);
            panelGo.SetActive(false);

            Text titleText = MakeLabel(panel, "TitleText", string.Empty, 96, new Color(0.98f, 0.82f, 0.2f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 520f), new Vector2(960f, 140f));
            Text subtitleText = MakeLabel(panel, "SubtitleText", string.Empty, 48, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(900f, 80f));
            Text moneyText = MakeLabel(panel, "MoneyText", "+0$", 100, new Color(1f, 0.82f, 0.16f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(900f, 140f));
            Text keysText = MakeLabel(panel, "KeysText", string.Empty, 48, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(900f, 80f));

            Button takeButton = BuildResultButton(panel, "TakeButton", LoadSprite("button.png"),
                new Vector2(0f, -220f), GameConfig.TakeLabel, out Text takeLabel);
            Button retryButton = BuildResultButton(panel, "RetryButton", LoadSprite("button.png"),
                new Vector2(0f, -220f), GameConfig.RetryLabel, out Text retryLabel);

            SetRef(controller, "root", panelGo);
            SetRef(controller, "backdrop", backdrop);
            SetRef(controller, "titleText", titleText);
            SetRef(controller, "subtitleText", subtitleText);
            SetRef(controller, "moneyText", moneyText);
            SetRef(controller, "keysText", keysText);
            SetRef(controller, "takeButton", takeButton);
            SetRef(controller, "takeLabel", takeLabel);
            SetRef(controller, "retryButton", retryButton);
            SetRef(controller, "retryLabel", retryLabel);
            return controller;
        }

        private static Button BuildResultButton(Transform panel, string name, Sprite sprite,
            Vector2 position, string caption, out Text label)
        {
            GameObject button = MakeImage(panel, name, sprite, Color.white,
                new Vector2(0.5f, 0.5f), position, new Vector2(620f, 170f)).gameObject;
            Button component = button.AddComponent<Button>();
            component.targetGraphic = button.GetComponent<Image>();

            label = MakeLabel(button.transform, "Label", caption, 56, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 90f));
            return component;
        }
    }
}
