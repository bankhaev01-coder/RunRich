using UnityEngine;

namespace RunRich
{
    // Купюра, вылетающая из бегуна (денежный дождь на финише и на подборах).
    public sealed class FlyingMoney : MonoBehaviour
    {
        [SerializeField] private float lifetime = 1.6f;
        [SerializeField] private float gravity = -9.5f;
        [SerializeField] private float spin = 420f;

        private Vector3 _velocity;
        private Vector3 _rotationAxis;
        private float _age;

        // Спавнит купюру со случайным толчком.
        public static FlyingMoney Spawn(GameObject billPrefab, Material material, Vector3 position,
            Vector3 direction, float force)
        {
            if (billPrefab == null) return null;

            GameObject instance = Instantiate(billPrefab, position, Random.rotation);
            instance.name = "FlyingBill";
            instance.transform.localScale = billPrefab.transform.localScale * Random.Range(0.85f, 1.15f);

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            var flying = instance.AddComponent<FlyingMoney>();
            Vector3 spread = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.6f, 1.4f), Random.Range(-0.4f, 0.4f));
            flying._velocity = (direction + spread).normalized * force * Random.Range(0.7f, 1.25f);
            flying._rotationAxis = Random.onUnitSphere;
            return flying;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            _velocity += Vector3.up * gravity * Time.deltaTime;

            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(_rotationAxis, spin * Time.deltaTime, Space.World);

            if (_age >= lifetime || transform.position.y < -6f)
            {
                Destroy(gameObject);
            }
        }
    }
}
