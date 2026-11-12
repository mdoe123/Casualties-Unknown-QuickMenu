using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 对游戏内置控制台（ConsoleScript）的唯一访问层。
    /// 所有可能因游戏更新而失效的调用都收敛在这里，失败时降级而非抛出。
    /// </summary>
    internal static class ConsoleBridge
    {
        // 控制台日志带 TMP 富文本标记（<color=...>、<alpha=...>），IMGUI 不解析，需要剥离。
        private static readonly Regex RichTextTag = new Regex("<[^>]{1,64}>", RegexOptions.Compiled);

        private static readonly List<Command> EmptyCommands = new List<Command>();

        private static float _lastEnsureAttempt = -10f;

        internal static ConsoleScript Instance => ConsoleScript.instance;

        /// <summary>控制台对象存在、日志列表已初始化、命令表已注册。</summary>
        internal static bool IsReady
        {
            get
            {
                var inst = ConsoleScript.instance;
                return inst != null
                       && inst.logs != null
                       && ConsoleScript.Commands != null
                       && ConsoleScript.Commands.Count > 0;
            }
        }

        internal static List<Command> Commands
        {
            get
            {
                var list = ConsoleScript.Commands;
                return list ?? EmptyCommands;
            }
        }

        /// <summary>按需实例化控制台。游戏在 PreRunScript.Awake 中也会调用，因此不会重复创建。</summary>
        internal static void EnsureConsole()
        {
            if (ConsoleScript.instance != null)
            {
                return;
            }

            // 失败时避免每帧重试刷屏。
            if (UnityEngine.Time.realtimeSinceStartup - _lastEnsureAttempt < 1f)
            {
                return;
            }

            _lastEnsureAttempt = UnityEngine.Time.realtimeSinceStartup;

            try
            {
                ConsoleScript.CheckForConsole();
                QuickMenuPlugin.Log?.LogInfo("已按需创建控制台实例。");
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"创建控制台失败：{e.Message}");
            }
        }

        /// <summary>
        /// 执行一条完整命令行，与在原生控制台输入并回车等价。
        /// 注意：命令自身的异常由游戏捕获并写入控制台日志，因此返回 true 不代表命令成功。
        /// </summary>
        internal static bool TryExecute(string line, out string error)
        {
            error = null;

            var inst = ConsoleScript.instance;
            if (inst == null || inst.logs == null)
            {
                error = "控制台尚未初始化，请稍后重试。";
                return false;
            }

            try
            {
                inst.ExecuteCommand(line);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>最近 outError 判定：若命令内部执行失败，游戏会把错误写进日志。</summary>
        internal static string LastLogLine()
        {
            var inst = ConsoleScript.instance;
            if (inst?.logs == null || inst.logs.Count == 0)
            {
                return null;
            }

            return Plain(inst.logs[inst.logs.Count - 1]);
        }

        internal static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return RichTextTag.Replace(text, string.Empty);
        }

        /// <summary>按时间正序返回最近 count 条日志（已剥离富文本）。</summary>
        internal static List<string> RecentLogs(int count)
        {
            var result = new List<string>();
            var inst = ConsoleScript.instance;
            if (inst?.logs == null)
            {
                return result;
            }

            int start = Math.Max(0, inst.logs.Count - count);
            for (int i = start; i < inst.logs.Count; i++)
            {
                result.Add(Plain(inst.logs[i]));
            }

            return result;
        }

        internal static void ClearLogs()
        {
            ConsoleScript.instance?.logs?.Clear();
        }

        /// <summary>当前角色的肢体名，用于断肢/接回快捷操作。</summary>
        internal static List<string> LimbNames()
        {
            var names = new List<string>();

            try
            {
                var player = PlayerCamera.main;
                if (player != null && player.body != null && player.body.limbs != null)
                {
                    foreach (var limb in player.body.limbs)
                    {
                        if (limb != null)
                        {
                            names.Add(limb.name);
                        }
                    }
                }
            }
            catch
            {
                // 主菜单或过场期间可能没有 PlayerCamera，静默降级。
            }

            return names;
        }

        internal static bool HasWorld
        {
            get
            {
                try
                {
                    return PlayerCamera.main != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>读取某条命令的参数自动补全列表；不存在时返回空表而不是 null。</summary>
        internal static List<string> ArgAutofill(string commandName, int argIndex)
        {
            var result = new List<string>();

            try
            {
                var command = ConsoleScript.SearchExact(commandName);
                if (command?.argAutofill != null
                    && command.argAutofill.TryGetValue(argIndex, out var list)
                    && list != null)
                {
                    result.AddRange(list);
                }
            }
            catch
            {
                // 命令表尚未注册时 SearchExact 会返回 null，忽略即可。
            }

            return result;
        }

        private static bool _spawnAutofillRequested;

        /// <summary>spawn 命令的物品名补全需要游戏侧扫描 Resources，只做一次。</summary>
        internal static void EnsureSpawnAutofill()
        {
            if (_spawnAutofillRequested)
            {
                return;
            }

            if (ArgAutofill("spawn", 0).Count > 0)
            {
                _spawnAutofillRequested = true;
                return;
            }

            var inst = ConsoleScript.instance;
            if (inst == null)
            {
                // 控制台还没创建，下一帧再试，不置标志。
                return;
            }

            try
            {
                inst.RegisterSpawnEntities();
                _spawnAutofillRequested = true;
            }
            catch (Exception e)
            {
                _spawnAutofillRequested = true;
                QuickMenuPlugin.Log?.LogWarning($"构建物品补全列表失败：{e.Message}");
            }
        }

        internal static void OpenNativeConsole()
        {
            var inst = ConsoleScript.instance;
            if (inst == null)
            {
                return;
            }

            if (!inst.active)
            {
                inst.ToggleActiveState();
            }
        }
    }
}
