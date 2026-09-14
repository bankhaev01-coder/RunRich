using System.Collections.Generic;
using UnityEngine;

namespace ButchersGames
{
    /// Уровень: ссылка на точку спауна игрока (в префабе уровня).
    public class Level : MonoBehaviour
    {
        /// Точка, в которой появляется игрок при загрузке уровня.
        [SerializeField] private Transform playerSpawnPoint;

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.magenta;
                var originalMatrix = Gizmos.matrix;
                Gizmos.matrix = playerSpawnPoint.localToWorldMatrix;

                // Маленький шар в точке спавна.
                Gizmos.DrawSphere(Vector3.up * 0.5f + Vector3.forward, 0.5f);
                // Куб рядом для наглядности.
                Gizmos.DrawCube(Vector3.up * 0.5f, Vector3.one);

                Gizmos.matrix = originalMatrix;
            }
        }
        #endif
    }
}