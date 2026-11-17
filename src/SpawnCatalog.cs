using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesQuickMenu
{
    internal sealed class SpawnEntity
    {
        internal GameObject Prefab;
        internal string Name;
        internal Sprite Icon;

        /// <summary>大于等于 0 时表示这是可玩角色，必须走 SpawnCharacter 而非直接实例化。</summary>
        internal int CharacterIndex = -1;
    }

    internal struct BlockEntry
    {
        internal int Id;
        internal string Name;
    }

    internal struct FluidEntry
    {
        internal int Id;
        internal string Name;
        internal Color Color;
    }

    /// <summary>
    /// 「生成」页的数据源与放置动作。
    ///
    /// 三类通道各不相同：
    ///   建筑 / 陷阱 —— Resources 里的预制体，脚本直接实例化（spawn 命令也只是 Resources.Load + 实例化）
    ///   物块       —— 没有控制台命令，直接写 WorldGeneration.world.SetBlock
    ///   流体       —— 没有控制台命令，直接调 FluidManager.SetLiquid（区域填充才用 floodfill）
    ///   生物       —— spawncharacter 只认 0..2 三个硬编码索引
    /// </summary>
    internal static class SpawnCatalog
    {
        internal static readonly List<SpawnEntity> Buildings = new List<SpawnEntity>();
        internal static readonly List<SpawnEntity> Traps = new List<SpawnEntity>();
        internal static readonly List<SpawnEntity> Creatures = new List<SpawnEntity>();
        internal static readonly List<BlockEntry> Blocks = new List<BlockEntry>();
        internal static readonly List<FluidEntry> Fluids = new List<FluidEntry>();

        // SpawnCharacter 的三个硬编码索引，对应预制体路径固定。
        private static readonly string[] CharacterPaths =
        {
            "Special/Characters/Experiment", "Special/Characters/Milky", "Special/Characters/Dune"
        };

        private static readonly string[] CharacterNames = { "Experiment", "Milky", "Dune" };

        private static bool _entitiesScanned;
        private static bool _blocksScanned;
        private static bool _fluidsScanned;

        internal static void EnsureEntities()
        {
            if (_entitiesScanned)
            {
                return;
            }

            _entitiesScanned = true;

            GameObject[] all;
            try
            {
                all = Resources.LoadAll<GameObject>(string.Empty);
            }
            catch (Exception e)
            {
                QuickMenuPlugin.Log?.LogWarning($"扫描建筑预制体失败：{e.Message}");
                return;
            }

            foreach (GameObject prefab in all)
            {
                if (prefab == null)
                {
                    continue;
                }

                if (!prefab.TryGetComponent(out BuildingEntity building))
                {
                    continue;
                }

                // 建筑名在 buildings 分表里，键是 BuildingEntity.id（不是预制体名）。
                string key = string.IsNullOrEmpty(building.id) ? prefab.name : building.id;
                var entry = new SpawnEntity
                {
                    Prefab = prefab,
                    Name = LocalizedName(key, true),
                    Icon = FindIcon(prefab)
                };

                bool trap = IsTrap(prefab, building);
                if (trap)
                {
                    Traps.Add(entry);
                }
                else if (building.animal)
                {
                    // animal 是游戏自己的标记：有生命、可流血的生物。
                    Creatures.Add(entry);
                }
                else
                {
                    Buildings.Add(entry);
                }
            }

            for (int i = 0; i < CharacterPaths.Length; i++)
            {
                GameObject prefab = null;
                try
                {
                    prefab = Resources.Load<GameObject>(CharacterPaths[i]);
                }
                catch
                {
                    // 路径变了就跳过，不影响其它分类。
                }

                Creatures.Add(new SpawnEntity
                {
                    Prefab = prefab,
                    Name = CharacterNames[i],
                    Icon = prefab != null ? FindIcon(prefab) : null,
                    CharacterIndex = i
                });
            }

            Sort(Buildings);
            Sort(Traps);
            Sort(Creatures);

            QuickMenuPlugin.Log?.LogInfo(
                $"可生成建筑 {Buildings.Count} 个、陷阱 {Traps.Count} 个、生物 {Creatures.Count} 个。");
        }

        /// <summary>
        /// 一部分陷阱没有专属脚本（冰锥、仙人掌、玻璃碎片这类静态危害物），
        /// 只能按建筑 id 列白名单。键取自语言文件的 buildings 分表。
        /// </summary>
        private static readonly HashSet<string> TrapWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cactus", "icestalactite", "stalactite", "spentfuel", "radbarrel",
            "glassshards", "skullcrusher", "sidestabber", "coil",
            "mushroomdropperpayload", "soundcannon", "bananaplant", "lavageyser",
            "piston", "jumppad", "bouncecap", "bloodsuckerden"
        };

        /// <summary>
        /// 游戏没有统一的「陷阱」标记，这里按会造成伤害的脚本组件 + 白名单归类。
        /// </summary>
        private static bool IsTrap(GameObject prefab, BuildingEntity building)
        {
            if (TrapWhitelist.Contains(building.id ?? string.Empty) || TrapWhitelist.Contains(prefab.name))
            {
                return true;
            }

            return prefab.GetComponent<BearTrap>() != null
                   || prefab.GetComponent<FanTrap>() != null
                   || prefab.GetComponent<MineScript>() != null
                   || prefab.GetComponent<GunmineScript>() != null
                   || prefab.GetComponent<SawbladeScript>() != null
                   || prefab.GetComponent<SpikeStabberScript>() != null
                   || prefab.GetComponent<GeyserScript>() != null
                   || prefab.GetComponent<TurretScript>() != null
                   || prefab.GetComponent<CeilingTurret>() != null
                   || prefab.GetComponent<BarbedFence>() != null;
        }

        private static void Sort(List<SpawnEntity> list)
        {
            // 三个可玩角色排在生物列表最前，其余按名称排序。
            list.Sort((a, b) =>
            {
                bool ca = a.CharacterIndex >= 0;
                bool cb = b.CharacterIndex >= 0;
                if (ca != cb)
                {
                    return ca ? -1 : 1;
                }

                if (ca)
                {
                    return a.CharacterIndex.CompareTo(b.CharacterIndex);
                }

                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        internal static void EnsureBlocks()
        {
            if (_blocksScanned || WorldGeneration.world == null)
            {
                return;
            }

            _blocksScanned = true;

            for (ushort id = 0; id < 64; id++)
            {
                BlockInfo info;
                try
                {
                    info = WorldGeneration.world.GetBlockInfo(id);
                }
                catch
                {
                    break;
                }

                if (info == null || string.IsNullOrEmpty(info.name))
                {
                    break;
                }

                Blocks.Add(new BlockEntry { Id = id, Name = info.name });
            }
        }

        internal static void EnsureFluids()
        {
            if (_fluidsScanned)
            {
                return;
            }

            _fluidsScanned = true;

            // 类型编号与名称来自 FluidManager.LiquidName 的 switch；0 表示无液体。
            AddFluid(1, "water");
            AddFluid(2, "lumalgae");
            AddFluid(3, "oil");
            AddFluid(4, "sap");
            AddFluid(5, "dirtywater");
            AddFluid(6, "magma");
            AddFluid(7, "redsludge");
        }

        private static void AddFluid(int id, string key)
        {
            Color color = Color.gray;
            try
            {
                Color[] colors = FluidManager.main != null ? FluidManager.main.liquidColors : null;
                if (colors != null && id < colors.Length)
                {
                    color = colors[id];
                }
            }
            catch
            {
                // 取不到颜色就退回灰色，不影响放置。
            }

            Fluids.Add(new FluidEntry
            {
                Id = id,
                Name = LocalizedName(key, false),
                Color = color
            });
        }

        private static string LocalizedName(string key, bool building)
        {
            try
            {
                string localized = building ? Locale.GetBuilding(key) : Locale.GetOther(key);
                if (!string.IsNullOrEmpty(localized) && !string.Equals(localized, key, StringComparison.OrdinalIgnoreCase))
                {
                    return localized;
                }
            }
            catch
            {
                // 本地化表未就绪时直接用键名。
            }

            return key;
        }

        private static Sprite FindIcon(GameObject prefab)
        {
            try
            {
                var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                return renderer != null ? renderer.sprite : null;
            }
            catch
            {
                return null;
            }
        }

        // ---------- 放置动作 ----------

        internal static bool PlaceBuilding(GameObject prefab, Vector2 position, out string error)
        {
            error = null;

            if (WorldGeneration.world == null || prefab == null)
            {
                error = "尚未进入对局";
                return false;
            }

            try
            {
                GameObject spawned = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
                if (spawned != null && spawned.TryGetComponent(out BuildingEntity building))
                {
                    // 与 spawn 命令保持一致：记录建筑落在的方块。
                    building.blockPlacedOn = WorldGeneration.world.WorldToBlockPos(position + Vector2.down);
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        internal static bool PlaceBlock(int blockId, Vector2 position, out string error)
        {
            error = null;

            if (WorldGeneration.world == null)
            {
                error = "尚未进入对局";
                return false;
            }

            try
            {
                // SetBlock 内部会夹到世界边界并刷新所在区块，不做支撑校验，
                // 因此这里的写入是不可撤销的。
                WorldGeneration.world.SetBlock(
                    WorldGeneration.world.WorldToBlockPos(position), (ushort)blockId);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        internal static bool PlaceFluid(int type, Vector2 position, bool flood, out string error)
        {
            error = null;

            if (WorldGeneration.world == null || FluidManager.main == null)
            {
                error = "尚未进入对局";
                return false;
            }

            try
            {
                if (flood)
                {
                    FluidManager.main.StartFill(
                        WorldGeneration.world.WorldToBlockPos(position), (byte)type, 2048);
                }
                else
                {
                    FluidManager.main.SetLiquid(position, (byte)type);
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        internal static bool PlaceCharacter(int index, Vector2 position, out string error)
        {
            error = null;

            if (PlayerCamera.main == null)
            {
                error = "尚未进入对局";
                return false;
            }

            try
            {
                GameObject character = PlayerCamera.main.SpawnCharacter(index);
                if (character == null)
                {
                    error = "该角色无法生成";
                    return false;
                }

                character.transform.position = position;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
