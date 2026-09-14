using System.Collections.Generic;
using UnityEngine;

namespace ButchersGames
{
    /// Набор уровней: список префабов уровней и флаг рандомизации.
    [CreateAssetMenu(menuName = "Data/Lvls List")]
    public class LevelsList : ScriptableObject
    {
        /// Включить случайный выбор уровней.
        public bool randomizedLvls;

        /// Префабы уровней.
        public List<Level> lvls;
    }
}