using System;
using HarmonyLib;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 菜单打开时跳过玩家的输入处理（移动、攻击、使用物品、暂停键）。
    /// 这是本作原生控制台采用的方式——ConsoleScript.active 为真时 HandleInput 也直接返回。
    /// </summary>
    [HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
    internal static class PlayerCameraHandleInputPatch
    {
        private static bool Prepare()
        {
            bool exists = AccessTools.Method(typeof(PlayerCamera), "HandleInput", Type.EmptyTypes) != null;
            if (!exists)
            {
                QuickMenuPlugin.Log?.LogWarning("未找到 PlayerCamera.HandleInput，菜单打开时将无法屏蔽角色操作。");
            }

            return exists;
        }

        private static bool Prefix()
        {
            return !QuickMenuState.IsOpen || !QuickMenuPlugin.BlockGameInput.Value;
        }
    }

    /// <summary>
    /// 作弊命令首次执行时，游戏会调用 Alert 弹出原生控制台。
    /// 菜单已经打开的情况下这会与我们的输入框抢键盘，因此把提示改投到菜单状态栏。
    /// </summary>
    [HarmonyPatch(typeof(ConsoleScript), "Alert")]
    internal static class ConsoleAlertPatch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(ConsoleScript), "Alert", new[] { typeof(string) }) != null;
        }

        private static bool Prefix(string __0)
        {
            if (!QuickMenuState.IsOpen || !QuickMenuPlugin.SuppressNativePopup.Value)
            {
                return true;
            }

            QuickMenuState.Notify(ConsoleBridge.Plain(__0));
            return false;
        }
    }
}
