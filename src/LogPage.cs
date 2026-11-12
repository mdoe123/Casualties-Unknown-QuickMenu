using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「日志」页：回显控制台最近输出，命令失败的原因会出现在这里。
    /// 最新的日志排在最上方，无需滚动到底部。
    /// </summary>
    internal sealed class LogPage
    {
        internal static readonly LogPage Instance = new LogPage();

        private const int MaxLines = 200;

        private Vector2 _scroll;

        internal void Draw()
        {
            GUILayout.BeginHorizontal();

            List<string> lines = ConsoleBridge.RecentLogs(MaxLines);

            if (GUILayout.Button("复制全部", QuickMenuUI.Btn, GUILayout.Width(96f), GUILayout.Height(28f)))
            {
                GUIUtility.systemCopyBuffer = string.Join("\n", lines.ToArray());
                QuickMenuState.Notify("已复制控制台日志。");
            }

            if (GUILayout.Button("清空", QuickMenuUI.Btn, GUILayout.Width(72f), GUILayout.Height(28f)))
            {
                ConsoleBridge.ClearLogs();
                QuickMenuState.Notify("已清空控制台日志。");
            }

            if (GUILayout.Button("打开原生控制台", QuickMenuUI.Btn, GUILayout.Width(140f), GUILayout.Height(28f)))
            {
                QuickMenuState.Close();
                ConsoleBridge.OpenNativeConsole();
            }

            GUILayout.Label($"共 {lines.Count} 条（最新在上）", QuickMenuUI.Muted, GUILayout.Height(28f));

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (lines.Count == 0)
            {
                GUILayout.Label("暂无日志。执行任意命令后会在此显示结果。", QuickMenuUI.Muted);
            }

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                GUILayout.Label(lines[i], QuickMenuUI.LogLine);
            }

            GUILayout.EndScrollView();
        }
    }
}
