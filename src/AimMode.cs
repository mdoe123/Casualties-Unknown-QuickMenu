using System;
using System.Globalization;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 点选模式：关掉菜单，让玩家在世界里点一下，用该点执行待定的命令。
    ///
    /// 解决的是「点按钮时光标在按钮上，取不到目标位置」的问题——
    /// 玩家需要在菜单打开之后再选位置时，走这条路径。
    /// </summary>
    internal static class AimMode
    {
        private static string _template;
        private static string _label;
        private static int _returnTab;

        private static GUIStyle _hint;

        internal static bool Active { get; private set; }

        /// <summary>template 里用 {0} 占位位置参数，例如 "spawn bandage {0} 1 3"。</summary>
        internal static bool Begin(string template, string label)
        {
            if (PlayerCamera.main == null)
            {
                QuickMenuState.Notify("进入对局后才能点选位置。");
                return false;
            }

            _template = template;
            _label = label;
            _returnTab = QuickMenuUI.CurrentTab;
            Active = true;

            // 关掉菜单，把画面交还给玩家去点。
            QuickMenuState.Close();
            return true;
        }

        internal static void Cancel()
        {
            if (!Active)
            {
                return;
            }

            Active = false;
            QuickMenuState.Notify("已取消点选位置。");
        }

        internal static void Tick()
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (!TryWorldPoint(out Vector2 world))
            {
                Cancel();
                return;
            }

            Confirm(world);
        }

        private static void Confirm(Vector2 world)
        {
            Active = false;

            string position = string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", world.x, world.y);
            string command = string.Format(CultureInfo.InvariantCulture, _template, position);

            if (!ConsoleBridge.TryExecute(command, out string error))
            {
                QuickMenuState.Notify("执行失败：" + error);
                return;
            }

            QuickMenuState.Notify($"{_label} 已定位到 {position}");

            QuickMenuUI.CurrentTab = _returnTab;
            QuickMenuState.Open();
        }

        internal static bool TryWorldPoint(out Vector2 world)
        {
            world = Vector2.zero;

            try
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    return false;
                }

                Vector3 point = camera.ScreenToWorldPoint(Input.mousePosition);
                world = new Vector2(point.x, point.y);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void DrawHint()
        {
            if (_hint == null)
            {
                _hint = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                    richText = false
                };
                _hint.normal.textColor = Color.white;
            }

            string text = $"{_label}：左键选择位置 · 右键 / Esc 取消";
            Vector2 size = _hint.CalcSize(new GUIContent(text));
            var rect = new Rect(
                (Screen.width - size.x) * 0.5f - 18f,
                Screen.height - 132f,
                size.x + 36f,
                size.y + 18f);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.66f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;

            GUI.Label(rect, text, _hint);
        }
    }
}
