using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「收藏」页：把常用命令固定在这里，一键执行。
    /// 需要填参数的命令可跳转到「全部命令」页，那里会自动选中。
    /// </summary>
    internal sealed class FavoritesPage
    {
        internal static readonly FavoritesPage Instance = new FavoritesPage();

        private static readonly string[] RiskyCommands =
        {
            "kill", "resetskills", "saveandquit", "nukeplayerprefs"
        };

        private Vector2 _scroll;
        private string _pendingCommand;
        private float _pendingUntil;

        internal void Draw()
        {
            List<string> names = FavoriteCommands.Snapshot();

            if (names.Count == 0)
            {
                GUILayout.Space(10f);
                GUILayout.Label("还没有收藏任何命令。", QuickMenuUI.Label);
                GUILayout.Label("在「全部命令」页选中一条命令，点「收藏」即可加入这里。", QuickMenuUI.Muted);
                GUILayout.Space(6f);

                if (GUILayout.Button("去全部命令", QuickMenuUI.Btn,
                        GUILayout.Width(130f), GUILayout.Height(30f)))
                {
                    QuickMenuUI.CurrentTab = QuickMenuUI.TabCommands;
                }

                return;
            }

            GUILayout.Label($"共 {names.Count} 条收藏。需要参数的命令请点「参数」跳转填写。",
                QuickMenuUI.Muted);
            GUILayout.Space(4f);

            _scroll = GUILayout.BeginScrollView(_scroll,
                GUILayout.Height(Mathf.Max(160f, QuickMenuUI.BodyHeight - 56f)));

            float descWidth = Mathf.Max(160f, QuickMenuUI.BodyWidth - 486f);
            bool removed = false;

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                Command command = ConsoleScript.SearchExact(name);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("★ " + name, QuickMenuUI.BtnSmall,
                        GUILayout.Width(176f), GUILayout.Height(28f)))
                {
                    JumpTo(name);
                }

                GUILayout.Label(
                    command != null
                        ? QuickMenuUI.Truncate(command.description, 60)
                        : "命令已不存在（可能来自已移除的 Mod）",
                    QuickMenuUI.Muted, GUILayout.Width(descWidth), GUILayout.Height(28f));

                GUI.enabled = command != null;

                if (GUILayout.Button("执行", QuickMenuUI.BtnSmall,
                        GUILayout.Width(62f), GUILayout.Height(28f)))
                {
                    Execute(name);
                }

                if (GUILayout.Button("参数", QuickMenuUI.BtnSmall,
                        GUILayout.Width(62f), GUILayout.Height(28f)))
                {
                    JumpTo(name);
                }

                GUI.enabled = true;

                if (GUILayout.Button("移除", QuickMenuUI.BtnSmall,
                        GUILayout.Width(62f), GUILayout.Height(28f)))
                {
                    FavoriteCommands.Remove(name);
                    QuickMenuState.Notify($"已取消收藏 {name}");
                    removed = true;
                }

                GUILayout.EndHorizontal();

                if (removed)
                {
                    // 集合已被修改，下一帧重新取快照。
                    break;
                }
            }

            GUILayout.EndScrollView();
        }

        private static void JumpTo(string name)
        {
            QuickMenuUI.CurrentTab = QuickMenuUI.TabCommands;
            CommandListPage.Instance.SelectByName(name);
        }

        private void Execute(string command)
        {
            if (NeedsConfirm(command))
            {
                return;
            }

            if (!ConsoleBridge.TryExecute(command, out string error))
            {
                QuickMenuState.Notify("执行失败：" + error);
                return;
            }

            string last = ConsoleBridge.LastLogLine();
            bool failed = !string.IsNullOrEmpty(last)
                          && (last.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                              || last.IndexOf("is not a valid", StringComparison.OrdinalIgnoreCase) >= 0
                              || last.IndexOf("No world", StringComparison.OrdinalIgnoreCase) >= 0);

            QuickMenuState.Notify(failed ? last : $"已执行 {command}");

            if (QuickMenuPlugin.CloseAfterExecute.Value)
            {
                QuickMenuState.Close();
            }
        }

        private bool NeedsConfirm(string command)
        {
            if (!QuickMenuPlugin.ConfirmRisky.Value)
            {
                return false;
            }

            bool risky = false;
            foreach (string name in RiskyCommands)
            {
                if (command == name || command.StartsWith(name + " ", StringComparison.Ordinal))
                {
                    risky = true;
                    break;
                }
            }

            if (!risky)
            {
                return false;
            }

            if (_pendingCommand == command && Time.realtimeSinceStartup <= _pendingUntil)
            {
                _pendingCommand = null;
                return false;
            }

            _pendingCommand = command;
            _pendingUntil = Time.realtimeSinceStartup + 3f;
            QuickMenuState.Notify($"危险操作：3 秒内再次点击「执行」确认 {command}");
            return true;
        }
    }
}
