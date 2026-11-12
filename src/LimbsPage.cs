using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「肢体」页：按卡片列出当前角色的所有肢体，显示皮肤/肌肉状态与伤病标记，
    /// 并提供止血、断肢、接回三个按肢体生效的操作。
    /// </summary>
    internal sealed class LimbsPage
    {
        internal static readonly LimbsPage Instance = new LimbsPage();

        private const float Gap = 8f;
        private const float CardHeight = 150f;

        private Vector2 _scroll;

        internal void Draw()
        {
            Body body = CurrentBody();
            if (body == null || body.limbs == null || body.limbs.Length == 0)
            {
                GUILayout.Label("进入游戏后可用。", QuickMenuUI.Muted);
                return;
            }

            int columns = Mathf.Clamp(Mathf.FloorToInt((QuickMenuUI.BodyWidth + Gap) / (272f + Gap)), 1, 3);
            float cardWidth = Mathf.Floor((QuickMenuUI.BodyWidth - Gap * (columns - 1)) / columns);

            float viewHeight = Mathf.Max(160f, QuickMenuUI.BodyHeight - 10f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(viewHeight));

            for (int index = 0; index < body.limbs.Length; index += columns)
            {
                GUILayout.BeginHorizontal();

                for (int column = 0; column < columns; column++)
                {
                    int limbIndex = index + column;
                    if (limbIndex >= body.limbs.Length)
                    {
                        break;
                    }

                    Limb limb = body.limbs[limbIndex];
                    if (limb != null)
                    {
                        DrawCard(limb, cardWidth);
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(Gap);
            }

            GUILayout.EndScrollView();
        }

        private static Body CurrentBody()
        {
            try
            {
                PlayerCamera camera = PlayerCamera.main;
                return camera != null ? camera.body : null;
            }
            catch
            {
                return null;
            }
        }

        private void DrawCard(Limb limb, float width)
        {
            Rect card = GUILayoutUtility.GetRect(width, CardHeight,
                GUILayout.Width(width), GUILayout.Height(CardHeight));

            QuickMenuUI.FillRect(card, new Color(1f, 1f, 1f, 0.05f));

            string title = string.IsNullOrEmpty(limb.fullName) ? limb.name : limb.fullName;
            GUI.Label(new Rect(card.x + 12f, card.y + 8f, width - 24f, 26f),
                QuickMenuUI.Truncate(title, 14), QuickMenuUI.CardTitle);

            string status = BuildStatus(limb);
            bool severe = limb.dismembered || limb.broken || limb.infected || limb.bleedAmount > 0.05f;
            GUI.Label(new Rect(card.x + 12f, card.y + 36f, width - 24f, 20f),
                QuickMenuUI.Truncate(status, 24),
                severe ? QuickMenuUI.CardWarn : QuickMenuUI.CardSmall);

            DrawBar(card.x + 12f, card.y + 62f, width - 24f, "皮肤", limb.skinHealth);
            DrawBar(card.x + 12f, card.y + 84f, width - 24f, "肌肉", limb.muscleHealth);

            var buttonWidth = Mathf.Floor((width - 30f) / 2f);
            float buttonY = card.y + CardHeight - 42f;

            bool canStopBleed = limb.bleedAmount > 0.01f;

            GUI.enabled = canStopBleed;
            if (GUI.Button(new Rect(card.x + 12f, buttonY, buttonWidth, 28f), "止血", QuickMenuUI.BtnSmall))
            {
                Run($"setlimbfield {limb.name} bleedAmount 0", "已止血");
            }

            GUI.enabled = true;

            bool canAmputate = !limb.dismembered && !limb.isHead && !limb.isAbdomen && !limb.isVital;
            bool canReattach = limb.dismembered;
            bool enabled = canAmputate || canReattach;

            GUI.enabled = enabled;
            string actionLabel = canReattach ? "接回" : "断肢";

            if (GUI.Button(new Rect(card.x + 18f + buttonWidth, buttonY, buttonWidth, 28f),
                    actionLabel, QuickMenuUI.BtnSmall))
            {
                Run(canReattach ? $"reattach {limb.name}" : $"amputate {limb.name}",
                    canReattach ? "已接回" : "已断肢");
            }

            GUI.enabled = true;
        }

        private static void DrawBar(float x, float y, float width, string label, float value)
        {
            GUI.Label(new Rect(x, y, 32f, 18f), label, QuickMenuUI.CardSmall);

            var track = new Rect(x + 34f, y + 4f, Mathf.Max(40f, width - 100f), 8f);
            QuickMenuUI.FillRect(track, new Color(0f, 0f, 0f, 0.45f));

            float ratio = Mathf.Clamp01(value / 100f);
            Color color = Color.Lerp(
                new Color(0.85f, 0.25f, 0.25f),
                new Color(0.35f, 0.80f, 0.42f),
                ratio);

            if (ratio > 0f)
            {
                QuickMenuUI.FillRect(new Rect(track.x, track.y, track.width * ratio, track.height), color);
            }

            GUI.Label(new Rect(x + width - 60f, y, 60f, 18f),
                value.ToString("0"), QuickMenuUI.CardValue);
        }

        private static string BuildStatus(Limb limb)
        {
            if (limb.dismembered)
            {
                return "已断肢";
            }

            var parts = new List<string>(4);

            if (limb.bleedAmount > 0.05f)
            {
                parts.Add($"流血 {limb.bleedAmount:0.0}");
            }

            if (limb.pain >= 1f)
            {
                parts.Add($"疼痛 {limb.pain:0}");
            }

            if (limb.infected)
            {
                parts.Add("感染");
            }

            if (limb.broken)
            {
                parts.Add(limb.splinted ? "骨折·已固定" : "骨折");
            }

            if (limb.dislocated)
            {
                parts.Add("脱臼");
            }

            if (limb.shrapnel > 0)
            {
                parts.Add($"弹片 {limb.shrapnel}");
            }

            if (parts.Count == 0)
            {
                return "正常";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" · ");
                }

                builder.Append(parts[i]);
            }

            return builder.ToString();
        }

        private static void Run(string command, string success)
        {
            if (!ConsoleBridge.TryExecute(command, out string error))
            {
                QuickMenuState.Notify("执行失败：" + error);
                return;
            }

            string last = ConsoleBridge.LastLogLine();
            bool failed = !string.IsNullOrEmpty(last)
                          && (last.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                              || last.IndexOf("is not a valid", StringComparison.OrdinalIgnoreCase) >= 0
                              || last.IndexOf("cannot be", StringComparison.OrdinalIgnoreCase) >= 0);

            QuickMenuState.Notify(failed ? last : success);
        }
    }
}
