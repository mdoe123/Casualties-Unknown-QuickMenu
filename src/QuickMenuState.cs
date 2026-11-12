using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 菜单开合状态与跨页面共享的提示信息。
    /// 刻意不修改 Time.timeScale 与光标：本作使用常驻可见的自由光标，
    /// 暂停与输入屏蔽分别由 PlayerCamera.HandleInput 补丁和游戏原生控制台逻辑处理。
    /// </summary>
    internal static class QuickMenuState
    {
        private static bool _open;

        internal static bool IsOpen => _open;

        internal static string LastMessage { get; private set; }

        internal static float LastMessageTime { get; private set; }

        internal static void Toggle()
        {
            if (_open)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        internal static void Open()
        {
            if (_open)
            {
                return;
            }

            _open = true;

            ConsoleBridge.EnsureConsole();

            // 原生控制台与快捷菜单同时开启会争抢键盘输入，先让原生控制台收起。
            var inst = ConsoleScript.instance;
            if (inst != null && inst.active)
            {
                inst.active = false;
            }

            CommandListPage.Instance.OnMenuOpened();
            Notify("菜单已打开。使用命令会禁用成就，直到重启游戏。");
        }

        internal static void Close()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
        }

        internal static void Notify(string message)
        {
            LastMessage = message;
            LastMessageTime = Time.realtimeSinceStartup;
        }

        internal static string CurrentMessage(float lifetimeSeconds)
        {
            if (string.IsNullOrEmpty(LastMessage))
            {
                return null;
            }

            if (Time.realtimeSinceStartup - LastMessageTime > lifetimeSeconds)
            {
                return null;
            }

            return LastMessage;
        }
    }
}
