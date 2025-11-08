using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Core;

namespace wowmod_cs2.MenuSystem
{
    internal class MenuPlayer
    {
        internal CCSPlayerController player { get; set; } = null!;
        internal Menu? MainMenu = null;
        internal LinkedListNode<MenuOption>? CurrentChoice = null;
        internal LinkedListNode<MenuOption>? MenuStart = null;
        internal int VisibleOptions = 5;

        internal string CenterHtml = "";

        internal void OpenMainMenu(Menu? menu, int selectedIndex = 0)
        {
            if (menu == null) { MainMenu = null; CurrentChoice = null; MenuStart = null; CenterHtml = ""; return; }

            MainMenu = menu;
            VisibleOptions = menu.ResultsBeforePaging;

            CurrentChoice = MainMenu.Options?.First;
            MenuStart = CurrentChoice;

            if (selectedIndex > 0 && MainMenu.Options is not null)
            {
                var node = MainMenu.Options.First;
                while (node != null)
                {
                    if (node.Value.Index == selectedIndex) { CurrentChoice = node; MenuStart = node; break; }
                    node = node.Next;
                }
            }
            UpdateCenterHtml();
        }

        internal void OpenSubMenu(Menu menu)
        {
            if (menu == null) return;
            menu.Prev = CurrentChoice;
            VisibleOptions = menu.ResultsBeforePaging;

            CurrentChoice = menu.Options?.First;
            MenuStart = CurrentChoice;

            var cc = CurrentChoice;
            if (cc != null) cc.Value.OnSelect?.Invoke(player, cc.Value);

            UpdateCenterHtml();
        }

        internal void CloseAllSubMenus()
        {
            if (MainMenu == null) return;
            CurrentChoice = MainMenu.Options?.First;
            MenuStart     = CurrentChoice;
            UpdateCenterHtml();
        }

        internal void GoBackToPrev(LinkedListNode<MenuOption>? prev)
        {
            if (prev == null) { OpenMainMenu(null); return; }

            VisibleOptions = prev.Value.Parent != null ? prev.Value.Parent.ResultsBeforePaging : 5;
            CurrentChoice  = prev;

            var cc = CurrentChoice;
            if (cc != null)
            {
                if (cc.Value.Index >= VisibleOptions)
                {
                    var ms = cc; for (int i = 1; i < VisibleOptions && ms.Previous != null; i++) ms = ms.Previous;
                    MenuStart = ms;
                }
                else MenuStart = cc.List?.First;

                cc.Value.OnSelect?.Invoke(player, cc.Value);
            }
            UpdateCenterHtml();
        }

        internal void ScrollDown()
        {
            if (CurrentChoice == null || MainMenu == null) return;
            var list = CurrentChoice.List; if (list == null || list.First == null) return;

            CurrentChoice = CurrentChoice.Next ?? list.First;

            var cc = CurrentChoice;
            if (cc != null)
            {
                MenuStart = (cc.Value.Index >= VisibleOptions) ? (MenuStart?.Next ?? list.First) : list.First;
                cc.Value.OnSelect?.Invoke(player, cc.Value);
            }
            UpdateCenterHtml();
        }

        internal void ScrollUp()
        {
            if (CurrentChoice == null || MainMenu == null) return;
            var list = CurrentChoice.List; if (list == null || list.First == null) return;

            CurrentChoice = CurrentChoice.Previous ?? list.Last;

            var cc = CurrentChoice;
            if (cc != null)
            {
                if (cc == list.Last && cc.Value.Index >= VisibleOptions)
                {
                    var ms = cc; for (int i = 1; i < VisibleOptions && ms.Previous != null; i++) ms = ms.Previous;
                    MenuStart = ms;
                }
                else MenuStart = list.First;

                cc.Value.OnSelect?.Invoke(player, cc.Value);
            }
            UpdateCenterHtml();
        }

        internal void Choose()
        {
            var cc = CurrentChoice; if (cc == null) return;
            if (cc.Value.Enabled) cc.Value.OnChoose?.Invoke(player, cc.Value);
        }

        // ===== helpers =====
        private static string StripTags(string s) => Regex.Replace(s, "<.*?>", string.Empty);

        private static string NoWrap(string s)
        {
            s = StripTags(s);
            s = s.Replace("-", "&#8209;"); // non-breaking hyphen
            s = s.Replace("/", "&#47;");
            s = s.Replace(" ", "&nbsp;");
            return s;
        }

        void UpdateCenterHtml()
        {
            var cc = CurrentChoice; if (cc == null) { CenterHtml = ""; return; }
            var start = MenuStart ?? cc; if (start == null) { CenterHtml = ""; return; }

            var sb = new StringBuilder();

            // Заголовки
            var titleMain = FontSizes.B(FontSizes.Color(Theme.TitleColor, FontSizes.Size(FontSizes.Title, NoWrap("Warcraft"))));
            var titleSub = FontSizes.Color(Theme.SelectedColor, FontSizes.Size(FontSizes.Sub, NoWrap("Главное меню")));
            sb.Append(titleMain).Append("<br>");
            sb.Append(titleSub).Append("<br>");

            // Пункты + описание под выделенной строкой
            int shown = 0; var node = start;
            while (node != null && shown < VisibleOptions)
            {
                var v = node.Value;
                bool isSelected = node == cc;

                string leftArrow = isSelected ? $"<font color='{Theme.AccentRed}' size='{FontSizes.Item}'>►</font>" : "&nbsp;";
                string rightArrow = isSelected ? $"<font color='{Theme.AccentRed}' size='{FontSizes.Item}'>◄</font>" : "&nbsp;";

                string txt = NoWrap(v.OptionDisplay ?? string.Empty);
                string label = FontSizes.Color(v.Enabled ? Theme.NormalColor : Theme.DisabledColor,
                                               FontSizes.Size(FontSizes.Item, txt));

                sb.Append(leftArrow).Append("&nbsp;").Append(label).Append("&nbsp;").Append(rightArrow).Append("<br>");

                // --- описание выбранного пункта (очень мелким серым) ---
                if (isSelected)
                {
                    var rawDesc = v.SubOptionDisplay ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(rawDesc))
                    {
                        rawDesc = Regex.Replace(rawDesc, @"\r?\n", "<br>");
                        const int descPx = 36; // ещё меньше, чем Hint
                        var descLine = FontSizes.Color(Theme.DisabledColor, FontSizes.Size(descPx, rawDesc));
                        sb.Append(descLine).Append("<br>");
                    }
                }
                // -------------------------------------------------------

                node = node.Next; shown++;
            }

            // Подсказка
            sb.Append(Theme.BuildHintLineRu());

            CenterHtml = sb.ToString();
        }
    }
}