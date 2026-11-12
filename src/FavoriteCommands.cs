using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 收藏命令的共享状态。列表页与收藏页共用同一份数据，
    /// 变更后立即写入 BepInEx 配置，避免两个页面不同步。
    /// </summary>
    internal static class FavoriteCommands
    {
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static int Count
        {
            get { return Names.Count; }
        }

        internal static bool Contains(string name)
        {
            return !string.IsNullOrEmpty(name) && Names.Contains(name);
        }

        internal static void Load()
        {
            Names.Clear();

            string raw = QuickMenuPlugin.Favorites?.Value;
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (string part in raw.Split(','))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    Names.Add(part.Trim());
                }
            }
        }

        /// <summary>返回 true 表示本次操作添加了收藏，false 表示取消了收藏。</summary>
        internal static bool Toggle(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            bool added = Names.Add(name);
            if (!added)
            {
                Names.Remove(name);
            }

            Save();
            return added;
        }

        internal static void Remove(string name)
        {
            if (Names.Remove(name))
            {
                Save();
            }
        }

        internal static void Clear()
        {
            if (Names.Count == 0)
            {
                return;
            }

            Names.Clear();
            Save();
        }

        /// <summary>按名称排序的快照，供页面遍历时避免修改集合。</summary>
        internal static List<string> Snapshot()
        {
            return Names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static void Save()
        {
            if (QuickMenuPlugin.Favorites == null)
            {
                return;
            }

            QuickMenuPlugin.Favorites.Value = string.Join(",", Names.ToArray());
            QuickMenuPlugin.Instance?.Config.Save();
        }
    }
}
