// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace LockpickStandaloneItems;

/// <summary>
/// 补开锁技能的本地化文案（2026-10-13 自独立模组 LockpickStandaloneItems41 并入本 mod）。
///
/// 背景：原版 global locale 表里有 "LockpickingDescription"（技能说明），
/// 但**没有** "LockpickingLevelingUpDescription"（技能升级方式说明）。
/// 客户端 EFT.UI.SkillTooltip 会把 Id 拼成这两个键去查表，
/// 缺键时就把裸键名直接显示在技能面板的说明行上。
///
/// 本类把该键补进 LocaleTable.Global 的每一个语言。
/// Global 里的值是 LazyLoad（首次读取时才反序列化），
/// 所以必须用 AddTransformer 登记改写器，而不能直接改字典
/// —— 直接改会落到一个还没反序列化的实例上、且会被丢弃。
/// 已有的键不覆盖，以 locales 文件为准。
/// </summary>
internal static class LockpickSkillLocale
{
    private const string Key = "LockpickingLevelingUpDescription";

    /// <summary>按语言给文案；表里没有的语言一律回落英文（与原版 SPT 的兜底口径一致）。</summary>
    private static readonly Dictionary<string, string> Texts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "en", "The Lockpicking skill is improved by picking locks." },
            { "ch", "开锁技能可以在撬开门锁时得到提升。" },
        };

    public static void Inject<TLog>(LocaleTable localeTable, ISptLogger<TLog> logger)
    {
        if (localeTable?.Global == null)
        {
            logger.Error("[万能钥匙物品包]: locale 表不可用, 开锁技能说明文案未登记。", null);
            return;
        }

        try
        {
            int injected = 0;
            foreach (var pair in localeTable.Global)
            {
                string language = pair.Key;
                string text = Texts.TryGetValue(language, out string own) ? own : Texts["en"];
                pair.Value.AddTransformer(dict =>
                {
                    if (dict == null)
                    {
                        return dict;
                    }
                    if (!dict.ContainsKey(Key))
                    {
                        dict[Key] = text;
                    }
                    return dict;
                });
                injected++;
            }

            logger.Info("[万能钥匙物品包]: 开锁技能说明文案已登记, 覆盖 " + injected
                + " 种语言（键已存在的语言不覆盖）。", null);
        }
        catch (Exception ex)
        {
            logger.Error("[万能钥匙物品包]: 补开锁技能说明文案失败: " + ex.Message, null);
        }
    }
}
