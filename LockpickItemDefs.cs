// SPDX-License-Identifier: AGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LockpickStandaloneItems;

/// <summary>
/// 万能钥匙 + 骇入装置（2026-10-13 自独立服务端模组 LockpickStandaloneItems41 并入本 mod）。
///
/// 【为什么并进 OperatorTraits】
/// 用户不想发太多包：刻印者（特质商人）作为唯一载体，开锁这条线只留一个服务端 mod。
/// 两件物品的 id 与原独立 mod **逐字节一致**（存档里已有的物品照常认得，不会变孤儿）：
///   万能钥匙 6a1f01aa01bb02cc03dd0001 / 骇入装置 6a1f01aa01bb02cc03dd0002。
///
/// 【克隆源与父节点】
/// 撬锁工具克隆原版钥匙 exitkey（5448ba0b4bdc2d02308b456c），模型即钥匙；
/// 骇入装置克隆金色 1G 手机（5bc9b720d4351e450201234b），外观直接借它的模型。
/// 两者都不覆盖 Prefab：模型完全沿用克隆源，本 mod 零资源依赖、不带任何 bundle。
/// 父节点统一改成原版 KeyMechanical（5c99f98d86f7745c314214b3），
/// 客户端会为它挂 KeyComponent —— 撬锁工具靠它记使用次数、构成耐久池；
/// 骇入装置设为 -1 无限使用，挂上它只是为了让客户端按钥匙类正常解析。
/// 已核对过金色手机与钥匙的 _props 结构几乎一致（51 个共有键，差异只有手机多
/// MaxResource/Resource、钥匙多 MaximumNumberOfUsage），两边 UsePrefab 都是空对象
/// （不是 null，符合客户端不 NRE 的要求），尺寸都是 1x1。
///
/// 【为什么不走 CustomItemService.CreateItemFromClone】
/// 原实现走 CustomItemService，本类改走与 RaidRepairItems 同一套 ICloner + TemplateTable 手法，
/// 与刻印者其余自持商品保持一个口径，不额外往 DI 里塞一个新依赖。
/// 三处「CreateItemFromClone 会做、本类自己复刻」的动作写在各自小节注释里，逐条对应：
///   · AddToHandbookDb  → AddHandbook（写 TemplateTable.Handbook.Items）
///   · AddToFleaPriceDb → AddFleaPrice（写 TemplateTable.Prices）
///   · AddToLocaleDbs   → AddLocales（按语言挂 transformer，中文给中文、其余回落英文）
/// </summary>
internal static class LockpickItemDefs
{
    public const string LockpickTpl = "6a1f01aa01bb02cc03dd0001";
    public const string HackDeviceTpl = "6a1f01aa01bb02cc03dd0002";

    /// <summary>撬锁工具的克隆源：原版钥匙 exitkey，模型即钥匙。</summary>
    private const string LockpickCloneTpl = "5448ba0b4bdc2d02308b456c";

    /// <summary>骇入装置的克隆源：金色 1G 手机。只借它的模型，属性全由本类覆盖。</summary>
    private const string HackDeviceCloneTpl = "5bc9b720d4351e450201234b";

    /// <summary>两件共用的父节点：原版 KeyMechanical。</summary>
    private const string KeyMechanicalNode = "5c99f98d86f7745c314214b3";

    /// <summary>手册分类「特殊装备」（Special equipment，handbook.json 里 ParentId=null 的一级分类）。</summary>
    private const string HandbookSpecialEquipment = "5b47574386f77428ca22b345";

    public static List<MongoId> Inject<TLog>(TemplateTable templates, ICloner cloner, LocaleTable localeTable, ISptLogger<TLog> logger)
    {
        List<MongoId> injected = new List<MongoId>();
        if (templates?.Items == null)
        {
            logger.Error("[万能钥匙物品包]: 物品库不可用, 跳过注入。", null);
            return injected;
        }

        // 万能钥匙：可用次数 8（会随使用次数损耗），0.2kg，手册 3 万、跳蚤 4.5 万。
        AddItem(injected, templates, cloner, localeTable, logger, LockpickTpl, LockpickCloneTpl,
            "Master Key", "万能钥匙",
            "A universal key for picking locks. Wears out with use.",
            "一把能开锁的万能钥匙，随使用次数损耗。",
            8, 0.2, 30000, 45000);

        // ★-1 = 无限使用，与全部 227 把原版钥匙一致（实测它们清一色是 -1）。
        //   骇入的频率限制已经交给「全局冷却」了，再加一条耐久就是双重收费，
        //   而且玩家会因为「怕用完」而不敢用这条路线 —— 冷却才是真正的节奏阀门。
        AddItem(injected, templates, cloner, localeTable, logger, HackDeviceTpl, HackDeviceCloneTpl,
            "Hacking Device", "骇入装置",
            "A handheld breaker for electronic locks. Unlimited uses; the cooldown is the real limit.",
            "一台手持式电子锁破解器，不消耗次数；真正的限制是开锁后的冷却。",
            -1, 0.3, 38000, 55000);

        logger.Info("[万能钥匙物品包]: 物品已注入 " + injected.Count + " 件（万能钥匙 / 骇入装置）。", null);
        return injected;
    }

