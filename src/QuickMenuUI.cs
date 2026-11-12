using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 窗口框架：位置、尺寸、不透明度与共享样式。
    /// 只负责绘制与窗口交互，不修改游戏状态。
    /// 采用深色半透明面板 + 高对比浅色文字，便于在游戏画面上叠加。
    /// </summary>
    internal static class QuickMenuUI
    {
        internal const float DesignHeight = 1080f;
        internal const float MinWidth = 900f;
        internal const float MinHeight = 560f;
        internal const float HeaderHeight = 62f;

        private static readonly string[] TabNames = { "常用", "物品", "肢体", "全部命令", "日志" };

        private static int _tab;

        /// <summary>窗口矩形，单位是 1920×1080 设计空间，随分辨率等比缩放。</summary>
        internal static Rect WindowRect = new Rect(0f, 0f, 1020f, 690f);

        private static bool _layoutReady;
        private static bool _dragging;
        private static bool _resizing;
        private static Vector2 _dragOffset;
        private static Vector2 _resizeStart;
        private static Vector2 _resizeOrigin;

        private static bool _initialized;
        private static GUIStyle _footerRight;

        internal static Font Cjk { get; private set; }
        internal static GUIStyle Title { get; private set; }
        internal static GUIStyle TabOn { get; private set; }
        internal static GUIStyle TabOff { get; private set; }
        internal static GUIStyle Btn { get; private set; }
        internal static GUIStyle BtnSmall { get; private set; }
        internal static GUIStyle Label { get; private set; }
        internal static GUIStyle Muted { get; private set; }
        internal static GUIStyle Field { get; private set; }
        internal static GUIStyle SectionTitle { get; private set; }
        internal static GUIStyle Footer { get; private set; }
        internal static GUIStyle LogLine { get; private set; }
        internal static GUIStyle CellLabel { get; private set; }
        internal static GUIStyle CellIconFallback { get; private set; }
        internal static GUIStyle CardTitle { get; private set; }
        internal static GUIStyle CardSmall { get; private set; }
        internal static GUIStyle CardWarn { get; private set; }
        internal static GUIStyle CardValue { get; private set; }

        private static readonly Color TextColor = Color.white;
        private static readonly Color MutedColor = new Color(0.62f, 0.67f, 0.76f);
        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.10f);
        private static readonly Color HeaderColor = new Color(0.13f, 0.15f, 0.19f);
        private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.14f);

        internal static float Scale
        {
            get
            {
                float user = QuickMenuPlugin.UiScale != null ? QuickMenuPlugin.UiScale.Value : 1f;
                float s = Screen.height / DesignHeight * Mathf.Max(0.4f, user);
                return Mathf.Max(0.4f, s);
            }
        }

        internal static float BodyWidth
        {
            get { return WindowRect.width - 44f; }
        }

        internal static float BodyHeight
        {
            get { return WindowRect.height - 126f; }
        }

        internal static float ListWidth
        {
            get { return Mathf.Clamp(WindowRect.width * 0.45f, 300f, 470f); }
        }

        internal static int CurrentTab
        {
            get { return _tab; }
            set { _tab = value; }
        }

        internal static void Draw()
        {
            EnsureResources();

            float scale = Scale;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float vw = Screen.width / scale;
            float vh = Screen.height / scale;

            EnsureLayout(vw, vh);
            HandleWindowInput(vw, vh);

            Rect win = WindowRect;

            // IMGUI 的全局色是跨插件共享的，进入绘制前统一复位，
            // 否则其它 Mod（或游戏自身）残留的 backgroundColor 会把按钮底色压暗。
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;

            DrawBackdrop(vw, vh);
            DrawChrome(win);
            DrawHeader(win);
            DrawBody(win);
            DrawFooter(win);
            DrawGrip(win);

            GUI.matrix = previous;
        }

        // ---------- 窗口交互 ----------

        private static void EnsureLayout(float vw, float vh)
        {
            if (!_layoutReady)
            {
                _layoutReady = true;

                float w = Mathf.Clamp(QuickMenuPlugin.WindowWidth.Value, MinWidth, 2400f);
                float h = Mathf.Clamp(QuickMenuPlugin.WindowHeight.Value, MinHeight, 1500f);
                float x = QuickMenuPlugin.WindowX.Value;
                float y = QuickMenuPlugin.WindowY.Value;

                if (x < 0f || y < 0f)
                {
                    x = (vw - w) * 0.5f;
                    y = (vh - h) * 0.5f;
                }

                WindowRect = new Rect(x, y, w, h);
            }

            WindowRect = Clamp(WindowRect, vw, vh);
        }

        private static Rect Clamp(Rect rect, float vw, float vh)
        {
            float maxW = Mathf.Max(MinWidth, vw - 16f);
            float maxH = Mathf.Max(MinHeight, vh - 16f);

            rect.width = Mathf.Clamp(rect.width, MinWidth, maxW);
            rect.height = Mathf.Clamp(rect.height, MinHeight, maxH);
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, vw - rect.width));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, vh - rect.height));

            return rect;
        }

        private static Rect TabRect(Rect win, int index)
        {
            return new Rect(win.x + 210f + index * 112f, win.y + 17f, 104f, 32f);
        }

        private static Rect CloseRect(Rect win)
        {
            return new Rect(win.x + win.width - 58f, win.y + 16f, 34f, 30f);
        }

        private static void HandleWindowInput(float vw, float vh)
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            Rect win = WindowRect;
            Vector2 mouse = e.mousePosition;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (IsOverGrip(win, mouse))
                {
                    _resizing = true;
                    _resizeStart = mouse;
                    _resizeOrigin = new Vector2(win.width, win.height);
                    e.Use();
                    return;
                }

                var header = new Rect(win.x, win.y, win.width, HeaderHeight);
                if (header.Contains(mouse) && !CloseRect(win).Contains(mouse) && !IsOverTab(win, mouse))
                {
                    _dragging = true;
                    _dragOffset = mouse - new Vector2(win.x, win.y);
                    e.Use();
                }

                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                if (_dragging)
                {
                    var moved = new Rect(mouse.x - _dragOffset.x, mouse.y - _dragOffset.y, win.width, win.height);
                    WindowRect = Clamp(moved, vw, vh);
                    e.Use();
                }
                else if (_resizing)
                {
                    Vector2 delta = mouse - _resizeStart;
                    var resized = new Rect(win.x, win.y, _resizeOrigin.x + delta.x, _resizeOrigin.y + delta.y);
                    WindowRect = Clamp(resized, vw, vh);
                    e.Use();
                }

                return;
            }

            if ((e.type == EventType.MouseUp || e.type == EventType.Ignore) && (_dragging || _resizing))
            {
                _dragging = false;
                _resizing = false;
                SaveLayout();
                e.Use();
            }
        }

        private static bool IsOverTab(Rect win, Vector2 mouse)
        {
            for (int i = 0; i < TabNames.Length; i++)
            {
                if (TabRect(win, i).Contains(mouse))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOverGrip(Rect win, Vector2 mouse)
        {
            var corner = new Rect(win.xMax - 26f, win.yMax - 26f, 26f, 26f);
            var rightEdge = new Rect(win.xMax - 8f, win.y, 8f, win.height);
            var bottomEdge = new Rect(win.x, win.yMax - 8f, win.width, 8f);

            return corner.Contains(mouse) || rightEdge.Contains(mouse) || bottomEdge.Contains(mouse);
        }

        private static void SaveLayout()
        {
            QuickMenuPlugin.WindowX.Value = WindowRect.x;
            QuickMenuPlugin.WindowY.Value = WindowRect.y;
            QuickMenuPlugin.WindowWidth.Value = WindowRect.width;
            QuickMenuPlugin.WindowHeight.Value = WindowRect.height;

            // 立即落盘：即使之后游戏被强制结束，窗口位置和尺寸也不会丢。
            QuickMenuPlugin.Instance?.Config.Save();
        }

        // ---------- 绘制 ----------

        private static void DrawBackdrop(float vw, float vh)
        {
            if (!QuickMenuPlugin.DimBackground.Value)
            {
                return;
            }

            Fill(new Rect(0f, 0f, vw, vh), new Color(0f, 0f, 0f, 0.42f));
        }

        private static void DrawChrome(Rect win)
        {
            float alpha = Mathf.Clamp(QuickMenuPlugin.Opacity.Value, 0.3f, 1f);

            Fill(win, new Color(PanelColor.r, PanelColor.g, PanelColor.b, alpha));

            // 1px 边框，低不透明度时用来界定窗口边缘。
            Fill(new Rect(win.x, win.y, win.width, 1f), BorderColor);
            Fill(new Rect(win.x, win.yMax - 1f, win.width, 1f), BorderColor);
            Fill(new Rect(win.x, win.y, 1f, win.height), BorderColor);
            Fill(new Rect(win.xMax - 1f, win.y, 1f, win.height), BorderColor);
        }

        private static void DrawHeader(Rect win)
        {
            float alpha = Mathf.Clamp(QuickMenuPlugin.Opacity.Value + 0.06f, 0.3f, 1f);
            Fill(new Rect(win.x, win.y, win.width, HeaderHeight),
                new Color(HeaderColor.r, HeaderColor.g, HeaderColor.b, alpha));
            Fill(new Rect(win.x, win.y + HeaderHeight, win.width, 1f), BorderColor);

            GUI.Label(new Rect(win.x + 26f, win.y + 15f, 240f, 36f), "快捷菜单", Title);

            for (int i = 0; i < TabNames.Length; i++)
            {
                Color prevBg = GUI.backgroundColor;
                if (i == _tab)
                {
                    GUI.backgroundColor = new Color(0.30f, 0.50f, 0.80f);
                }

                if (GUI.Button(TabRect(win, i), TabNames[i], i == _tab ? TabOn : TabOff))
                {
                    _tab = i;
                }

                GUI.backgroundColor = prevBg;
            }

            if (GUI.Button(CloseRect(win), "×", Btn))
            {
                QuickMenuState.Close();
            }
        }

        private static void DrawBody(Rect win)
        {
            var body = new Rect(win.x + 22f, win.y + HeaderHeight + 12f, win.width - 44f, win.height - 126f);
            GUILayout.BeginArea(body);
            switch (_tab)
            {
                case 0:
                    QuickActionsPage.Instance.Draw();
                    break;
                case 1:
                    ItemsPage.Instance.Draw();
                    break;
                case 2:
                    LimbsPage.Instance.Draw();
                    break;
                case 3:
                    CommandListPage.Instance.Draw();
                    break;
                default:
                    LogPage.Instance.Draw();
                    break;
            }
            GUILayout.EndArea();
        }

        private static void DrawFooter(Rect win)
        {
            var rect = new Rect(win.x + 26f, win.y + win.height - 44f, win.width - 52f, 26f);

            string status = ConsoleBridge.IsReady
                ? $"控制台就绪 · {ConsoleBridge.Commands.Count} 条命令 · 热键 {QuickMenuPlugin.Hotkey.Value}"
                : "控制台初始化中…";

            GUI.Label(rect, status, Footer);

            string message = QuickMenuState.CurrentMessage(6f);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (_footerRight == null)
            {
                _footerRight = new GUIStyle(Footer) { alignment = TextAnchor.MiddleRight };
                _footerRight.normal.textColor = new Color(0.95f, 0.78f, 0.45f);
            }

            GUI.Label(rect, Truncate(message, 78), _footerRight);
        }

        private static void DrawGrip(Rect win)
        {
            var color = new Color(1f, 1f, 1f, 0.28f);

            for (int i = 0; i < 3; i++)
            {
                float offset = 6f + i * 5f;
                Fill(new Rect(win.xMax - offset, win.yMax - 5f, 4f, 2f), color);
            }
        }

        internal static void FillRect(Rect rect, Color color)
        {
            Fill(rect, color);
        }

        /// <summary>按 sprite 的 textureRect 绘制图集子区域，并保持原始宽高比居中。</summary>
        internal static void DrawSprite(Rect area, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            Texture2D texture = sprite.texture;
            if (texture == null)
            {
                return;
            }

            Rect rect = sprite.textureRect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var uv = new Rect(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height);

            float aspect = rect.width / rect.height;
            Rect draw = area;

            if (aspect > area.width / area.height)
            {
                draw.height = area.width / aspect;
                draw.y = area.y + (area.height - draw.height) * 0.5f;
            }
            else
            {
                draw.width = area.height * aspect;
                draw.x = area.x + (area.width - draw.width) * 0.5f;
            }

            GUI.DrawTextureWithTexCoords(draw, texture, uv, true);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // ---------- 样式 ----------

        private static void EnsureResources()
        {
            if (_initialized)
            {
                return;
            }

            Cjk = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 16);

            if (Cjk == null)
            {
                QuickMenuPlugin.Log?.LogWarning("未能加载系统中文字体，界面文字可能显示为方框。");
            }

            Title = MakeLabel(24, TextColor, FontStyle.Bold);
            SectionTitle = MakeLabel(17, TextColor, FontStyle.Bold);
            Label = MakeLabel(15, TextColor);
            Muted = MakeLabel(13, MutedColor);
            Footer = MakeLabel(13, MutedColor);

            LogLine = MakeLabel(13, TextColor);
            LogLine.richText = false;
            LogLine.wordWrap = true;

            CellLabel = MakeLabel(11, TextColor);
            CellLabel.alignment = TextAnchor.UpperCenter;
            CellLabel.wordWrap = true;
            CellLabel.richText = false;
            CellLabel.clipping = TextClipping.Clip;

            CellIconFallback = MakeLabel(20, MutedColor);
            CellIconFallback.alignment = TextAnchor.MiddleCenter;

            CardTitle = MakeLabel(15, TextColor, FontStyle.Bold);
            CardSmall = MakeLabel(12, new Color(0.76f, 0.81f, 0.88f));
            CardWarn = MakeLabel(12, new Color(0.96f, 0.66f, 0.56f));

            CardValue = MakeLabel(12, TextColor);
            CardValue.alignment = TextAnchor.MiddleRight;

            TabOn = MakeButton(15, FontStyle.Bold);
            TabOff = MakeButton(15, FontStyle.Normal);
            Btn = MakeButton(15, FontStyle.Normal);
            BtnSmall = MakeButton(13, FontStyle.Normal);

            // 输入框保留引擎浅色底，文字继续用深色，白字会不可见。
            Field = new GUIStyle(GUI.skin.textField) { font = Cjk, fontSize = 14 };

            _initialized = true;
        }

        private static GUIStyle MakeLabel(int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                font = Cjk,
                fontSize = size,
                fontStyle = style,
                richText = true
            };
            s.normal.textColor = color;
            return s;
        }

        /// <summary>沿用引擎默认按钮底色，只把文字统一成纯白，保证在深色面板上可读。</summary>
        private static GUIStyle MakeButton(int size, FontStyle style)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                font = Cjk,
                fontSize = size,
                fontStyle = style
            };

            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            s.focused.textColor = Color.white;
            s.onNormal.textColor = Color.white;
            s.onHover.textColor = Color.white;
            s.onActive.textColor = Color.white;
            s.onFocused.textColor = Color.white;

            return s;
        }

        internal static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max - 1) + "…";
        }

        internal static void Section(string title)
        {
            GUILayout.Space(6f);
            GUILayout.Label(title, SectionTitle);
        }

        /// <summary>width 为 0 时按钮自动均分可用宽度，窗口缩放时不会溢出。</summary>
        internal static bool WideButton(string text, float width = 0f, float height = 32f)
        {
            if (width <= 0f)
            {
                return GUILayout.Button(text, Btn, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            }

            return GUILayout.Button(text, Btn, GUILayout.Width(width), GUILayout.Height(height));
        }
    }
}
