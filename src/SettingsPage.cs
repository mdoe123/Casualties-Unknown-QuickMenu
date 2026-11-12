using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「设置」页：把常用配置项搬到界面里，避免每次都去改 cfg 文件。
    /// 滑杆类修改在关闭菜单时统一落盘，开关与热键立即保存。
    /// </summary>
    internal sealed class SettingsPage
    {
        internal static readonly SettingsPage Instance = new SettingsPage();

        private const float LabelWidth = 260f;

        private Vector2 _scroll;
        private bool _capturing;

        internal void Draw()
        {
            if (_capturing)
            {
                HandleCapture();
            }

            _scroll = GUILayout.BeginScrollView(_scroll,
                GUILayout.Height(Mathf.Max(160f, QuickMenuUI.BodyHeight - 8f)));

            QuickMenuUI.Section("快捷键");
            DrawHotkeyRow();

            QuickMenuUI.Section("界面");
            DrawSliderRow("界面缩放", QuickMenuPlugin.UiScale, 0.6f, 2f);
            DrawSliderRow("面板不透明度", QuickMenuPlugin.Opacity, 0.3f, 1f);
            DrawToggleRow("压暗背景画面", QuickMenuPlugin.DimBackground);

            GUILayout.BeginHorizontal();
            GUILayout.Label("窗口布局", QuickMenuUI.Label,
                GUILayout.Width(LabelWidth), GUILayout.Height(26f));

            if (GUILayout.Button("恢复默认位置与大小", QuickMenuUI.Btn,
                    GUILayout.Width(190f), GUILayout.Height(26f)))
            {
                QuickMenuUI.ResetLayout();
                QuickMenuState.Notify("窗口已恢复默认位置与大小。");
            }

            GUILayout.Label("也可以直接拖动标题栏与右下角手柄调整。", QuickMenuUI.Muted,
                GUILayout.Height(26f));
            GUILayout.EndHorizontal();

            QuickMenuUI.Section("行为");
            DrawToggleRow("菜单打开时屏蔽角色操作", QuickMenuPlugin.BlockGameInput);
            DrawToggleRow("阻止原生控制台自动弹出", QuickMenuPlugin.SuppressNativePopup);
            DrawToggleRow("执行命令后自动关闭菜单", QuickMenuPlugin.CloseAfterExecute);
            DrawToggleRow("危险命令二次确认", QuickMenuPlugin.ConfirmRisky);

            QuickMenuUI.Section("收藏");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"已收藏 {FavoriteCommands.Count} 条命令", QuickMenuUI.Label,
                GUILayout.Width(LabelWidth), GUILayout.Height(26f));

            GUI.enabled = FavoriteCommands.Count > 0;
            if (GUILayout.Button("清空收藏", QuickMenuUI.Btn,
                    GUILayout.Width(110f), GUILayout.Height(26f)))
            {
                FavoriteCommands.Clear();
                QuickMenuState.Notify("已清空收藏。");
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            QuickMenuUI.Section("关于");

            GUILayout.Label($"Casualties Quick Menu {QuickMenuPlugin.PluginVersion}",
                QuickMenuUI.Label, GUILayout.Height(24f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("GitHub 仓库", QuickMenuUI.Label,
                GUILayout.Width(LabelWidth), GUILayout.Height(26f));

            if (GUILayout.Button("复制链接", QuickMenuUI.Btn,
                    GUILayout.Width(100f), GUILayout.Height(26f)))
            {
                GUIUtility.systemCopyBuffer = "https://github.com/mdoe123/Casualties-Unknown-QuickMenu";
                QuickMenuState.Notify("仓库链接已复制到剪贴板。");
            }

            GUILayout.Label("mdoe123/Casualties-Unknown-QuickMenu",
                QuickMenuUI.Muted, GUILayout.Height(26f));
            GUILayout.EndHorizontal();

            GUILayout.Label("配置文件：BepInEx/config/local.casualties.quickmenu.cfg",
                QuickMenuUI.Muted, GUILayout.Height(22f));

            GUILayout.EndScrollView();
        }

        private void DrawHotkeyRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("菜单热键", QuickMenuUI.Label,
                GUILayout.Width(LabelWidth), GUILayout.Height(26f));

            Color prev = GUI.backgroundColor;
            if (_capturing)
            {
                GUI.backgroundColor = new Color(0.55f, 0.45f, 0.20f);
            }

            string label = _capturing ? "请按键…" : QuickMenuPlugin.Hotkey.Value.ToString();
            bool clicked = GUILayout.Button(label, QuickMenuUI.Btn,
                GUILayout.Width(150f), GUILayout.Height(26f));

            GUI.backgroundColor = prev;

            if (clicked)
            {
                _capturing = !_capturing;
                QuickMenuPlugin.CapturingKey = _capturing;
            }

            GUILayout.Label(_capturing ? "按 Esc 取消" : "点击后按下新按键", QuickMenuUI.Muted,
                GUILayout.Height(26f));
            GUILayout.EndHorizontal();
        }

        private void HandleCapture()
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None)
            {
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                QuickMenuState.Notify("已取消按键设置。");
            }
            else
            {
                QuickMenuPlugin.Hotkey.Value = e.keyCode;
                QuickMenuPlugin.Instance?.Config.Save();
                QuickMenuState.Notify($"热键已设为 {e.keyCode}");
            }

            _capturing = false;
            QuickMenuPlugin.CapturingKey = false;
            e.Use();
        }

        private static void DrawToggleRow(string label, ConfigEntry<bool> entry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, QuickMenuUI.Label,
                GUILayout.Width(LabelWidth), GUILayout.Height(26f));

            Color prev = GUI.backgroundColor;
            if (entry.Value)
            {
                GUI.backgroundColor = new Color(0.30f, 0.50f, 0.80f);
            }

            bool clicked = GUILayout.Button(entry.Value ? "开" : "关", QuickMenuUI.Btn,
                GUILayout.Width(64f), GUILayout.Height(26f));

            GUI.backgroundColor = prev;

            if (clicked)
            {
                entry.Value = !entry.Value;
                QuickMenuPlugin.Instance?.Config.Save();
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawSliderRow(string label, ConfigEntry<float> entry, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, QuickMenuUI.Label,
                GUILayout.Width(LabelWidth), GUILayout.Height(26f));

            float next = GUILayout.HorizontalSlider(entry.Value, min, max,
                QuickMenuUI.SliderTrack, QuickMenuUI.SliderThumb,
                GUILayout.Width(300f), GUILayout.Height(22f));

            if (!Mathf.Approximately(next, entry.Value))
            {
                entry.Value = next;
            }

            GUILayout.Label(next.ToString("0.00"), QuickMenuUI.Label,
                GUILayout.Width(56f), GUILayout.Height(26f));
            GUILayout.EndHorizontal();
        }
    }
}