    private static void AddItem<TLog>(List<MongoId> injected, TemplateTable templates, ICloner cloner,
        LocaleTable localeTable, ISptLogger<TLog> logger, string tplId, string cloneTpl,
        string enName, string chName, string enDesc, string chDesc,
        int maxUsages, double weight, double handbookPrice, double fleaPrice)
    {
        MongoId tpl = new MongoId(tplId);
        if (templates.Items.ContainsKey(tpl))
        {
            // 幂等：已经在表里（本次起服重复注入，或别的 mod 占了同一个 id）就不再覆盖。
            injected.Add(tpl);
            return;
        }

        if (!templates.Items.TryGetValue(new MongoId(cloneTpl), out TemplateItem source) || source == null)
        {
            logger.Error("[万能钥匙物品包]: 找不到克隆源 " + cloneTpl + "（" + chName + "）, 未注入。", null);
            return;
        }

        try
        {
            TemplateItem clone = cloner.Clone(source);
            string id = tpl.ToString();
            Set(clone, "Id", tpl);
            // Name 与原实现一致取英文名（locale 显示走的是 <id> Name，不是这个字段）。
            Set(clone, "Name", enName);
            SetParent(clone, KeyMechanicalNode);

            object props = GetProp(clone, "Properties");
            if (props == null)
            {
                logger.Error("[万能钥匙物品包]: " + id + " 拿不到 Properties, 未注入（物品会是空壳）。", null);
                return;
            }

            // 关键字段逐个写并逐个回执：任何一个写不进去都整件作废并报 Error，
            // 绝不静默 —— 静默的表现是「物品能看见但用不了」，排查成本极高。
            List<string> missed = new List<string>();
            if (!Set(props, "MaximumNumberOfUsage", maxUsages)) { missed.Add("MaximumNumberOfUsage"); }
            if (!Set(props, "Weight", weight)) { missed.Add("Weight"); }
            if (!Set(props, "Width", 1)) { missed.Add("Width"); }
            if (!Set(props, "Height", 1)) { missed.Add("Height"); }
            // BSG 跳蚤白名单：enableBsgList=true 时这两个开关必须同时为真，
            // 否则 RagfairServerHelper.IsItemValidRagfairItem 直接判否、跳蚤不生成挂单。
            if (!Set(props, "CanSellOnRagfair", true)) { missed.Add("CanSellOnRagfair"); }
            if (!Set(props, "CanRequireOnRagfair", true)) { missed.Add("CanRequireOnRagfair"); }
            if (missed.Count > 0)
            {
                logger.Error("[万能钥匙物品包]: " + chName + "(" + id + ") 以下属性没写进去 "
                    + string.Join("/", missed) + ", 未注入。", null);
                return;
            }

            templates.Items[tpl] = clone;
            AddHandbook(templates, logger, tpl, handbookPrice);
            AddFleaPrice(templates, logger, tpl, fleaPrice);
            AddLocales(localeTable, logger, id, enName, chName, enDesc, chDesc);
            injected.Add(tpl);
            logger.Info("[万能钥匙物品包]: 已注入 " + chName + " " + id + "（" + maxUsages
                + " 次 / " + weight + "kg / 手册 " + handbookPrice + " / 跳蚤 " + fleaPrice + "）。", null);
        }
        catch (Exception ex)
        {
            logger.Error("[万能钥匙物品包]: 注入 " + tpl + " 失败: " + ex.Message, null);
        }
    }

    /// <summary>
    /// 复刻 CustomItemService.AddToHandbookDb：往 Handbook.Items 塞一条
    /// { Id, ParentId=手册分类, Price }，物品在手册里就归到「特殊装备」并带上定价。
    /// </summary>
    private static void AddHandbook<TLog>(TemplateTable templates, ISptLogger<TLog> logger, MongoId tpl, double price)
    {
        List<HandbookItem> book = templates.Handbook?.Items;
        if (book == null)
        {
            logger.Error("[万能钥匙物品包]: handbook 表不可用, " + tpl + " 归不了类、也没有手册价。", null);
            return;
        }
        foreach (HandbookItem entry in book)
        {
            if (entry != null && entry.Id.Equals(tpl))
            {
                // 幂等：表里已有（上一轮注入过）就只改价，不重复塞条目。
                entry.ParentId = new MongoId(HandbookSpecialEquipment);
                entry.Price = price;
                return;
            }
        }
        book.Add(new HandbookItem
        {
            Id = tpl,
            ParentId = new MongoId(HandbookSpecialEquipment),
            Price = price,
        });
    }

