using UnityEngine;
using UnityEngine.UI;

namespace RunRich
{
    // Маленькая подпись «+5$» в мире: всплывает вверх и гаснет.
    public sealed class FloatingText : MonoBehaviour
    {
        [SerializeField] private float lifetime = 1.15f;
        [SerializeField] private float riseSpeed = 2.1f;
        [SerializeField] private float startScale = 1.35f;

        private Text _label;
        private Transform _pivot;
        private Camera _camera;
        private float _age;
        private Vector3 _drift;

        public static FloatingText Spawn(Vector3 worldPosition, string content, Color color, Font font, Camera camera, float scale = 1f)
        {
            var root = new GameObject("FloatingText");
            root.transform.position = worldPosition + new Vector3(0f, 1.35f, 0f);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(320f, 120f);
            root.transform.localScale = Vector3.one * (0.011f * scale);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = 78;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(320f, 120f);
            rect.anchoredPosition = Vector2.zero;

            var outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var floating = root.AddComponent<FloatingText>();
            floating._label = text;
            floating._pivot = root.transform;
            floating._camera = camera;
            floating._drift = new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.2f, 0.2f));
            return floating;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / lifetime);

            _pivot.position += (_drift + Vector3.up * riseSpeed) * Time.deltaTime;

            float scale = Mathf.Lerp(startScale, 1f, Mathf.Clamp01(t * 3f));
            _pivot.localScale = Vector3.one * (0.011f * scale);

            if (_label != null)
            {
                Color color = _label.color;
                color.a = Mathf.Clamp01(1f - (t - 0.55f) / 0.45f);
                _label.color = color;
            }

            if (_camera != null)
            {
                _pivot.rotation = Quaternion.LookRotation(_pivot.position - _camera.transform.position, Vector3.up);
            }

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
