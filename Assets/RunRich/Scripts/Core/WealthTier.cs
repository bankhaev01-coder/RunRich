using UnityEngine;

namespace RunRich
{
    // Стадия богатства бегуна. Подписи - как в референсе (русская локализация).
    public enum WealthTier
    {
        Poor = 0,
        Middle = 1,
        Rich = 2,
        Millionaire = 3
    }

    // Всё, что меняется при переходе на новую стадию богатства.
    public sealed class TierInfo
    {
        // Подпись над шкалой богатства.
        public string Label;
        // Сколько денег нужно, чтобы войти в стадию.
        public int Threshold;
        // Цвет заливки шкалы на стадии.
        public Color GaugeColor;
        // Цвет верха.
        public Color TopColor;
        // Цвет низа.
        public Color BottomColor;
        // Цвет обуви.
        public Color ShoesColor;
        // Цвет волос.
        public Color HairColor;
        // Дополнительный акцент (золото, сумки, ...).
        public Color AccentColor;
        // Правда на последней стадии: блестящее золото вместо плоского цвета.
        public bool Golden;

        public TierInfo(string label, int threshold, Color gauge, Color top, Color bottom, Color shoes,
            Color hair, Color accent, bool golden = false)
        {
            Label = label;
            Threshold = threshold;
            GaugeColor = gauge;
            TopColor = top;
            BottomColor = bottom;
            ShoesColor = shoes;
            HairColor = hair;
            AccentColor = accent;
            Golden = golden;
        }
    }

    // Статичная таблица четырёх стадий богатства, используемых клоном.
    public static class WealthStages
    {
        public static readonly TierInfo[] All =
        {
            // 0 - БЕДНЫЙ: повседневный верх, джинсовые шорты, сланцы (начало референсного ролика)
            new TierInfo("БЕДНЫЙ", 0,
                gauge: Hex("F07A1E"),
                top: Hex("35B8AC"), bottom: Hex("5A82C8"), shoes: Hex("A44FD1"),
                hair: Hex("8A5A32"), accent: Hex("C9D2DA")),

            // 1 - СОСТОЯТЕЛЬН: зелёное платье + розовые каблуки
            new TierInfo("СОСТОЯТЕЛЬН", 80,
                gauge: Hex("F2C41E"),
                top: Hex("35C24A"), bottom: Hex("35C24A"), shoes: Hex("E050A0"),
                hair: Hex("8A5A32"), accent: Hex("F2C41E")),

            // 2 - БОГАТЫЙ: оранжевая шуба, джинсы, коричневые ботинки
            new TierInfo("БОГАТЫЙ", 240,
                gauge: Hex("F2A03C"),
                top: Hex("F07A1E"), bottom: Hex("4A74B0"), shoes: Hex("7A4C28"),
                hair: Hex("5A3A1E"), accent: Hex("F2C41E")),

            // 3 - МИЛЛИОНЕР: золотой костюм
            new TierInfo("МИЛЛИОНЕР", 600,
                gauge: Hex("F2C41E"),
                top: Hex("F2C41E"), bottom: Hex("F2E08A"), shoes: Hex("C79A0E"),
                hair: Hex("F2E08A"), accent: Hex("FFF3B0"), golden: true)
        };

        public static int Count => All.Length;

        public static TierInfo Get(int index) => All[Mathf.Clamp(index, 0, All.Length - 1)];

        // Индекс стадии для заданной суммы денег.
        public static int IndexFor(int money)
        {
            int result = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (money >= All[i].Threshold) result = i;
            }
            return result;
        }

        // Заливка шкалы 0..1 внутри текущей стадии (белая полоса референсного HUD).
        public static float StageProgress(int money)
        {
            int index = IndexFor(money);
            int from = All[index].Threshold;
            int to = index + 1 < All.Length ? All[index + 1].Threshold : from + 1;
            if (to <= from) return 1f;
            return Mathf.Clamp01((money - from) / (float)(to - from));
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
