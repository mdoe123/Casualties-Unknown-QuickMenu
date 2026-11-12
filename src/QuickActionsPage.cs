using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「常用」页：把高频命令做成按钮，避免每次手打参数。
    /// </summary>
    internal sealed class QuickActionsPage
    {
        internal static readonly QuickActionsPage Instance = new QuickActionsPage();

        private static readonly string[] SkillKeys = { "str", "res", "int" };
        private static readonly string[] SkillNames = { "力量 str", "韧性 res", "智力 int" };
        private static readonly string[] RiskyCommands = { "kill", "resetskills", "saveandquit", "nukeplayerprefs" };

        private Vector2 _scroll;

        private float _timescale = 1f;
        private int _skill;
        private string _xpAmount = "100";


        private string _pendingCommand;
        private float _pendingUntil;

        internal void Draw()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawSurvival();
            DrawMovement();
            DrawTime();
            DrawProgression();
            DrawMisc();

            GUILayout.Space(10f);
            GUILayout.Label("提示：物品与肢体已拆分为独立页签。任何作弊命令都会禁用成就，直到重启游戏。",
                QuickMenuUI.Muted);

            GUILayout.EndScrollView();
        }

        private void DrawSurvival()
        {
            QuickMenuUI.Section("生存");

            GUILayout.BeginHorizontal();
            if (QuickMenuUI.WideButton("治疗（heal）"))
            {
                Run("heal", "治疗");
            }

            if (QuickMenuUI.WideButton("止血（coagulate）"))
            {
                Run("coagulate", "止血");
            }

            if (QuickMenuUI.WideButton("击杀（kill）"))
            {
                Run("kill", "击杀");
            }

            if (QuickMenuUI.WideButton("新手包（starterkit）"))
            {
                Run("starterkit", "新手包");
            }

            GUILayout.EndHorizontal();
        }

        private void DrawMovement()
        {
            QuickMenuUI.Section("移动与场景");

            GUILayout.BeginHorizontal();
            if (QuickMenuUI.WideButton("传送到光标（tp）"))
            {
                Run("tp", "传送");
            }

            if (QuickMenuUI.WideButton("下一层（skiplayer）"))
            {
                Run("skiplayer", "下一层");
            }

            if (QuickMenuUI.WideButton("无碰撞（noclip）"))
            {
                Run("noclip", "无碰撞");
            }

            if (QuickMenuUI.WideButton("自由相机（freecam）"))
            {
                Run("freecam", "自由相机");
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTime()
        {
            QuickMenuUI.Section("时间流速");

            GUILayout.BeginHorizontal();
            GUILayout.Label("倍率", QuickMenuUI.Label, GUILayout.Width(40f));
            _timescale = GUILayout.HorizontalSlider(_timescale, 0.25f, 5f,
                QuickMenuUI.SliderTrack, QuickMenuUI.SliderThumb,
                GUILayout.MinWidth(140f), GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            GUILayout.Label($"{_timescale:0.00}×", QuickMenuUI.Label, GUILayout.Width(70f));

            if (QuickMenuUI.WideButton("应用", 90f))
            {
                Run($"timescale {_timescale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}", "时间流速");
            }

            if (QuickMenuUI.WideButton("恢复 1×", 100f))
            {
                _timescale = 1f;
                Run("timescale 1", "时间流速");
            }

            GUILayout.EndHorizontal();
        }

        private void DrawProgression()
        {
            QuickMenuUI.Section("技能经验");

            GUILayout.BeginHorizontal();
            _skill = GUILayout.SelectionGrid(_skill, SkillNames, 3, QuickMenuUI.Btn,
                GUILayout.MinWidth(240f), GUILayout.ExpandWidth(true), GUILayout.Height(30f));

            GUILayout.Label("经验", QuickMenuUI.Label, GUILayout.Width(36f));
            _xpAmount = GUILayout.TextField(_xpAmount, QuickMenuUI.Field, GUILayout.Width(100f), GUILayout.Height(26f));

            if (QuickMenuUI.WideButton("给予", 90f))
            {
                string amount = string.IsNullOrEmpty(_xpAmount) ? "0" : _xpAmount;
                Run($"addxp {SkillKeys[_skill]} {amount}", "技能经验");
            }

            if (QuickMenuUI.WideButton("重置技能", 100f))
            {
                Run("resetskills", "重置技能");
            }

            GUILayout.EndHorizontal();
        }

        private void DrawMisc()
        {
            QuickMenuUI.Section("其他");

            GUILayout.BeginHorizontal();
            if (QuickMenuUI.WideButton("调试全亮（fullbright）"))
            {
                Run("fullbright", "全亮");
            }

            if (QuickMenuUI.WideButton("保存并退出（saveandquit）"))
            {
                Run("saveandquit", "保存并退出");
            }

            if (QuickMenuUI.WideButton("打开原生控制台", 140f))
            {
                QuickMenuState.Close();
                ConsoleBridge.OpenNativeConsole();
            }

            if (QuickMenuUI.WideButton("清空控制台日志", 140f))
            {
                ConsoleBridge.ClearLogs();
                QuickMenuState.Notify("已清空控制台日志。");
            }

            GUILayout.EndHorizontal();
        }

        private void Run(string command, string label)
        {
            if (NeedsConfirm(command))
            {
                QuickMenuState.Notify($"危险操作：3 秒内再次点击「{label}」确认执行 {command}");
                return;
            }

            if (!ConsoleBridge.TryExecute(command, out string error))
            {
                QuickMenuState.Notify("执行失败：" + error);
                return;
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

        /// <summary>破坏性命令要求 3 秒内二次点击，避免误触。</summary>
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
            return true;
        }
    }
}
