// SPDX-License-Identifier: AGPL-3.0-or-later
using System.Collections.Generic;
using SemanticVersioning;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace LockpickStandaloneItems;

/// <summary>
/// 万能钥匙模组的自备工具物品包（服务端）。
/// 提供本模组自造的万能钥匙，使模组在没有任何其它开锁模组的环境里也能正常开锁。
/// </summary>
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.dsh.lockpickstandalone.items";

    public string Name { get; init; } = "万能钥匙物品包";

    public string Author { get; init; } = "dsh";

    public List<string> Contributors { get; init; }

    public Version Version { get; init; } = new("1.0.0", false);

    public Range SptVersion { get; init; } = new("~4.1.0", false);

    public bool HasPrepatcher { get; init; }

    public List<string> Incompatibilities { get; init; }

    public Dictionary<string, Range> ModDependencies { get; init; }

    public string Url { get; init; }

    // 2026-10-25：MIT → CC BY-NC-SA 4.0，与发帖文案、说明书、包内 LICENSE 三处统一
    // （同作者的 QuestTuner41 已于 10-08 做同一次迁移）。
    public string License { get; init; } = "AGPL-3.0-or-later";
}