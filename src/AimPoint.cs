using System;
using System.Globalization;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    /// <summary>
    /// 光标位置快照。
    ///
    /// 游戏的 `tp`、`spawn ... cursor` 等命令在**执行瞬间**读取 Input.mousePosition，
    /// 而玩家点菜单按钮时光标正压在按钮上，取到的自然是界面里的坐标。
    /// 因此在菜单打开的那一刻把世界坐标记下来，执行时用显式坐标替换 cursor。
    /// </summary>
    internal static class AimPoint
    {
        private static bool _has;
        private static Vector2 _world;

        internal static bool Has
        {
            get { return _has; }
        }

        internal static Vector2 World
        {
            get { return _world; }
        }

        /// <summary>在菜单打开、输入尚未被屏蔽时调用。</summary>
        internal static void Capture()
        {
            _has = false;

            try
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                Vector3 point = camera.ScreenToWorldPoint(Input.mousePosition);
                _world = new Vector2(point.x, point.y);
                _has = true;
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"记录光标位置失败：{e.Message}");
            }
        }

        /// <summary>把位置参数里的 cursor 换成快照坐标；其它取值（player/random/#,#）原样返回。</summary>
        internal static string Resolve(string token)
        {
            if (!string.Equals(token, "cursor", StringComparison.OrdinalIgnoreCase) || !_has)
            {
                return token;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##},{1:0.##}", _world.x, _world.y);
        }

        internal static string Describe()
        {
            if (!_has)
            {
                return "未取到光标位置";
            }

            return string.Format(CultureInfo.InvariantCulture, "目标 {0:0.#}, {1:0.#}", _world.x, _world.y);
        }
    }
}
