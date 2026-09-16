using System.Collections.Generic;
using UnityEngine;

namespace RunRich
{
    /// Процедурная анимация бегуна (у привезённого FBX риг Mixamo, но нет клипов).
    /// Каждая кость качается вокруг оси, которая отвечает за направление "вперёд / назад" персонажа.
    [DisallowMultipleComponent]
    public sealed class RunnerAnimator : MonoBehaviour
    {
        private struct BoneDriver
        {
            public Transform Bone;
            public Quaternion RestLocal;
            public Vector3 SwingAxis;
            public string Key;
        }

        [SerializeField] private float strideFrequency = 2.6f;
        [SerializeField] private float legSwing = 42f;
        [SerializeField] private float kneeBend = 58f;
        [SerializeField] private float armSwing = 38f;
        [SerializeField] private float elbowBend = 34f;
        [SerializeField] private float hipBob = 0.075f;
        [SerializeField] private float spineTwist = 9f;

        private static readonly string[] RequiredBones =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
            "LeftShoulder", "RightShoulder", "LeftArm", "RightArm", "LeftForeArm", "RightForeArm",
            "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "LeftFoot", "RightFoot"
        };

        private readonly List<BoneDriver> _drivers = new List<BoneDriver>();
        private Transform _hips;
        private Vector3 _restHipsLocal;
        private float _phase;
        private bool _ready;

        /// Запоминает импортированную позу покоя: вызывается один раз до любого Tick.
        public void CaptureRestPose()
        {
            if (_ready) return;

            _drivers.Clear();
            _hips = null;
            Transform root = transform;

            foreach (string required in RequiredBones)
            {
                Transform bone = FindDeep(root, required);
                if (bone == null) continue;

                _drivers.Add(new BoneDriver
                {
                    Bone = bone,
                    RestLocal = bone.localRotation,
                    SwingAxis = bone.InverseTransformDirection(root.right),
                    Key = required
                });

                if (required == "Hips")
                {
                    _hips = bone;
                    _restHipsLocal = bone.localPosition;
                }
            }

            _ready = _drivers.Count > 0;
        }

        /// Продвигает беговой цикл. Параметр speed01 - доля максимальной скорости, 0..1.
        public void Tick(float speed01, float deltaTime, bool running)
        {
            if (!_ready) return;

            float frequency = running ? strideFrequency * Mathf.Max(0.35f, speed01) : 0.55f;
            _phase += deltaTime * frequency * Mathf.PI * 2f;
            if (_phase > Mathf.PI * 4f) _phase -= Mathf.PI * 4f;

            float amount = running ? Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(speed01)) : 0.1f;

            for (int i = 0; i < _drivers.Count; i++)
            {
                BoneDriver driver = _drivers[i];
                float angle = AngleFor(driver.Key, _phase, amount);
                driver.Bone.localRotation = driver.RestLocal * Quaternion.AngleAxis(angle, driver.SwingAxis);
            }

            if (_hips != null)
            {
                float bob = running ? Mathf.Abs(Mathf.Sin(_phase)) * hipBob * amount : 0f;
                _hips.localPosition = _restHipsLocal + new Vector3(0f, bob, 0f);
            }
        }

        /// Ставит персонажа в нейтральную позу стоя (между забегами).
        public void RestPose()
        {
            for (int i = 0; i < _drivers.Count; i++)
            {
                BoneDriver driver = _drivers[i];
                driver.Bone.localRotation = driver.RestLocal;
            }
            if (_hips != null) _hips.localPosition = _restHipsLocal;
            _phase = 0f;
        }

        private float AngleFor(string key, float phase, float amount)
        {
            float wave = Mathf.Sin(phase);
            float opposite = Mathf.Sin(phase + Mathf.PI);

            switch (key)
            {
                case "LeftUpLeg": return wave * legSwing * amount;
                case "RightUpLeg": return opposite * legSwing * amount;
                case "LeftLeg": return Mathf.Max(0f, -wave) * kneeBend * amount;
                case "RightLeg": return Mathf.Max(0f, -opposite) * kneeBend * amount;
                case "LeftFoot": return -wave * 12f * amount;
                case "RightFoot": return -opposite * 12f * amount;
                case "LeftArm": return opposite * armSwing * amount + 6f;
                case "RightArm": return wave * armSwing * amount + 6f;
                case "LeftForeArm": return -(elbowBend * amount + Mathf.Max(0f, -opposite) * 18f);
                case "RightForeArm": return -(elbowBend * amount + Mathf.Max(0f, -wave) * 18f);
                case "Spine": return wave * 2f * amount;
                case "Spine1": return opposite * spineTwist * 0.35f * amount;
                case "Spine2": return wave * spineTwist * 0.3f * amount;
                case "Head": return -wave * 2.5f * amount;
                default: return 0f;
            }
        }

        private static Transform FindDeep(Transform root, string namePart)
        {
            string clean = root.name.Replace("mixamorig:", string.Empty);
            if (clean.Equals(namePart, System.StringComparison.OrdinalIgnoreCase)) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDeep(root.GetChild(i), namePart);
                if (result != null) return result;
            }
            return null;
        }
    }
}