    /// <summary>
    /// 复刻 CustomItemService.AddToFleaPriceDb：往 prices.json 的运行期表 TemplateTable.Prices
    /// 写一件物品的跳蚤底价（跳蚤挂单按它定价）。价必须 &gt; 0，否则 SPT 会抛。
    /// </summary>
    private static void AddFleaPrice<TLog>(TemplateTable templates, ISptLogger<TLog> logger, MongoId tpl, double price)
    {
        Dictionary<MongoId, double> prices = templates.Prices;
        if (prices == null)
        {
            logger.Error("[万能钥匙物品包]: prices 表不可用, " + tpl + " 在跳蚤没有底价。", null);
            return;
        }
        if (price <= 0)
        {
            logger.Error("[万能钥匙物品包]: " + tpl + " 的跳蚤底价非正(" + price + "), 未写入。", null);
            return;
        }
        prices[tpl] = price;
    }

    /// <summary>
    /// 复刻 CustomItemService.AddToLocaleDbs：按语言给 &lt;id&gt; Name / ShortName / Description 挂 transformer。
    /// 中文语言用中文文案，其余语言回落英文 —— 与原 LockpickStandaloneItems41 的兜底口径一致
    /// （源实现是 en/ch 两份，其他语言取字典第一项，也就是英文）。
    /// 必须走 AddTransformer 而不是直改字典：Global 里的值是 LazyLoad，首次读取才反序列化。
    /// </summary>
    private static void AddLocales<TLog>(LocaleTable localeTable, ISptLogger<TLog> logger, string tpl,
        string enName, string chName, string enDesc, string chDesc)
    {
        if (localeTable?.Global == null)
        {
            logger.Error("[万能钥匙物品包]: locale 表不可用, " + tpl + " 没有 Name/Description 文案。", null);
            return;
        }
        try
        {
            int count = 0;
            foreach (var entry in localeTable.Global)
            {
                bool chinese = string.Equals(entry.Key, "ch", StringComparison.OrdinalIgnoreCase);
                string name = chinese ? chName : enName;
                string desc = chinese ? chDesc : enDesc;
                string id = tpl;
                entry.Value.AddTransformer(dict =>
                {
                    if (dict == null)
                    {
                        return dict;
                    }
                    dict[id + " Name"] = name;
                    dict[id + " ShortName"] = name;
                    dict[id + " Description"] = desc;
                    return dict;
                });
                count++;
            }
            logger.Info("[万能钥匙物品包]: " + tpl + " 文案已登记, 覆盖 " + count + " 种语言。", null);
        }
        catch (Exception ex)
        {
            logger.Error("[万能钥匙物品包]: " + tpl + " 文案写入失败: " + ex.Message, null);
        }
    }

    private static object GetProp(object target, string name)
    {
        if (target == null) { return null; }
        PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null) { return p.GetValue(target); }
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return f?.GetValue(target);
    }

    /// <summary>反射赋值（规避反编译件字段/属性命名不确定）；返回 false 表示根本没写进去。</summary>
    private static bool Set(object target, string name, object value)
    {
        if (target == null) { return false; }
        try
        {
            PropertyInfo p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(target, value); return true; }
            FieldInfo f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) { f.SetValue(target, value); return true; }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[万能钥匙物品包]: 写属性 " + name + " 抛异常 " + ex.Message);
        }
        return false;
    }

    /// <summary>
    /// 父类字段真名是 Parent（不是 ParentId），三候选逐个试。
    /// ★值必须是 MongoId 而不是 string：TemplateItem.Parent 的类型是 MongoId，
    ///   传 string 会抛 "Object of type System.String cannot be converted to MongoId"。
    /// </summary>
    private static void SetParent(TemplateItem item, string parentId)
    {
        MongoId value = new MongoId(parentId);
        foreach (string field in new[] { "Parent", "ParentId", "_parent" })
        {
            PropertyInfo p = item.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(item, value); return; }
            FieldInfo f = item.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) { f.SetValue(item, value); return; }
        }
        Console.WriteLine("[万能钥匙物品包]: 设父节点失败（三种候选名都没找到可写成员）");
    }
}
