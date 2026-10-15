# SM 与 Anno 系统研究笔记（2026-10-13）

范围：整理 Surviving Mars（SM）与 Anno 的一手来源事实，供《余电》物资、地形、建造与科技的结构讨论使用。来源访问日期 2026-10-13，未实机、未试玩。资料由主控联网读取原文并核验，本笔记作者按已核内容整理，未另行联网复核。全部来源 URL 见[竞品基础框架主报告](./2026-10-13-competitor-foundation-systems.md)第 7 节，本笔记第八节同列以便追溯。本笔记不是实施授权。

## 一、结论先行

- SM:Relaunched 的物流与科研日记显示，depot、班车、科技与探索都是有条件的机制。免费即时 depot 是官方明确的类型例外，不能据此把“点一下就出现”一律判为坏设计。
- 2019 原版 SM 的整平先评估现有地形，再由无人机搬入或移走废石；Ramp 连接不同高程的平地，形成可步行地面。整平工具开局可用，不需研究。
- Anno 的蓝图只做规划：放置不耗资源或人力，建成前不运行，且只允许已解锁建筑预先规划。建成后的运营人力不足会放慢生产，这属于运营规则。
- 对《余电》的启示是区分定义、规划、工程、运营与研究状态；不照搬竞品的土方资源、居民、资金或随机科技位置。

## 二、来源与边界

- 访问日期 2026-10-13；无实机。资料由主控联网读取原文并核验，本笔记作者按已核内容整理，未另行联网复核。
- SM:Relaunched：2025-09-02 物流日记，2025-09-09 研究与探索日记。
- SM 2019 原版：Dev Diary #5（2019-05-10）与 Armstrong notes（2019-05-16），日期取自 Steam 原始页面。新版 RC Dozer 的完整规则未核，不外推。
- Steam 列表页混合了社区与媒体条目。本笔记只引用其中两篇 steam_community_announcements 官方公告条目（Dev Diary #5、Armstrong notes）及其正文，不引用列表里的其他媒体条目。这里所说的一手，指这两篇官方公告条目，不指整个聚合列表。
- Anno：2018 PC 发布前开发博客（页面标题为《This belongs in a museum!》，正文含蓝图）；2023-03-16 Console Edition 官方提示。Anno 2205、现行 PC 补丁与闭源内部代码未核。

## 三、SM:Relaunched 物流与科研（2025）

### 物流（2025-09-02）

来源：[SM:Relaunched 物流与资源日记](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/logistics-and-resources)

- depot 可设置期望存量。无人机的服务范围由指挥器限定；班车运行还需要供电与燃料等条件。
- 班车可由开局配置直接取得；否则需经 Physics 科技解锁。
- 免费即时 depot 是官方明确的类型例外。
- 电力经电缆、水与氧经管道输送，与可运输的矿物物资不是同一类。
- 私有注册表与类实现未公开，本笔记不作断言。

### 科研（2025-09-09）

来源：[SM:Relaunched 研究与探索日记](https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/research-and-exploration)

- 7 条研究路线，每项研究点有成本，研究队列最多 5 项。
- 每局科技位置重排，但有早晚阶段限制；通常完成一项后开放下一项。
- 异常探索可给研究点，也可能揭示更深的科技。
- 研究点来源包括赞助、里程碑、探险、异常、实验室与地球外包。
- 这是机制说明，不是固定科技图的源码。历史原版的节点数量不引用，避免误称新版数量。

## 四、2019 原版 SM 整平

来源：2019 Steam 官方公告条目 [Dev Diary #5 与 Armstrong notes](https://store.steampowered.com/news/posts/?appgroupname=Surviving+Mars&appids=464920&enddate=1559147206)（列表页中的两篇 steam_community_announcements 条目，不含列表中的其他媒体条目）

- Dev Diary #5（2019-05-10）：每个整平工程先评估现有地形，计算需要或多余的土方，再由无人机运入或移走废石。Ramp 连接不同高程的平地，形成可步行地面。
- Armstrong notes（2019-05-16）：Flatten/Ramp 大量占用无人机时间，并可能需要或产生废石。整平工具开局可用，不需研究。
- 限定：只核了 2019 原版，不外推到新版 RC Dozer。

## 五、Anno 蓝图（2018 与 2023）

### 2018 PC 发布前开发博客

来源：[Anno 2018 官方蓝图博文](https://www.anno-union.com/x-marks-the-spot/)

- 发布日期按页面显示为 2018-10-05；HTML 元数据 datePublished 为 2018-10-04T22:00:00+00:00，两者差异来自站点时区。
- 页面标题为《This belongs in a museum!》，正文同时介绍博物馆与蓝图。
- 蓝图放置不耗资源或金钱，建成前不运行，只允许已通过自然进程解锁的建筑预先规划。
- 该资料是 PC 发布前的说明，不是现行 PC 实测。

### 2023 Console Edition 官方提示（2023-03-16）

来源：[Anno 1800 Console Edition 官方玩法提示](https://news.ubisoft.com/en-us/article/6z7wAll2mXTdQtp79h2B2x/anno-1800-console-edition-4-tips-for-getting-started)

- 蓝图不生产，也不耗资源或人力。
- 同岛仓库共享；跨岛不共享，需要贸易路线。
- 运营人力不足会放慢生产、增加成本。
- 不能据此推断建造阶段消耗人力，或所有物流瞬间传送。

## 六、与《余电》的对照（结构推导，非实施）

- 物资：统一的物资定义与实际库存、物流分开记录。定义不能替代位置、预留、在途与运输条件。
- 改地：规划依次评估场地、代价与收益，再决定是否执行。
- 工程：规划、授权、施工、建成、运营分开记录；预览不产生承诺。
- 科技：进度独立于教学，且解锁后必须改变生产或物流能力。
- 不照搬：土方资源、居民、资金、随机科技位置。保留《余电》已有的真实料流与目标委托。

## 七、未核与限制

- 未试玩、未实机，无法证明手感、节奏或玩家理解。资料由主控联网读取原文并核验，本笔记未另行联网复核。
- Anno 2205、现行 PC 补丁、闭源内部代码与注册表未核。
- SM 新版 RC Dozer 的规则未核。

## 八、来源 URL

以下 Steam 链接为列表页；两篇官方公告条目的范围见第二节。

- SM:Relaunched 物流：https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/logistics-and-resources
- SM:Relaunched 研究与探索：https://prod-paradox-hub.paradox-interactive.com/games/surviving-mars-relaunched/news/research-and-exploration
- SM 2019 Steam 公告列表：https://store.steampowered.com/news/posts/?appgroupname=Surviving+Mars&appids=464920&enddate=1559147206
- Anno 2018 开发博客：https://www.anno-union.com/x-marks-the-spot/
- Anno 2023 Console Edition 提示：https://news.ubisoft.com/en-us/article/6z7wAll2mXTdQtp79h2B2x/anno-1800-console-edition-4-tips-for-getting-started
