using UnityEngine;

namespace RunRich
{
    // Катмулл-Ром трек бегуна. Весь уровень описывается горстью контрольных точек;
    // позиции вдоль трека адресуются пройденной дистанцией.
    public sealed class TrackSpline
    {
        private readonly Vector3[] _points;
        private readonly float[] _sampleDistances;
        private readonly Vector3[] _samples;
        private readonly Vector3[] _sampleTangents;
        private readonly float _sampleStep;

        public float Length => _sampleDistances[_sampleDistances.Length - 1];

        public TrackSpline(Vector3[] controlPoints, float sampleStep = 0.5f)
        {
            if (controlPoints == null || controlPoints.Length < 2)
            {
                throw new System.ArgumentException("A track needs at least two control points.");
            }

            _sampleStep = Mathf.Max(0.1f, sampleStep);
            _points = controlPoints;

            // семплируем кривую фиксированным шагом и запоминаем длину дуги каждого семпла
            int controlSegments = controlPoints.Length - 1;
            int samplesPerSegment = Mathf.Max(2, Mathf.CeilToInt(
                Vector3.Distance(controlPoints[0], controlPoints[1]) / _sampleStep)) + 1;

            var samples = new System.Collections.Generic.List<Vector3>();
            for (int seg = 0; seg < controlSegments; seg++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(0, seg - 1)];
                Vector3 p1 = controlPoints[seg];
                Vector3 p2 = controlPoints[seg + 1];
                Vector3 p3 = controlPoints[Mathf.Min(controlPoints.Length - 1, seg + 2)];

                float segmentLength = Vector3.Distance(p1, p2);
                int steps = Mathf.Max(2, Mathf.CeilToInt(segmentLength / _sampleStep));
                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)steps;
                    samples.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            samples.Add(controlPoints[controlPoints.Length - 1]);

            _samples = samples.ToArray();
            _sampleTangents = new Vector3[_samples.Length];
            _sampleDistances = new float[_samples.Length];

            float distance = 0f;
            _sampleDistances[0] = 0f;
            for (int i = 1; i < _samples.Length; i++)
            {
                distance += Vector3.Distance(_samples[i - 1], _samples[i]);
                _sampleDistances[i] = distance;
            }

            for (int i = 0; i < _samples.Length; i++)
            {
                int next = Mathf.Min(i + 1, _samples.Length - 1);
                int prev = Mathf.Max(i - 1, 0);
                _sampleTangents[i] = (_samples[next] - _samples[prev]).normalized;
                if (_sampleTangents[i].sqrMagnitude < 0.0001f) _sampleTangents[i] = Vector3.forward;
            }
        }

        public int SampleCount => _samples.Length;
        public Vector3 SamplePoint(int index) => _samples[Mathf.Clamp(index, 0, _samples.Length - 1)];
        public float SampleDistance(int index) => _sampleDistances[Mathf.Clamp(index, 0, _sampleDistances.Length - 1)];

        // Позиция кривой на заданной дистанции (кламп к треку).
        public Vector3 GetPosition(float distance)
        {
            GetSampleIndices(distance, out int a, out int b, out float t);
            return Vector3.Lerp(_samples[a], _samples[b], t);
        }

        // Нормированное направление вперёд на заданной дистанции.
        public Vector3 GetTangent(float distance)
        {
            GetSampleIndices(distance, out int a, out int b, out float t);
            Vector3 tangent = Vector3.Lerp(_sampleTangents[a], _sampleTangents[b], t);
            return tangent.sqrMagnitude < 0.0001f ? Vector3.forward : tangent.normalized;
        }

        // Вектор вправо от трека на дистанции (для боковых смещений).
        public Vector3 GetRight(float distance)
        {
            Vector3 tangent = GetTangent(distance);
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            return right;
        }

        // Мировая позиция на треке: вперёд на дистанцию, вбок на смещение.
        public Vector3 GetPoint(float distance, float lateral) => GetPosition(distance) + GetRight(distance) * lateral;

        // Поворот объекта, едущего вдоль трека.
        public Quaternion GetRotation(float distance) => Quaternion.LookRotation(GetTangent(distance), Vector3.up);

        private void GetSampleIndices(float distance, out int a, out int b, out float t)
        {
            distance = Mathf.Clamp(distance, 0f, Length);

            int low = 0;
            int high = _sampleDistances.Length - 1;
            while (low < high - 1)
            {
                int mid = (low + high) / 2;
                if (_sampleDistances[mid] <= distance) low = mid;
                else high = mid;
            }

            a = low;
            b = Mathf.Min(low + 1, _sampleDistances.Length - 1);
            float span = _sampleDistances[b] - _sampleDistances[a];
            t = span <= 0.0001f ? 0f : (distance - _sampleDistances[a]) / span;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
