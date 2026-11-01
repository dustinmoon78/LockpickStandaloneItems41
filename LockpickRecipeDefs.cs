// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace LockpickStandaloneItems;

/// <summary>
/// 万能钥匙 + 骇入装置的工作台配方（2026-10-13 自独立模组 LockpickStandaloneItems41 的
/// Resources/Crafting.json 并入本 mod）。
///
/// 【为什么代码构造而不是读 JSON】
/// HideoutProduction.AreaType 是枚举 HideoutAreas，而 Requirement.AreaType 是 int?，
/// 两种类型混在同一份 JSON 里 SPT 转换器处理不了（源模组就是踩这个坑才需要额外分发一个
/// Resources/Crafting.json）。直接构造 record 没有这层不确定性，
/// 也少一个必须随包分发的数据文件。与 RaidRepairRecipes 同一套写法。
///
/// 【材料与工具（照原 Crafting.json 原样搬运，脚本查 items.json 得名）】
///   工具位：精英钳子 item_barter_tools_pliers_elite（5af04b6486f774195a3ebb49）
///           —— 钳子专供撬锁线，维修套件配方那边用的是螺丝刀，两边不抢同一个工具位。
///   万能钥匙主料：CPU（573477e124597737dd42e191）×1
///   骇入装置主料：金色 1G 手机 item_barter_valuable_1gphone（5bc9b720d4351e450201234b）×1
///     （也正是这件物品的模型来源 —— 同一件东西当主料与当模型，省一次额外的模型依赖）
///
/// 【两条都在工作台】
/// 两条都挂在「工作台」上（AreaType 恒为 Workbench=12，配方本身不分级）；分档由 Requirement.RequiredLevel 表达：
/// 万能钥匙要 1 级（900 秒），骇入装置要 2 级（1800 秒）。
/// ★别再写更高等级：工作台（areaType=10）只有 4 个 stage，最高 3 级，写过 12 玩家永远做不出来。
/// </summary>
internal static class LockpickRecipeDefs
{
    // 工具位：精英钳子
    private const string PliersTpl = "5af04b6486f774195a3ebb49";   // 精英钳子
    // 材料
    private const string CpuTpl = "573477e124597737dd42e191";       // CPU
    private const string GoldPhoneTpl = "5bc9b720d4351e450201234b";  // 金色 1G 手机（骇入装置的模型来源）

    public static void Inject<TLog>(HideoutTable hideoutTable, ISptLogger<TLog> logger, bool endProductReady)
    {
        try
        {
            // 两件物品没注入成功就别写配方 —— 否则工作台里会出现两条永远搓不出来的死配方。
            if (!endProductReady)
            {
                logger.Error("[万能钥匙物品包] 物品未注入成功, 配方一并跳过（避免工作台出现搓不出来的死配方）。", null);
                return;
            }

            var recipes = new List<HideoutProduction>
            {
                // 万能钥匙：工作台 RequiredLevel=1，精英钳子 + CPU ×1，900 秒
                Build("6a1f01aa01bb02cc03dd1001", LockpickItemDefs.LockpickTpl, 900,
                    new List<Requirement>
                    {
                        Area(1),
                        Tool(PliersTpl),
                        Item(CpuTpl, 1),
                    }),
                // 骇入装置：工作台 RequiredLevel=2（官方最高就 3 级，留一级余量不卡满级门槛），精英钳子 + 金色 1G 手机 ×1，1800 秒
                Build("6a1f01aa01bb02cc03dd1002", LockpickItemDefs.HackDeviceTpl, 1800,
                    new List<Requirement>
                    {
                        Area(2),
                        Tool(PliersTpl),
                        Item(GoldPhoneTpl, 1),
                    }),
            };
            foreach (HideoutProduction recipe in recipes)
            {
                // 幂等：同 id 已在表里（刻印者先注过、或本包上一轮注过）就跳过，
                // 免得工作台里出现两条一模一样的配方。这也是守卫万一失配时的兜底。
                if (hideoutTable.Production.Recipes.Any(r => r != null && r.Id == recipe.Id))
                {
                    continue;
                }
                hideoutTable.Production.Recipes.Add(recipe);
            }
            logger.Info("[万能钥匙物品包] 配方: 已加入工作台配方 " + recipes.Count
                + " 条（万能钥匙一级工作台 900 秒 / 骇入装置二级工作台 1800 秒）。", null);
        }
        catch (Exception ex)
        {
            logger.Error("[万能钥匙物品包] 配方: 注入失败 " + ex.Message, null);
        }
    }

    private static HideoutProduction Build(string id, string endProduct, int seconds, List<Requirement> reqs)
    {
        return new HideoutProduction
        {
            Id = new MongoId(id),
            AreaType = HideoutAreas.Workbench,
            Requirements = reqs,
            ProductionTime = seconds,
            EndProduct = new MongoId(endProduct),
            IsEncoded = false,
            Locked = false,
            NeedFuelForAllProductionTime = true,
            Continuous = false,
            Count = 1,
            ProductionLimitCount = 0,
        };
    }

    private static Requirement Area(int requiredLevel)
    {
        return new Requirement { AreaType = (int)HideoutAreas.Workbench, RequiredLevel = requiredLevel, Type = "Area" };
    }

    private static Requirement Tool(string tpl)
    {
        return new Requirement { TemplateId = new MongoId(tpl), Type = "Tool" };
    }

    private static Requirement Item(string tpl, int count)
    {
        return new Requirement { TemplateId = new MongoId(tpl), Count = count, IsFunctional = false, IsEncoded = false, Type = "Item" };
    }
}
