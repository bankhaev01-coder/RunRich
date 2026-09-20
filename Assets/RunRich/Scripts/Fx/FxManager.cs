using System.Collections;
using UnityEngine;

namespace RunRich
{
    // Весь фидбек бегуна: вспышки искр, всплывающие «+5$» и фонтан денег.
    [DisallowMultipleComponent]
    public sealed class FxManager : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Camera fxCamera;
        [SerializeField] private Font popupFont;
        [SerializeField] private GameObject billPrefab;

        [Header("Materials")]
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material redMaterial;
        [SerializeField] private Material billMaterial;

        [Header("Colours")]
        [SerializeField] private Color moneyColor = new Color(0.35f, 0.86f, 0.2f);
        [SerializeField] private Color lossColor = new Color(0.95f, 0.28f, 0.24f);
        [SerializeField] private Color goldColor = new Color(1f, 0.82f, 0.16f);

        private void Start()
        {
            if (fxCamera == null) fxCamera = Camera.main;
            if (popupFont == null) popupFont = RuntimeFont.Default;
        }

        public void PlayPickup(Vector3 position, int amount)
        {
            Burst(position, sparkMaterial, 14, 5.5f, 0.34f);
            Popup(position, "+" + amount + "$", moneyColor, 1f);
        }

        public void PlayBottleHit(Vector3 position)
        {
            Burst(position, redMaterial, 14, 4.6f, 0.4f);
            Popup(position, "-" + GameConfig.BottlePenalty + "$", lossColor, 1.15f);
        }

        public void PlayKeyPickup(Vector3 position, int amount)
        {
            Burst(position, goldMaterial, 24, 6.5f, 0.42f);
            Popup(position, "+" + amount + "$", goldColor, 1.25f);
        }

        public void PlayMultiplier(Vector3 position, int multiplier)
        {
            Burst(position, goldMaterial, 34, 7.5f, 0.5f);
            Popup(position, "×" + multiplier, goldColor, 2.1f);
            StartCoroutine(MoneySplash(position, 10, 6f));
        }

        public void PlayChoice(Vector3 position)
        {
            Burst(position, goldMaterial, 20, 6f, 0.4f);
        }

        public void PlayTierUp(Vector3 position, int tierIndex)
        {
            Burst(position + Vector3.up, sparkMaterial, 46, 7f, 0.62f);
            Popup(position, WealthStages.Get(tierIndex).Label, goldColor, 1.6f);
        }

        public void PlayVictory(Vector3 position)
        {
            StartCoroutine(MoneyFountain(position));
        }

        private IEnumerator MoneyFountain(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 1.6f;
            for (int wave = 0; wave < 4; wave++)
            {
                for (int i = 0; i < 12; i++)
                {
                    FlyingMoney.Spawn(billPrefab, billMaterial, origin + Random.insideUnitSphere * 0.5f, Vector3.up, 7f);
                }
                Burst(origin, goldMaterial, 18, 6.5f, 0.4f);
                yield return new WaitForSeconds(0.22f);
            }
        }

        private IEnumerator MoneySplash(Vector3 position, int count, float force)
        {
            for (int i = 0; i < count; i++)
            {
                FlyingMoney.Spawn(billPrefab, billMaterial, position + Vector3.up, Vector3.up, force);
            }
            yield return null;
        }

        private void Popup(Vector3 position, string content, Color color, float scale)
        {
            FloatingText.Spawn(position, content, color, popupFont, fxCamera, scale);
        }

        // Короткая вспышка частиц; объект сам себя удаляет.
        private void Burst(Vector3 position, Material material, int count, float speed, float size)
        {
            var go = new GameObject("FxBurst");
            go.transform.position = position;

            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(32, count * 2);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (material != null) renderer.sharedMaterial = material;

            system.Play();
            system.Emit(count);
            Destroy(go, 1.6f);
        }

        // Прокидывание ссылок в редакторе, использует сборщик уровней.
        public void EditorAssign(Camera camera, Font font, GameObject bill, Material spark, Material gold, Material red, Material billMat)
        {
            fxCamera = camera;
            popupFont = font;
            billPrefab = bill;
            sparkMaterial = spark;
            goldMaterial = gold;
            redMaterial = red;
            billMaterial = billMat;
        }
    }
}
