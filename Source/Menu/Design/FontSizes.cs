﻿namespace wowmod_cs2.MenuSystem
{
    internal static class FontSizes
    {
        public const int Title = 22; // "Warcraft"
        public const int Sub   = 15; // было 16
        public const int Item  = 14; // было 15
        public const int Hint  = 8;  // было 9/10

        public static string Size(int px, string text)  => $"<font size='{px}'>{text}</font>";
        public static string Color(string hex, string t) => $"<font color='{hex}'>{t}</font>";
        public static string B(string text)             => $"<b>{text}</b>";
    }
}