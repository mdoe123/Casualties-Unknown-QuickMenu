using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 「生成」页：按类型分为 物品 / 建筑 / 陷阱 / 物块 / 流体 / 生物。
    ///
    /// 物品走控制台命令 spawn；建筑与陷阱直接实例化预制体；
    /// 物块与流体没有控制台入口，直接调用游戏 API；生物走 spawncharacter。
    /// 所有放置动作都经过点选模式，玩家点哪里就落在哪里。
    /// </summary>
    internal sealed class SpawnPage
    {
        internal static readonly SpawnPage Instance = new SpawnPage();

        private enum Kind
        {
            Items,
            Buildings,
            Traps,
            Blocks,
            Fluids,
            Characters
        }

        private static readonly string[] KindLabels = { "物品", "建筑", "陷阱", "物块", "流体", "生物" };

        private const float CellWidth = 106f;
        private const float CellHeight = 96f;

        private static readonly string[] CategoryOrder =
        {
            "medical", "food", "water", "drug", "tool", "container", "utility", "trash", "unobtainable", "custom"
        };

        private Kind _kind = Kind.Items;

        // 物品
        private readonly List<ItemEntry> _filtered = new List<ItemEntry>();
        private readonly List<string> _categories = new List<string>();
        private string _search = string.Empty;
        private string _category;
        private string _filterKey;
        private bool _categoriesBuilt;
        private Vector2 _scroll;
        private string _selectedId;
        private string _amount = "1";

        // 建筑 / 陷阱 / 生物
        private Vector2 _entityScroll;
        private SpawnEntity _selectedEntity;

        internal void Draw()
        {
            DrawKindRow();

            switch (_kind)
            {
                case Kind.Items:
                    DrawItems();
                    break;
                case Kind.Buildings:
                    DrawEntityGrid(SpawnCatalog.Buildings, "没有找到可生成的建筑。");
                    break;
                case Kind.Traps:
                    DrawEntityGrid(SpawnCatalog.Traps, "没有找到陷阱类建筑。");
                    break;
                case Kind.Blocks:
                    DrawBlockList();
                    break;
                case Kind.Fluids:
                    DrawFluidList();
                    break;
                default:
                    DrawEntityGrid(SpawnCatalog.Creatures, "没有找到可生成的生物。");
                    break;
            }
        }

        private void DrawKindRow()
        {
            SpawnCatalog.EnsureEntities();
            SpawnCatalog.EnsureBlocks();
            SpawnCatalog.EnsureFluids();

            GUILayout.BeginHorizontal();

            for (int i = 0; i < KindLabels.Length; i++)
            {
                if (Chip(KindLabels[i], (int)_kind == i))
                {
                    _kind = (Kind)i;
                    _entityScroll = Vector2.zero;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // ---------- 物品 ----------

        private void DrawItems()
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
            DrawItemBar();
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
            float viewHeight = RemainingHeight(46f);
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

                        ItemEntry entry = _filtered[index];
                        DrawCell(
                            ItemCatalog.DisplayName(entry.Id),
                            entry.Icon,
                            string.Equals(entry.Id, _selectedId, StringComparison.OrdinalIgnoreCase),
                            () => _selectedId = entry.Id);
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(Mathf.Max(0f, (rows - lastRow - 1) * CellHeight));
            }

            GUILayout.EndScrollView();
        }

        // ---------- 建筑 / 陷阱 ----------

        private void DrawEntityGrid(List<SpawnEntity> list, string emptyHint)
        {
            float viewHeight = RemainingHeight(46f);
            float available = QuickMenuUI.BodyWidth - 18f;
            int columns = Mathf.Max(1, Mathf.FloorToInt(available / CellWidth));

            _entityScroll = GUILayout.BeginScrollView(_entityScroll, GUILayout.Height(viewHeight));

            if (list.Count == 0)
            {
                GUILayout.Label(emptyHint, QuickMenuUI.Muted);
            }
            else
            {
                for (int i = 0; i < list.Count; i += columns)
                {
                    GUILayout.BeginHorizontal();
                    for (int column = 0; column < columns && i + column < list.Count; column++)
                    {
                        SpawnEntity entity = list[i + column];
                        DrawCell(entity.Name, entity.Icon, ReferenceEquals(entity, _selectedEntity),
                            () => _selectedEntity = entity);
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();

            if (_selectedEntity == null)
            {
                GUILayout.Label("点击图标选择，再点「点选放置」。", QuickMenuUI.Muted, GUILayout.Height(30f));
            }
            else
            {
                GUILayout.Label(_selectedEntity.Name, QuickMenuUI.Label, GUILayout.Height(30f));
                GUILayout.Label(QuickMenuUI.Truncate(_selectedEntity.Prefab.name, 18),
                    QuickMenuUI.Muted, GUILayout.Height(30f));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("点选放置", QuickMenuUI.Btn, GUILayout.Width(100f), GUILayout.Height(30f)))
            {
                BeginPlaceEntity();
            }

            GUILayout.EndHorizontal();
        }

        private void BeginPlaceEntity()
        {
            SpawnEntity entity = _selectedEntity;
            if (entity == null)
            {
                QuickMenuState.Notify("请先选择一个目标。");
                return;
            }

            string name = entity.Name;
            int characterIndex = entity.CharacterIndex;

            AimMode.Begin(position =>
            {
                bool ok = characterIndex >= 0
                    ? SpawnCatalog.PlaceCharacter(characterIndex, position, out string error)
                    : SpawnCatalog.PlaceBuilding(entity.Prefab, position, out error);

                QuickMenuState.Notify(ok ? $"已生成 {name}" : "生成失败：" + error);
            }, $"生成 {name}");
        }

        // ---------- 物块 / 流体 / 生物 ----------

        private void DrawBlockList()
        {
            if (WorldGeneration.world == null || SpawnCatalog.Blocks.Count == 0)
            {
                GUILayout.Label("进入对局后才能读取方块表。", QuickMenuUI.Muted);
                return;
            }

            float viewHeight = RemainingHeight(16f);
            _entityScroll = GUILayout.BeginScrollView(_entityScroll, GUILayout.Height(viewHeight));

            GUILayout.Label("写入方块不做支撑校验，放错位置需要手动修回来。", QuickMenuUI.Muted);

            foreach (BlockEntry block in SpawnCatalog.Blocks)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label(block.Name, QuickMenuUI.Label, GUILayout.Width(220f), GUILayout.Height(26f));
                GUILayout.Label(block.Id.ToString(), QuickMenuUI.Muted, GUILayout.Width(40f), GUILayout.Height(26f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("点选放置", QuickMenuUI.BtnSmall,
                        GUILayout.Width(88f), GUILayout.Height(26f)))
                {
                    int id = block.Id;
                    string label = block.Name;
                    AimMode.Begin(position =>
                    {
                        if (SpawnCatalog.PlaceBlock(id, position, out string error))
                        {
                            QuickMenuState.Notify($"已放置 {label}");
                        }
                        else
                        {
                            QuickMenuState.Notify("放置失败：" + error);
                        }
                    }, $"放置 {label}");
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void DrawFluidList()
        {
            float viewHeight = RemainingHeight(16f);
            _entityScroll = GUILayout.BeginScrollView(_entityScroll, GUILayout.Height(viewHeight));

            foreach (FluidEntry fluid in SpawnCatalog.Fluids)
            {
                GUILayout.BeginHorizontal();

                var swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
                QuickMenuUI.FillRect(swatch, fluid.Color);

                GUILayout.Label(fluid.Name, QuickMenuUI.Label, GUILayout.Width(180f), GUILayout.Height(26f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("单格", QuickMenuUI.BtnSmall,
                        GUILayout.Width(64f), GUILayout.Height(26f)))
                {
                    BeginPlaceFluid(fluid, false);
                }

                if (GUILayout.Button("区域填充", QuickMenuUI.BtnSmall,
                        GUILayout.Width(88f), GUILayout.Height(26f)))
                {
                    BeginPlaceFluid(fluid, true);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Label("「单格」只改一格；「区域填充」会蔓延到相连空间，最多 2048 格。",
                QuickMenuUI.Muted);

            GUILayout.EndScrollView();
        }

        private void BeginPlaceFluid(FluidEntry fluid, bool flood)
        {
            int type = fluid.Id;
            string name = fluid.Name;
            AimMode.Begin(position =>
            {
                if (SpawnCatalog.PlaceFluid(type, position, flood, out string error))
                {
                    QuickMenuState.Notify(flood ? $"已在 {name} 处区域填充" : $"已放置一格 {name}");
                }
                else
                {
                    QuickMenuState.Notify("放置失败：" + error);
                }
            }, (flood ? "区域填充 " : "放置 ") + name);
        }

        // ---------- 通用 ----------

        /// <summary>按「已经排布掉的高度」推算剩余空间，避免底部留出空白。</summary>
        private static float RemainingHeight(float reserve)
        {
            Rect last = GUILayoutUtility.GetLastRect();
            return Mathf.Max(120f, QuickMenuUI.BodyHeight - last.yMax - reserve);
        }

        private static void DrawCell(string name, Sprite icon, bool selected, Action onClick)
        {
            Rect cell = GUILayoutUtility.GetRect(CellWidth, CellHeight,
                GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));

            bool hovered = cell.Contains(Event.current.mousePosition);

            if (selected)
            {
                QuickMenuUI.FillRect(cell, new Color(0.26f, 0.45f, 0.75f, 0.55f));
            }
            else if (hovered)
            {
                QuickMenuUI.FillRect(cell, new Color(1f, 1f, 1f, 0.08f));
            }

            var iconRect = new Rect(cell.x + 9f, cell.y + 2f, cell.width - 18f, 56f);
            QuickMenuUI.DrawSprite(iconRect, icon);

            if (icon == null)
            {
                GUI.Label(iconRect, "?", QuickMenuUI.CellIconFallback);
            }

            var labelRect = new Rect(cell.x + 3f, cell.y + 60f, cell.width - 6f, 34f);
            GUI.Label(labelRect, QuickMenuUI.Truncate(name, 12), QuickMenuUI.CellLabel);

            if (GUI.Button(cell, GUIContent.none, GUIStyle.none))
            {
                onClick?.Invoke();
            }
        }

        private void DrawItemBar()
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
                GUILayout.Label(QuickMenuUI.Truncate(_selectedId, 14),
                    QuickMenuUI.Muted, GUILayout.Height(30f));
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("数量", QuickMenuUI.Label, GUILayout.Width(34f), GUILayout.Height(30f));
            _amount = GUILayout.TextField(_amount, QuickMenuUI.Field, GUILayout.Width(44f), GUILayout.Height(26f));

            if (GUILayout.Button("生成到光标", QuickMenuUI.Btn, GUILayout.Width(100f), GUILayout.Height(30f)))
            {
                BeginAim();
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

        /// <summary>关掉菜单让玩家点地面，再以该点执行生成。</summary>
        private void BeginAim()
        {
            if (string.IsNullOrEmpty(_selectedId))
            {
                QuickMenuState.Notify("请先选择一个物品。");
                return;
            }

            int amount = ParseAmount();
            string id = _selectedId;

            AimMode.Begin($"spawn {id} {{0}} 1 {amount}",
                $"生成 {ItemCatalog.DisplayName(id)} ×{amount}");
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

        private int ParseAmount()
        {
            if (!int.TryParse(_amount, out int amount))
            {
                amount = 1;
            }

            amount = Mathf.Clamp(amount, 1, 20);
            _amount = amount.ToString();
            return amount;
        }

        private void Spawn(string position)
        {
            if (string.IsNullOrEmpty(_selectedId))
            {
                QuickMenuState.Notify("请先选择一个物品。");
                return;
            }

            int amount = ParseAmount();
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
