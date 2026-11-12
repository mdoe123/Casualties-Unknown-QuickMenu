using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「全部命令」页：直接读取 ConsoleScript.Commands，因此游戏新增命令会自动出现，
    /// 不需要在 Mod 里维护一份命令清单。
    /// </summary>
    internal sealed class CommandListPage
    {
        internal static readonly CommandListPage Instance = new CommandListPage();

        private readonly List<Command> _filtered = new List<Command>();
        private readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _recent = new List<string>();

        private string _search = string.Empty;
        private string _filterKey;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _fillScroll;

        private Command _selected;
        private string[] _args = Array.Empty<string>();

        internal void LoadFavorites()
        {
            _favorites.Clear();

            string raw = QuickMenuPlugin.Favorites?.Value;
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (string part in raw.Split(','))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    _favorites.Add(part.Trim());
                }
            }
        }

        private void SaveFavorites()
        {
            QuickMenuPlugin.Favorites.Value = string.Join(",", _favorites);
        }

        internal void OnMenuOpened()
        {
            // 强制重建过滤列表：控制台可能刚刚注册完命令。
            _filterKey = null;
        }

        internal void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索", QuickMenuUI.Label, GUILayout.Width(40f), GUILayout.Height(28f));

            string next = GUILayout.TextField(_search, QuickMenuUI.Field, GUILayout.Height(28f));
            if (next != _search)
            {
                _search = next;
                _filterKey = null;
            }

            if (GUILayout.Button("清空", QuickMenuUI.Btn, GUILayout.Width(64f), GUILayout.Height(28f)))
            {
                _search = string.Empty;
                _filterKey = null;
            }

            GUILayout.Label($"共 {ConsoleBridge.Commands.Count} 条", QuickMenuUI.Muted, GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            EnsureFiltered();

            GUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            GUILayout.EndHorizontal();

            DrawRecent();
        }

        private void EnsureFiltered()
        {
            string key = _search + "|" + ConsoleBridge.Commands.Count;
            if (key == _filterKey)
            {
                return;
            }

            _filterKey = key;
            _filtered.Clear();

            foreach (Command command in ConsoleBridge.Commands)
            {
                if (string.IsNullOrEmpty(_search) || Matches(command, _search))
                {
                    _filtered.Add(command);
                }
            }

            _filtered.Sort(CompareCommands);

            // 首次进入或过滤后没有选中项时，自动选中第一条，避免右侧详情长期空白。
            if (_selected == null && _filtered.Count > 0)
            {
                Select(_filtered[0]);
            }
        }

        private static bool Matches(Command command, string query)
        {
            if (command.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrEmpty(command.description)
                   && command.description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int CompareCommands(Command a, Command b)
        {
            bool fa = _favorites.Contains(a.name);
            bool fb = _favorites.Contains(b.name);

            if (fa != fb)
            {
                return fa ? -1 : 1;
            }

            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        private void DrawList()
        {
            GUILayout.BeginVertical(GUILayout.Width(QuickMenuUI.ListWidth));

            _listScroll = GUILayout.BeginScrollView(_listScroll,
                GUILayout.Height(Mathf.Max(160f, QuickMenuUI.BodyHeight - 96f)));

            foreach (Command command in _filtered)
            {
                bool selected = ReferenceEquals(command, _selected);

                GUILayout.BeginHorizontal();

                Color prev = GUI.backgroundColor;
                if (selected)
                {
                    GUI.backgroundColor = new Color(0.30f, 0.50f, 0.80f);
                }

                string label = (_favorites.Contains(command.name) ? "★ " : string.Empty) + command.name;
                if (GUILayout.Button(label, QuickMenuUI.BtnSmall,
                        GUILayout.Width(198f), GUILayout.Height(26f)))
                {
                    Select(command);
                }

                GUI.backgroundColor = prev;

                GUILayout.Label(QuickMenuUI.Truncate(command.description, 30),
                    QuickMenuUI.Muted, GUILayout.Width(240f), GUILayout.Height(26f));

                GUILayout.EndHorizontal();
            }

            if (_filtered.Count == 0)
            {
                GUILayout.Label("没有匹配的命令。", QuickMenuUI.Muted);
            }

            GUILayout.EndScrollView();

            GUILayout.Label($"显示 {_filtered.Count} 条 · ★ 为收藏", QuickMenuUI.Muted);
            GUILayout.EndVertical();
        }

        private void DrawDetail()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            if (_selected == null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("从左侧选择一条命令。", QuickMenuUI.Muted);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(_selected.name, QuickMenuUI.SectionTitle);
            GUILayout.Label(_selected.description, QuickMenuUI.Label);

            GUILayout.Space(4f);

            _detailScroll = GUILayout.BeginScrollView(_detailScroll,
                GUILayout.Height(Mathf.Max(120f, QuickMenuUI.BodyHeight - 260f)));

            var argDescriptions = _selected.argDescription;
            if (argDescriptions.Length == 0)
            {
                GUILayout.Label("此命令不需要参数。", QuickMenuUI.Muted);
            }

            for (int i = 0; i < argDescriptions.Length; i++)
            {
                if (i >= _args.Length)
                {
                    break;
                }

                GUILayout.Label(argDescriptions[i].shortDesc, QuickMenuUI.Muted);

                GUI.SetNextControlName("qm_arg_" + i);
                _args[i] = GUILayout.TextField(_args[i] ?? string.Empty, QuickMenuUI.Field,
                    GUILayout.Height(26f));

                DrawAutofill(i);

                if (!string.IsNullOrEmpty(argDescriptions[i].longDesc)
                    && argDescriptions[i].longDesc != argDescriptions[i].shortDesc)
                {
                    GUILayout.Label(argDescriptions[i].longDesc, QuickMenuUI.Muted);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Label("将执行：" + BuildCommandText(), QuickMenuUI.Muted);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("执行", QuickMenuUI.Btn, GUILayout.Height(34f)))
            {
                ExecuteSelected();
            }

            if (GUILayout.Button(_favorites.Contains(_selected.name) ? "取消收藏" : "收藏",
                    QuickMenuUI.Btn, GUILayout.Width(110f), GUILayout.Height(34f)))
            {
                ToggleFavorite(_selected.name);
            }

            if (GUILayout.Button("复制", QuickMenuUI.Btn, GUILayout.Width(80f), GUILayout.Height(34f)))
            {
                GUIUtility.systemCopyBuffer = BuildCommandText();
                QuickMenuState.Notify("已复制命令到剪贴板。");
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawAutofill(int index)
        {
            if (_selected.argAutofill == null)
            {
                return;
            }

            if (!_selected.argAutofill.TryGetValue(index, out var options) || options == null || options.Count == 0)
            {
                return;
            }

            // 仅当该输入框拥有焦点时展开补全，避免界面被长列表占满。
            if (GUI.GetNameOfFocusedControl() != "qm_arg_" + index)
            {
                return;
            }

            string query = _args[index] ?? string.Empty;
            int shown = 0;

            _fillScroll = GUILayout.BeginScrollView(_fillScroll, GUILayout.Height(92f));
            GUILayout.BeginVertical();

            foreach (string option in options)
            {
                if (!string.IsNullOrEmpty(query)
                    && option.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (GUILayout.Button(option, QuickMenuUI.BtnSmall, GUILayout.Height(22f)))
                {
                    _args[index] = option;
                    GUI.FocusControl(null);
                }

                if (++shown >= 40)
                {
                    break;
                }
            }

            if (shown == 0)
            {
                GUILayout.Label("无匹配补全项。", QuickMenuUI.Muted);
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawRecent()
        {
            if (_recent.Count == 0)
            {
                return;
            }

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("最近：", QuickMenuUI.Muted, GUILayout.Width(46f));

            for (int i = _recent.Count - 1; i >= 0 && i >= _recent.Count - 5; i--)
            {
                string command = _recent[i];
                if (GUILayout.Button(QuickMenuUI.Truncate(command, 26), QuickMenuUI.BtnSmall,
                        GUILayout.Height(24f)))
                {
                    Execute(command);
                }
            }

            GUILayout.EndHorizontal();
        }

        private void Select(Command command)
        {
            _selected = command;
            _args = new string[command.argDescription?.Length ?? 0];
            _detailScroll = Vector2.zero;
            _fillScroll = Vector2.zero;
        }

        private string BuildCommandText()
        {
            if (_selected == null)
            {
                return string.Empty;
            }

            string text = _selected.name;
            for (int i = 0; i < _args.Length; i++)
            {
                string value = (_args[i] ?? string.Empty).Trim();
                if (value.Length > 0)
                {
                    text += " " + value;
                }
            }

            return text;
        }

        private void ExecuteSelected()
        {
            if (_selected == null)
            {
                return;
            }

            Execute(BuildCommandText());
        }

        private void Execute(string command)
        {
            if (!ConsoleBridge.TryExecute(command, out string error))
            {
                QuickMenuState.Notify("执行失败：" + error);
                return;
            }

            _recent.Add(command);
            if (_recent.Count > 20)
            {
                _recent.RemoveAt(0);
            }

            string last = ConsoleBridge.LastLogLine();
            if (!string.IsNullOrEmpty(last) && last.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                QuickMenuState.Notify(last);
            }
            else
            {
                QuickMenuState.Notify("已执行：" + command);
            }

            if (QuickMenuPlugin.CloseAfterExecute.Value)
            {
                QuickMenuState.Close();
            }
        }

        private void ToggleFavorite(string name)
        {
            if (!_favorites.Add(name))
            {
                _favorites.Remove(name);
            }

            SaveFavorites();
            _filterKey = null;
        }
    }
}
