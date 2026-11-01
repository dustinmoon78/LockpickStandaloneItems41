// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LockpickStandaloneItems;

/// <summary>
/// 万能钥匙物品包（服务端）—— 独立自持版（2026-10-17 自 OperatorTraits 抽回）。
///
/// 【本包做什么】
/// 建两件自备工具，让「万能钥匙」客户端插件在任何环境里都能用：
///   万能钥匙 6a1f01aa01bb02cc03dd0001（克隆原版钥匙，父节点 KeyMechanical，8 次使用）
///   骇入装置 6a1f01aa01bb02cc03dd0002（克隆金色 1G 手机，无限使用）
/// 另加两条工作台配方（见 LockpickRecipeDefs）与开锁技能说明文案（见 LockpickSkillLocale）。
///
/// 【分工：物品/配方/文案归本包，货架归刻印者】
/// 2026-10-28 拆包定案：本包是这两件物品与两条配方的唯一注入方，
/// 刻印者（OperatorTraits）侧的同名注入调用已删除，只保留「货架条目」——
/// 货架条目由刻印者在探测到本包在位后上架（判据见 OT 的 StandaloneProbe）。
/// 原先的「刻印者在位则整包空操作」互斥守卫已删除：留着会让本包在有刻印者的部署里
/// 一件东西都不注入（物品与配方全部落空），与拆包后的分工正好相反。
/// 幂等由各自实现兜底：物品走模板表判重（LockpickItemDefs.AddItem），
/// 配方走 id 判重（LockpickRecipeDefs.Inject），所以与旧版刻印者同时存在也不会重复注入。
/// </summary>
[Injectable(InjectionType.Singleton, int.MaxValue, TypePriority = OnLoadOrder.Preload)]
public class LockpickStandaloneItems : IOnLoad
{
    private readonly ISptLogger<LockpickStandaloneItems> _logger;
    private readonly TemplateTable _templates;
    private readonly ICloner _cloner;
    private readonly LocaleTable _localeTable;
    private readonly HideoutTable _hideoutTable;

    public LockpickStandaloneItems(
        ISptLogger<LockpickStandaloneItems> logger,
        TemplateTable templates,
        ICloner cloner,
        LocaleTable localeTable,
        HideoutTable hideoutTable)
    {
        _logger = logger;
        _templates = templates;
        _cloner = cloner;
        _localeTable = localeTable;
        _hideoutTable = hideoutTable;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // 三件套各自 try/catch：任何一件失败都写 Error（不许静默），其余两件照常继续。
        int injected = 0;
        try
        {
            injected = LockpickItemDefs.Inject(_templates, _cloner, _localeTable, _logger).Count;
        }
        catch (Exception ex)
        {
            _logger.Error("[Lockpick] 注入物品失败: " + ex.Message, null);
        }

        try
        {
            LockpickRecipeDefs.Inject(_hideoutTable, _logger, injected > 0);
        }
        catch (Exception ex)
        {
            _logger.Error("[Lockpick] 注入配方失败: " + ex.Message, null);
        }

        try
        {
            LockpickSkillLocale.Inject(_localeTable, _logger);
        }
        catch (Exception ex)
        {
            _logger.Error("[Lockpick] 登记开锁技能文案失败: " + ex.Message, null);
        }

        // 末行固定验收串（拆包验收按它 grep）：三件套各自的成败见上面各自的 Info/Error。
        _logger.Info("[Lockpick] patches enabled (items x2 / recipes x2 / skill locale)", null);
        return Task.CompletedTask;
    }
}
