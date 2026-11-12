using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 快捷菜单入口：热键呼出，集中执行游戏内置控制台命令。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("CasualtiesUnknown.exe")]
    public sealed class QuickMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.casualties.quickmenu";
        public const string PluginName = "Casualties Quick Menu";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; }
        internal static QuickMenuPlugin Instance { get; private set; }

        internal static ConfigEntry<KeyCode> Hotkey { get; private set; }
        internal static ConfigEntry<bool> BlockGameInput { get; private set; }
        internal static ConfigEntry<bool> SuppressNativePopup { get; private set; }
        internal static ConfigEntry<bool> CloseAfterExecute { get; private set; }
        internal static ConfigEntry<bool> ConfirmRisky { get; private set; }
        internal static ConfigEntry<string> Favorites { get; private set; }
        internal static ConfigEntry<float> UiScale { get; private set; }
        internal static ConfigEntry<float> Opacity { get; private set; }
        internal static ConfigEntry<bool> DimBackground { get; private set; }
        internal static ConfigEntry<float> WindowWidth { get; private set; }
        internal static ConfigEntry<float> WindowHeight { get; private set; }
        internal static ConfigEntry<float> WindowX { get; private set; }
        internal static ConfigEntry<float> WindowY { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Hotkey = Config.Bind("General", "Hotkey", KeyCode.F8,
                "呼出/关闭快捷菜单的按键。默认 F8；游戏原生控制台是反引号键。");
            BlockGameInput = Config.Bind("General", "BlockGameInput", true,
                "菜单打开时屏蔽角色操作（移动、攻击、使用物品）。");
            SuppressNativePopup = Config.Bind("General", "SuppressNativeConsolePopup", true,
                "菜单打开时，阻止原生控制台因作弊警告被自动弹出，避免两个界面争抢键盘。");
            CloseAfterExecute = Config.Bind("General", "CloseAfterExecute", false,
                "执行任意命令后自动关闭菜单。");
            ConfirmRisky = Config.Bind("General", "ConfirmRiskyCommands", true,
                "对击杀、重置技能、保存退出等破坏性命令要求 3 秒内二次确认。");
            Favorites = Config.Bind("General", "Favorites", string.Empty,
                "收藏的命令名，逗号分隔。由界面自动维护，无需手动编辑。");
            UiScale = Config.Bind("Interface", "UiScale", 1.0f,
                new ConfigDescription("界面缩放系数。", new AcceptableValueRange<float>(0.6f, 2.0f)));

            Opacity = Config.Bind("Interface", "Opacity", 0.92f,
                new ConfigDescription("面板不透明度。1 为完全不透明，越低越能看清背后的游戏画面。",
                    new AcceptableValueRange<float>(0.3f, 1.0f)));

            DimBackground = Config.Bind("Interface", "DimBackground", true,
                "是否在菜单背后压暗整个屏幕。关闭后背景游戏画面不受影响。");

            WindowWidth = Config.Bind("Interface", "WindowWidth", 1020f,
                "窗口宽度（设计单位）。也可以直接在界面右下角拖动调整，松手后自动保存。");
            WindowHeight = Config.Bind("Interface", "WindowHeight", 690f,
                "窗口高度（设计单位）。");
            WindowX = Config.Bind("Interface", "WindowX", -1f,
                "窗口左上角 X 坐标；-1 表示自动居中。拖动窗口后自动保存。");
            WindowY = Config.Bind("Interface", "WindowY", -1f,
                "窗口左上角 Y 坐标；-1 表示自动居中。");

            CommandListPage.Instance.LoadFavorites();

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(QuickMenuPlugin).Assembly);
                Log.LogInfo("Harmony 补丁已应用。");
            }
            catch (Exception e)
            {
                // 补丁失败时菜单仍可用，只是不屏蔽游戏输入。
                Log.LogError($"Harmony 补丁应用失败，降级运行：{e}");
            }

            Log.LogInfo($"{PluginName} {PluginVersion} 已加载，热键 {Hotkey.Value}。");
        }

        private void Update()
        {
            if (Hotkey.Value != KeyCode.None && Input.GetKeyDown(Hotkey.Value))
            {
                QuickMenuState.Toggle();
            }

            if (!QuickMenuState.IsOpen)
            {
                return;
            }

            // 菜单打开时 PlayerCamera.HandleInput 被跳过，因此 Esc 不会再触发游戏暂停。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuickMenuState.Close();
                return;
            }

            if (!ConsoleBridge.IsReady)
            {
                ConsoleBridge.EnsureConsole();
            }
        }

        private void OnGUI()
        {
            if (!QuickMenuState.IsOpen)
            {
                return;
            }

            QuickMenuUI.Draw();
        }

        private void OnDestroy()
        {
            QuickMenuState.Close();
            _harmony?.UnpatchSelf();
            Log.LogInfo("插件已卸载。");
        }
    }
}
