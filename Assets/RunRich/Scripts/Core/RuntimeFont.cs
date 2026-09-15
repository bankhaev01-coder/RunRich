using UnityEngine;

namespace RunRich
{
    // Хелпер, отдающий рабочий шрифт в рантайме (легаси UI-текст).
    public static class RuntimeFont
    {
        private static Font _default;

        public static Font Default
        {
            get
            {
                if (_default != null) return _default;

                _default = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_default == null) _default = Font.CreateDynamicFontFromOSFont("Arial", 40);
                return _default;
            }
        }
    }
}
