using System.Collections.Generic;
using UnityEngine;

namespace RunRich
{
    /// Дорога, по которой бежит игрок: держит контрольные точки трассы, строит
    /// белую ленту пирса (со скруглённой кромкой и боковинами плит) и море вокруг неё.
    [DisallowMultipleComponent]
    public sealed class TrackPath : MonoBehaviour
    {
        [Header("Geometry")]
        [SerializeField] private Vector3[] controlPoints = new Vector3[0];
        [SerializeField] private float roadHalfWidth = GameConfig.RoadHalfWidth;
        [SerializeField] private float slabThickness = 1.8f;
        [SerializeField] private float edgeInset = 0.55f;
        [SerializeField] private float sampleStep = 0.5f;

        [Header("Materials")]
        [SerializeField] private Material roadMaterial;
        [SerializeField] private Material roadEdgeMaterial;
        [SerializeField] private Material waterMaterial;

        [Header("Sea")]
        [SerializeField] private bool buildSea = true;
        [SerializeField] private float seaSize = 900f;
        [SerializeField] private float seaLevel = -1.15f;

        public TrackSpline Spline { get; private set; }
        public float Length => Spline?.Length ?? 0f;
        public float RoadHalfWidth => roadHalfWidth;
        public float SeaLevel => seaLevel;

        private void Awake()
        {
            Build();
        }

        public void SetControlPoints(Vector3[] points) => controlPoints = points;

        public void SetMaterials(Material road, Material edge, Material water)
        {
            roadMaterial = road;
            roadEdgeMaterial = edge;
            waterMaterial = water;
        }

        /// Пересобирает сплайн и сгенерированные меши.
        public void Build()
        {
            if (controlPoints == null || controlPoints.Length < 2) return;

            Spline = new TrackSpline(controlPoints, sampleStep);

            BuildRoadMesh();
            if (buildSea) BuildSea();
        }

#if UNITY_EDITOR
        /// Хелпер редактора: удаляет меши, сгенерированные <see cref="Build"/>, чтобы сохранённый
        /// префаб оставался лёгким (в рантайме их пересоздаёт Awake).
        public void ClearGeneratedMeshes()
        {
            Transform road = transform.Find("RoadMesh");
            if (road != null) DestroyImmediate(road.gameObject);
            Transform sea = transform.Find("Sea");
            if (sea != null) DestroyImmediate(sea.gameObject);
        }
#endif

        /// Удобная обёртка: мировая позиция центра дороги на заданной дистанции.
        public Vector3 PointAt(float distance, float lateral = 0f)
        {
            if (Spline == null) return transform.position;
            return transform.TransformPoint(Spline.GetPoint(distance, lateral));
        }

        public Quaternion RotationAt(float distance)
        {
            if (Spline == null) return transform.rotation;
            return transform.rotation * Spline.GetRotation(distance);
        }

        // ------------------------------------------------------------------ генерация мешей
        private void BuildRoadMesh()
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            int sampleCount = Spline.SampleCount;
            float v = 0f;

            // 5 точек поперечного сечения: крайняя левая, внутренняя левая, центр, внутренняя правая, крайняя правая
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 centre = Spline.SamplePoint(i);
                Vector3 forward = i < sampleCount - 1
                    ? (Spline.SamplePoint(i + 1) - centre).normalized
                    : (centre - Spline.SamplePoint(i - 1)).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                float half = roadHalfWidth;
                float inner = Mathf.Max(0.1f, roadHalfWidth - edgeInset);

                vertices.Add(centre + right * -half);
                vertices.Add(centre + right * -inner);
                vertices.Add(centre);
                vertices.Add(centre + right * inner);
                vertices.Add(centre + right * half);

                for (int k = 0; k < 5; k++) uvs.Add(new Vector2(k / 4f, v));
                v += sampleStep / 8f;
            }

            for (int i = 0; i < sampleCount - 1; i++)
            {
                int row = i * 5;
                int next = row + 5;
                for (int k = 0; k < 4; k++)
                {
                    triangles.Add(row + k);
                    triangles.Add(next + k);
                    triangles.Add(row + k + 1);

                    triangles.Add(row + k + 1);
                    triangles.Add(next + k);
                    triangles.Add(next + k + 1);
                }
            }

            AddSlabSides(vertices, uvs, triangles, sampleCount);

            var mesh = new Mesh { name = "RoadMesh" };
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var existing = transform.Find("RoadMesh");
            if (existing != null) Destroy(existing.gameObject);

            var go = new GameObject("RoadMesh");
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadMaterial != null
                ? roadMaterial
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void AddSlabSides(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int sampleCount)
        {
            int baseIndex = vertices.Count;
            float half = roadHalfWidth;

            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 centre = Spline.SamplePoint(i);
                Vector3 forward = i < sampleCount - 1
                    ? (Spline.SamplePoint(i + 1) - centre).normalized
                    : (centre - Spline.SamplePoint(i - 1)).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                vertices.Add(centre + right * -half);
                vertices.Add(centre + right * -half + Vector3.down * slabThickness);
                vertices.Add(centre + right * half);
                vertices.Add(centre + right * half + Vector3.down * slabThickness);
                uvs.Add(new Vector2(0f, i * sampleStep / 4f));
                uvs.Add(new Vector2(0.25f, i * sampleStep / 4f));
                uvs.Add(new Vector2(0.5f, i * sampleStep / 4f));
                uvs.Add(new Vector2(0.75f, i * sampleStep / 4f));
            }

            for (int i = 0; i < sampleCount - 1; i++)
            {
                int row = baseIndex + i * 4;
                int next = row + 4;

                triangles.Add(row + 0); triangles.Add(row + 1); triangles.Add(next + 0);
                triangles.Add(next + 0); triangles.Add(row + 1); triangles.Add(next + 1);

                triangles.Add(row + 2); triangles.Add(next + 2); triangles.Add(row + 3);
                triangles.Add(row + 3); triangles.Add(next + 2); triangles.Add(next + 3);
            }
        }

        private void BuildSea()
        {
            var existing = transform.Find("Sea");
            if (existing != null) Destroy(existing.gameObject);

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Sea";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(seaSize / 10f, 1f, seaSize / 10f);

            Vector3 first = Spline.GetPosition(0f);
            Vector3 last = Spline.GetPosition(Spline.Length);
            Vector3 centre = (first + last) * 0.5f;
            go.transform.localPosition = new Vector3(centre.x, seaLevel, centre.z);

            var renderer = go.GetComponent<MeshRenderer>();
            if (waterMaterial != null) renderer.sharedMaterial = waterMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
