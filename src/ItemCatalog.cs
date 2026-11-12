using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    internal sealed class ItemEntry
    {
        internal string Id;
        internal string Category;
        internal Sprite Icon;
    }

    /// <summary>
    /// 物品目录：扫描 Resources 中带 Item 组件的预制体，一次性拿到 id 与图标。
    /// 名称不缓存，每次显示时向游戏本地化表查询，切换语言后立即生效。
    /// </summary>
    internal static class ItemCatalog
    {
        private static readonly List<ItemEntry> Entries = new List<ItemEntry>();
        private static readonly Dictionary<string, string[]> CategoryNames =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "medical", new[] { "医疗", "Medical" } },
                { "food", new[] { "食物", "Food" } },
                { "water", new[] { "饮水", "Water" } },
                { "drug", new[] { "药物", "Drugs" } },
                { "tool", new[] { "工具", "Tools" } },
                { "container", new[] { "容器", "Containers" } },
                { "utility", new[] { "实用", "Utility" } },
                { "trash", new[] { "垃圾", "Trash" } },
                { "unobtainable", new[] { "未开放", "Unobtainable" } },
                { "custom", new[] { "其他", "Misc" } }
            };

        private static bool _built;

        internal static IReadOnlyList<ItemEntry> All
        {
            get { return Entries; }
        }

        internal static bool IsBuilt
        {
            get { return _built; }
        }

        internal static void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            _built = true;

            try
            {
                // 主菜单下游戏的 Item.SetupItems 还没跑过，分类与描述会缺失。
                // 该方法是纯数据构建（不依赖世界），这里补一次即可保持一致。
                if (Item.GlobalItems == null)
                {
                    try
                    {
                        Item.SetupItems();
                    }
                    catch (Exception e)
                    {
                        QuickMenuPlugin.Log?.LogWarning($"初始化物品表失败：{e.Message}");
                    }
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (GameObject prefab in Resources.LoadAll<GameObject>(string.Empty))
                {
                    if (prefab == null)
                    {
                        continue;
                    }

                    var item = prefab.GetComponent<Item>();
                    if (item == null)
                    {
                        continue;
                    }

                    string id = string.IsNullOrEmpty(item.id) ? prefab.name : item.id;
                    if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    {
                        continue;
                    }

                    var renderer = prefab.GetComponent<SpriteRenderer>();
                    Entries.Add(new ItemEntry
                    {
                        Id = id,
                        Icon = renderer != null ? renderer.sprite : null,
                        Category = ResolveCategory(id)
                    });
                }

                Entries.Sort((a, b) => string.Compare(
                    DisplayName(a.Id), DisplayName(b.Id), StringComparison.CurrentCultureIgnoreCase));

                QuickMenuPlugin.Log?.LogInfo($"物品目录已建立：{Entries.Count} 项。");
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"扫描物品表失败：{e.Message}");
            }
        }

        /// <summary>游戏在启动时会把 Localized 名称写入 ItemInfo.fullName；未就绪时退回 Locale 查询。</summary>
        internal static string DisplayName(string id)
        {
            try
            {
                var map = Item.GlobalItems;
                if (map != null && map.TryGetValue(id, out ItemInfo info) && info != null
                    && !string.IsNullOrEmpty(info.fullName))
                {
                    return info.fullName;
                }
            }
            catch
            {
                // GlobalItems 尚未初始化时忽略。
            }

            string localized = Locale.GetItem(id);
            return string.IsNullOrEmpty(localized) ? id : localized;
        }

        internal static string DisplayDescription(string id)
        {
            try
            {
                var map = Item.GlobalItems;
                if (map != null && map.TryGetValue(id, out ItemInfo info) && info != null
                    && !string.IsNullOrEmpty(info.description))
                {
                    return info.description;
                }
            }
            catch
            {
            }

            string localized = Locale.GetItem(id + "dsc");
            return string.IsNullOrEmpty(localized) || localized == id + "dsc" ? null : localized;
        }

        private static string ResolveCategory(string id)
        {
            try
            {
                var map = Item.GlobalItems;
                if (map != null && map.TryGetValue(id, out ItemInfo info) && info != null
                    && !string.IsNullOrEmpty(info.category))
                {
                    return info.category;
                }
            }
            catch
            {
            }

            return null;
        }

        internal static bool IsChinese
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(Locale.currentLangName))
                    {
                        return Locale.currentLangName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    }

                    var lang = Locale.currentLang;
                    return lang?.name != null && lang.name.Contains("简");
                }
                catch
                {
                    return true;
                }
            }
        }

        internal static string CategoryLabel(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return IsChinese ? "其他" : "Misc";
            }

            if (CategoryNames.TryGetValue(category, out string[] names))
            {
                return IsChinese ? names[0] : names[1];
            }

            return category;
        }
    }
}
