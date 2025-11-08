namespace wowmod_cs2.MenuSystem
{
    internal static class Theme
    {
        // Палитра 
        public const string TitleColor     = "#A98CFF"; // "Warcraft"
        public const string NormalColor    = "#FFFFFF";
        public const string DisabledColor  = "#9A9A9A";
        public const string SelectedColor  = "#9DED80"; // для "Главное меню"

        public const string AccentRed      = "#FF6A6A"; // стрелки
        public const string HintLabelColor = "#FF6A6A"; // Навиг./Выбр./Вых.
        public const string HintKeyColor   = "#FFD479"; // w↑ s↓ E R

        // Стрелки 
        public const string ArrowLeftIn  = "►";
        public const string ArrowRightIn = "◄";

        // Фиксированная высота строк меню
        public const int LineHeightPx = 18;

        public static string Colorize(string hex, string s) => $"<font color='{hex}'>{s}</font>";
        public static string Key(string s)   => Colorize(HintKeyColor, s);
        public static string Label(string s) => Colorize(HintLabelColor, s);

        public static string BuildHintLineRu()
        {
            var t = Label("Навиг.:") + "&nbsp;" + Key("w↑") + "&nbsp;" + Key("s↓")
                  + "&nbsp;&#124;&nbsp;"
                  + Label("Выбр.:")  + "&nbsp;" + Key("E")
                  + "&nbsp;&#124;&nbsp;"
                  + Label("Вых.:")   + "&nbsp;" + Key("R");
            return $"<span style='white-space:nowrap'>{FontSizes.Size(FontSizes.Hint, t)}</span>";
        }
    }
}