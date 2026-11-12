using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「物品」页：以图标网格浏览全部可生成物品，名称取自游戏本地化表。
    /// 网格做了行级虚拟化，只绘制可见行，避免 300 个格子每帧全量布局。
    /// </summary>
    internal sealed class ItemsPage
    {
        internal static readonly ItemsPage Instance = new ItemsPage();

        private const float CellWidth = 106f;
        private const float CellHeight = 96f;

        private static readonly string[] CategoryOrder =
        {
            "medical", "food", "water", "drug", "tool", "container", "utility", "trash", "unobtainable", "custom"
        };

        private readonly List<ItemEntry> _filtered = new List<ItemEntry>();
        private readonly List<string> _categories = new List<string>();

        private string _search = string.Empty;
        private string _category;
        private string _filterKey;
        private bool _categoriesBuilt;

        private Vector2 _scroll;
        private string _selectedId;
        private string _amount = "1";

        internal void Draw()
        {
            if (!ItemCatalog.IsBuilt)
            {
                ItemCatalog.EnsureBuilt();
            }

            if (ItemCatalog.All.Count == 0)
            {
                GUILayout.Label("物品表尚未就绪，请进入游戏后再试。", QuickMenuUI.Muted);
                return;
            }

            EnsureCategories();
            DrawFilters();
            DrawGrid();
            DrawBottomBar();
        }

        private void EnsureCategories()
        {
            if (_categoriesBuilt)
            {
                return;
            }

            _categoriesBuilt = true;
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ItemEntry entry in ItemCatalog.All)
            {
                if (!string.IsNullOrEmpty(entry.Category))
                {
                    present.Add(entry.Category);
                }
            }

            foreach (string category in CategoryOrder)
            {
                if (present.Remove(category))
                {
                    _categories.Add(category);
                }
            }

            foreach (string leftover in present)
            {
                _categories.Add(leftover);
            }
        }

        private void DrawFilters()
        {
            // 分类数据来自游戏物品表；取不到时（极早期）就不显示这一行，避免只剩一个「全部」。
            if (_categories.Count > 0)
            {
                GUILayout.BeginHorizontal();

                if (Chip("全部", _category == null))
                {
                    _category = null;
                    _filterKey = null;
                }

                foreach (string category in _categories)
                {
                    if (Chip(ItemCatalog.CategoryLabel(category), _category == category))
                    {
                        _category = category;
                        _filterKey = null;
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("搜索", QuickMenuUI.Label, GUILayout.Width(40f), GUILayout.Height(26f));

            string next = GUILayout.TextField(_search, QuickMenuUI.Field, GUILayout.Height(26f));
            if (next != _search)
            {
                _search = next;
                _filterKey = null;
            }

            if (GUILayout.Button("清空", QuickMenuUI.Btn, GUILayout.Width(58f), GUILayout.Height(26f)))
            {
                _search = string.Empty;
                _filterKey = null;
            }

            GUILayout.EndHorizontal();

            EnsureFiltered();
        }

        private bool Chip(string label, bool active)
        {
            Color prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.30f, 0.50f, 0.80f);
            }

            bool clicked = GUILayout.Button(label, QuickMenuUI.BtnSmall,
                GUILayout.Width(Mathf.Max(54f, label.Length * 14f + 12f)), GUILayout.Height(24f));

            GUI.backgroundColor = prev;
            return clicked;
        }

        private void EnsureFiltered()
        {
            string key = string.Concat(
                _search, "|", _category ?? "*", "|",
                ItemCatalog.All.Count.ToString(), "|",
                ItemCatalog.IsChinese ? "zh" : "en");

            if (key == _filterKey)
            {
                return;
            }

            _filterKey = key;
            _filtered.Clear();

            foreach (ItemEntry entry in ItemCatalog.All)
            {
                if (_category != null && !string.Equals(entry.Category, _category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(_search))
                {
                    string name = ItemCatalog.DisplayName(entry.Id);
                    bool hit = name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0
                               || entry.Id.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit)
                    {
                        continue;
                    }
                }

                _filtered.Add(entry);
            }
        }

        private void DrawGrid()
        {
            float viewHeight = Mathf.Max(150f, QuickMenuUI.BodyHeight - 168f);
            float available = QuickMenuUI.BodyWidth - 18f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(available / CellWidth));
            int rows = Mathf.CeilToInt(_filtered.Count / (float)columns);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(viewHeight));

            if (_filtered.Count == 0)
            {
                GUILayout.Label("没有匹配的物品。", QuickMenuUI.Muted);
            }
            else
            {
                int firstRow = Mathf.Clamp(Mathf.FloorToInt(_scroll.y / CellHeight) - 1, 0, Mathf.Max(0, rows - 1));
                int lastRow = Mathf.Clamp(Mathf.CeilToInt((_scroll.y + viewHeight) / CellHeight) + 1, 0, rows - 1);

                GUILayout.Space(firstRow * CellHeight);

                for (int row = firstRow; row <= lastRow; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int column = 0; column < columns; column++)
                    {
                        int index = row * columns + column;
                        if (index >= _filtered.Count)
                        {
                            break;
                        }

                        DrawCell(_filtered[index]);
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(Mathf.Max(0f, (rows - lastRow - 1) * CellHeight));
            }

            GUILayout.EndScrollView();
        }

        private void DrawCell(ItemEntry entry)
        {
            Rect cell = GUILayoutUtility.GetRect(CellWidth, CellHeight,
                GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));

            bool selected = string.Equals(entry.Id, _selectedId, StringComparison.OrdinalIgnoreCase);
            bool hovered = cell.Contains(Event.current.mousePosition);

            if (selected)
            {
                QuickMenuUI.FillRect(cell, new Color(0.26f, 0.45f, 0.75f, 0.55f));
            }
            else if (hovered)
            {
                QuickMenuUI.FillRect(cell, new Color(1f, 1f, 1f, 0.08f));
            }

            var iconRect = new Rect(cell.x + 9f, cell.y + 4f, cell.width - 18f, 58f);
            QuickMenuUI.DrawSprite(iconRect, entry.Icon);

            if (entry.Icon == null)
            {
                GUI.Label(iconRect, "?", QuickMenuUI.CellIconFallback);
            }

            var labelRect = new Rect(cell.x + 3f, cell.y + 62f, cell.width - 6f, 30f);
            GUI.Label(labelRect, QuickMenuUI.Truncate(ItemCatalog.DisplayName(entry.Id), 12),
                QuickMenuUI.CellLabel);

            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
            {
                _selectedId = entry.Id;
            }
        }

        private void DrawBottomBar()
        {
            GUILayout.BeginHorizontal();

            if (string.IsNullOrEmpty(_selectedId))
            {
                GUILayout.Label("点击图标选择物品。", QuickMenuUI.Muted, GUILayout.Height(30f));
            }
            else
            {
                var iconRect = GUILayoutUtility.GetRect(30f, 30f, GUILayout.Width(30f), GUILayout.Height(30f));
                QuickMenuUI.DrawSprite(iconRect, FindIcon(_selectedId));

                GUILayout.Label(ItemCatalog.DisplayName(_selectedId), QuickMenuUI.Label, GUILayout.Height(30f));
                GUILayout.Label(_selectedId, QuickMenuUI.Muted, GUILayout.Height(30f));
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("数量", QuickMenuUI.Label, GUILayout.Width(34f), GUILayout.Height(30f));
            _amount = GUILayout.TextField(_amount, QuickMenuUI.Field, GUILayout.Width(44f), GUILayout.Height(26f));

            if (GUILayout.Button("生成到光标", QuickMenuUI.Btn, GUILayout.Width(100f), GUILayout.Height(30f)))
            {
                Spawn("cursor");
            }

            if (GUILayout.Button("生成到脚下", QuickMenuUI.Btn, GUILayout.Width(100f), GUILayout.Height(30f)))
            {
                Spawn("player");
            }

            if (GUILayout.Button("随机位置", QuickMenuUI.Btn, GUILayout.Width(80f), GUILayout.Height(30f)))
            {
                Spawn("random");
            }

            GUILayout.EndHorizontal();
        }

        private static Sprite FindIcon(string id)
        {
            foreach (ItemEntry entry in ItemCatalog.All)
            {
                if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Icon;
                }
            }

            return null;
        }

        private void Spawn(string position)
        {
            if (string.IsNullOrEmpty(_selectedId))
            {
                QuickMenuState.Notify("请先选择一个物品。");
                return;
            }

            if (!int.TryParse(_amount, out int amount))
            {
                amount = 1;
            }

            amount = Mathf.Clamp(amount, 1, 20);
            _amount = amount.ToString();

            string command = $"spawn {_selectedId} {position} 1 {amount}";

            if (!ConsoleBridge.TryExecute(command, out string error))
            {
                QuickMenuState.Notify("生成失败：" + error);
                return;
            }

            string last = ConsoleBridge.LastLogLine();
            if (!string.IsNullOrEmpty(last) && last.IndexOf("Created", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                QuickMenuState.Notify($"已生成 {ItemCatalog.DisplayName(_selectedId)} ×{amount}");
            }
            else
            {
                QuickMenuState.Notify(last ?? "生成失败，请查看日志页。");
            }

            if (QuickMenuPlugin.CloseAfterExecute.Value)
            {
                QuickMenuState.Close();
            }
        }
    }
}
