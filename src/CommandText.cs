using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 命令文本解析层：只服务本 Mod 的界面显示。
    /// 绝不修改 Command 对象本身——原生控制台、其它 Mod 与 Command.GetShortDescription()
    /// 都依赖原始英文字段，改动会变成全局副作用。
    /// </summary>
    internal static class CommandText
    {
        private sealed class Entry
        {
            internal string Name;
            internal string Description;
            internal string[] Args;
        }

        private static readonly Dictionary<string, Entry> Map =
            new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        private static bool _loaded;

        /// <summary>用户可编辑的词典路径。</summary>
        internal static string UserFilePath
        {
            get { return Path.Combine(Paths.ConfigPath, "QuickMenu", "commands.zh-CN.json"); }
        }

        /// <summary>已收录的条目数，用于设置页展示。</summary>
        internal static int EntryCount
        {
            get { return Map.Count; }
        }

        internal static void Load()
        {
            Map.Clear();

            string embedded = ReadEmbedded();
            Apply(embedded, "内置词典");
            ApplyUserFile(embedded);

            _loaded = true;
            QuickMenuPlugin.Log?.LogInfo($"命令词典已加载：{Map.Count} 条（{UserFilePath}）");
        }

        internal static void Reload()
        {
            Load();
        }

        private static void ApplyUserFile(string embedded)
        {
            string path = UserFilePath;

            try
            {
                if (!File.Exists(path))
                {
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (!string.IsNullOrEmpty(embedded))
                    {
                        // 首次运行释放一份可编辑的副本，方便玩家自行补充。
                        File.WriteAllText(path, embedded, new UTF8Encoding(false));
                    }

                    return;
                }

                Apply(File.ReadAllText(path, Encoding.UTF8), "用户词典");
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"读取命令词典失败，将使用内置版本：{e.Message}");
            }
        }

        private static void Apply(string json, string source)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                JObject root = JObject.Parse(json);
                if (!(root["commands"] is JObject commands))
                {
                    return;
                }

                foreach (JProperty property in commands.Properties())
                {
                    if (!(property.Value is JObject node))
                    {
                        continue;
                    }

                    var entry = new Entry
                    {
                        Name = (string)node["name"],
                        Description = (string)node["description"]
                    };

                    if (node["args"] is JArray args)
                    {
                        entry.Args = new string[args.Count];
                        for (int i = 0; i < args.Count; i++)
                        {
                            entry.Args[i] = args[i]?.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(entry.Name) || !string.IsNullOrEmpty(entry.Description))
                    {
                        // 用户词典逐条覆盖内置词典，未覆盖的条目继续沿用内置译文。
                        Map[property.Name] = entry;
                    }
                }
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"{source}解析失败：{e.Message}");
            }
        }

        private static string ReadEmbedded()
        {
            try
            {
                Assembly assembly = typeof(CommandText).Assembly;
                foreach (string name in assembly.GetManifestResourceNames())
                {
                    if (!name.EndsWith("commands.zh-CN.json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    {
                        if (stream == null)
                        {
                            continue;
                        }

                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"内置命令词典读取失败：{e.Message}");
            }

            return null;
        }

        private static Entry Find(Command command)
        {
            if (command == null || string.IsNullOrEmpty(command.name))
            {
                return null;
            }

            if (!_loaded)
            {
                Load();
            }

            return Map.TryGetValue(command.name, out Entry entry) ? entry : null;
        }

        internal static bool Has(Command command)
        {
            return Find(command) != null;
        }

        /// <summary>列表里显示的短名：有译文用中文，否则回退原始命令名。</summary>
        internal static string Name(Command command)
        {
            Entry entry = Find(command);
            return entry != null && !string.IsNullOrEmpty(entry.Name) ? entry.Name : command?.name;
        }

        /// <summary>详情标题：中文名 · 英文 ID，两者都保留便于复制与查错。</summary>
        internal static string Display(Command command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            Entry entry = Find(command);
            if (entry == null || string.IsNullOrEmpty(entry.Name) || entry.Name == command.name)
            {
                return command.name;
            }

            return entry.Name + " · " + command.name;
        }

        internal static string Description(Command command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            Entry entry = Find(command);
            return entry != null && !string.IsNullOrEmpty(entry.Description)
                ? entry.Description
                : command.description;
        }

        /// <summary>参数标签；词典没覆盖到的参数回退游戏原始短描述。</summary>
        internal static string ArgLabel(Command command, int index)
        {
            if (command == null || command.argDescription == null || index >= command.argDescription.Length)
            {
                return string.Empty;
            }

            Entry entry = Find(command);
            if (entry?.Args != null && index < entry.Args.Length && !string.IsNullOrEmpty(entry.Args[index]))
            {
                return entry.Args[index];
            }

            return command.argDescription[index].shortDesc;
        }

        /// <summary>搜索用的拼接串，让中英文都能命中。</summary>
        internal static string SearchBlob(Command command)
        {
            if (command == null)
            {
                return string.Empty;
            }

            Entry entry = Find(command);
            var builder = new StringBuilder(command.name);

            if (entry != null)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    builder.Append(' ').Append(entry.Name);
                }

                if (!string.IsNullOrEmpty(entry.Description))
                {
                    builder.Append(' ').Append(entry.Description);
                }
            }

            if (!string.IsNullOrEmpty(command.description))
            {
                builder.Append(' ').Append(command.description);
            }

            return builder.ToString();
        }
    }
}
