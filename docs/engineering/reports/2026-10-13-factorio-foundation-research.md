# Factorio 底层框架研究：物资 / 配方 / 建造与科技

- 访问日期：2026-10-13
- 依据版本：全部一手页面页头实测为 **Factorio API Docs Version 2.1.21**（latest 与固定 /2.1.21/ 路径一致，下文引用一律用固定版本 URL 便于日后复读比对）
- 来源边界：仅 Factorio 官方 API 文档（原型定义页、runtime 类页、数据生命周期页）。未试玩，未访问闭源引擎内部实现，未做源码级审计。wiki 仅作背景参考、不作为证据引用（社区维护）。
- 性质：本文为竞品公开文档研究，不是实现授权；"建议"一节的推导由主控验收。

## 总框架：定义（data stage）与运行态（runtime）分离

Factorio 公开文档把游戏内容明确切成两层，且生命周期页写明两层的边界与交接：

- **data stage（定义层）**：每个 mod 依次跑 `data.lua`、`data-updates.lua`、`data-final-fixes.lua` 三轮，向全局 `data` 表登记原型（prototype）；"Missing properties will either fall back to a default value or give an error"，游戏会记录"哪个 mod 改过哪个原型"的历史。三轮结束后 Lua 状态整体丢弃（"the shared Lua state is discarded"），随后"all prototypes are constructed and the game goes to the main menu"。
  来源：[Data Lifecycle](https://lua-api.factorio.com/2.1.21/auxiliary/data-lifecycle.html)
- **runtime stage（运行态）**：读档时"prototypes can no longer be modified"，原型在运行期不可变；运行期只有实例对象（LuaEntity、LuaInventory、LuaTechnology 等）持有对原型的只读引用（如 [LuaTechnology.prototype](https://lua-api.factorio.com/2.1.21/classes/LuaTechnology.html)）。
- **运行期的保存与迁移（公开文档可见部分）**：生命周期页把控制阶段（control stage）列为 `on_init`、`on_load`、`on_configuration_changed` 与 migrations，运行期持久化表为 `storage`。即官方公开层面可证明的存档/迁移机制是：mod 用 `storage` 表持久化状态，配置变化时通过 `on_configuration_changed` 与 migration 文件迁移旧存档数据。闭源引擎内部如何序列化（event-sourcing 还是 ECS 还是快照）**不在公开文档内，本文不下结论**。
- **跨引用方式**：从各定义页字段类型可直接证明"按名字跨引用"是内容层的统一约定——[ItemPrototype.place_result](https://lua-api.factorio.com/2.1.21/prototypes/ItemPrototype.html) 类型为 `EntityID`（实体原型名）、[TechnologyPrototype.prerequisites](https://lua-api.factorio.com/2.1.21/prototypes/TechnologyPrototype.html) 为 `array[TechnologyID]`、unlock-recipe 效果以 `recipe` 字符串指配方；runtime 侧 [LuaForce.technologies / LuaForce.recipes](https://lua-api.factorio.com/2.1.21/classes/LuaForce.html) 均为"indexed by `name`"的表。生命周期页本身未单独陈述此约定，此条由上述字段类型间接证明。

## 模块一：物资与库存

### 定义（data stage）

[ItemPrototype](https://lua-api.factorio.com/2.1.21/prototypes/ItemPrototype.html)，类型串 `'item'`，继承 `Prototype « PrototypeBase`。关键字段：

| 字段 | 类型 | 文档原文/要点 |
|---|---|---|
| `name` | string | "Unique textual identification of the prototype"（本页对 name 的定义原文；未说明全局唯一范围） |
| `stack_size` | ItemCountType | 必填，"Count of items of the same name that can be stored in one inventory slot"（按**同名**计数） |
| `place_result` | EntityID | "Name of the EntityPrototype that can be built using this item" |
| `weight` | Weight | 可选，缺省"calculated automatically from recipes" |
| `localised_name` | LocalisedString | 可选；设了 `place_result` 时默认改用实体的 localised name |

### 运行态

[LuaInventory](https://lua-api.factorio.com/2.1.21/classes/LuaInventory.html)："A storage of item stacks"，属于运行期实例，且有 `entity_owner :: Read LuaEntity?`（"The entity that owns this inventory, if any"）。核心接口（均为"按物品名计数/操作"的形状）：

- `get_item_count(item?) → uint32`
- `insert(items) → uint32`（"Number of items actually inserted"——插入可因容量不足部分成功）
- `remove(items) → uint32`（"Number of items actually removed"）
- `can_insert(items) → boolean`
- `find_item_stack(item) → LuaItemStack?, uint32?`
- `get_contents() → array[ItemWithQualityCount]`（2.x 引入了 quality 维度，计数结构带品质聚合）

### 规则要点与推导

1. **身份引用统一走字符串 `(type, name)`**：公开 API 对内容的引用全部是字符串 name（定义层字段如 `place_result`/`prerequisites`/unlock-recipe 的 `recipe`，运行期接口参数如 `ItemWithQualityID`/`ItemStackIdentification`）；内部是否存在数值 ID 不在公开文档内，不作否定或肯定推定。`stack_size` 的定义原文把"同一格可叠放"与"同名"绑定——运行态持"名字 + 数量"，实体细节留在原型里。
2. **库存可隶属运行期对象**：`entity_owner :: LuaEntity?`（可空）表示库存**可以**隶属实体，另有 `player_owner`/`equipment_owner`/`mod_owner` 等并列所有权字段；nullable 字段不证明所有库存都实体所有，也不能据此否定全局账本形态——库存的内存所有权与全局结构未核。堆叠规则（`stack_size`）来自原型，运行期只读。
3. **插入/移除返回实际数量**：接口按"可能部分成功"设计，说明引擎在运行期做容量与堆叠校验，而不是调用者自负。

### 证据边界

- `stack_size` 唯一性表述止于"同名一格"；原型 name 的全局唯一范围本页未陈述，不下断言。
- 物品在物流网络/车辆等载体的分配细节未读，不覆盖。

## 模块二：配方

### 定义（data stage）

[RecipePrototype](https://lua-api.factorio.com/2.1.21/prototypes/RecipePrototype.html)，继承 `Prototype « PrototypeBase`。关键字段：

| 字段 | 类型 | 要点 |
|---|---|---|
| `ingredients` | `array[IngredientPrototype]` | 条目为 `{type, name, amount}`，如 `{type = "item", name = "iron-stick", amount = 2}`；"Duplicate ingredients, e.g. two entries with the same name, are not allowed" |
| `results` | `array[ProductPrototype]` | 同形状，产品可带 `probability`、流体 `temperature`；重复结果允许；`main_product` 指定默认信息来源 |
| `energy_required` | double | 默认 0.5，"Must be > 0.001" |
| `categories` | `array[RecipeCategoryID]` | 默认 `{"crafting"}` |
| `enabled` | boolean | 默认 `true`（可被配方直接开局可用） |

### 运行态

- [LuaForce.recipes](https://lua-api.factorio.com/2.1.21/classes/LuaForce.html)：`LuaCustomTable[string → LuaRecipe]`，"Recipes available to this force, indexed by `name`"——配方的可用性是**每 force（队伍）的运行态**，不是全局。
- force 提供 `enable_all_recipes()`、`disable_all_prototypes()`（"Disable all recipes and technologies"）；单个配方的 enabled 运行态挂在 LuaRecipe 上（本页未展开，未读 LuaRecipe 页）。

### 规则要点与推导

1. **配方是"名字引用的纯声明"**：ingredients/results 只含 `(type, name, amount)`，没有对物品实例的引用；这使配方可以安全地在定义层注册、运行层每 force 实例化可用性。
2. **enabled 双层**：定义层 `enabled` 是初始默认；运行层"这个 force 能不能用这个配方"是 force 状态，由科技解锁（unlock-recipe）或脚本改写。
3. **成本与产出都是显式数量**：不隐含概率外的任何账目逻辑；`probability` 与"是否计产率加成"是产品条目的显式字段。

### 证据边界

- `enabled` 的存档持久化行为在 RecipePrototype 页**未读到**（TechnologyPrototype 页只对 technology 的 enabled 写明存档规则，见模块三）；配方 enabled 是否同样"存于存档、不从原型重载"**未核**。
- crafting machine 端的加速/产能模块机制未读，不覆盖。

## 模块三：建造（ghost/实体）与科技

### 建造：ghost 与已建成实体是不同的运行期对象

[LuaEntity](https://lua-api.factorio.com/2.1.21/classes/LuaEntity.html)（"Entities are everything that exists on the map except for tiles"）上，ghost 是同一个 LuaEntity 类的不同形态，文档以字段区分：

- `ghost_type` / `ghost_name`："The prototype type of the entity or tile contained in this ghost" / "Name of the entity or tile contained in this ghost"——ghost 内含的是**被引用原型的名字**，不是实体。
- `revive`："Revive a ghost, which turns it from a ghost into a real entity or tile"——ghost 变真体是一次显式状态转换。
- `item_requests :: Read array[ItemWithQualityCount]`："Items this ghost will request when revived or items this item request proxy is requesting"。按原句照录：这是实体（复活）后**内部物品**的请求清单（实体自身槽位，如模块/弹药类），**不等于 ghost 建造本体的材料清单，不能当 construction cost 证据**；本体材料的供应机制未核（见证据边界）。另注意 `revive` 本身是**脚本 API**（把 ghost 转为真实实体的显式调用），API 页不证明该动作必然由建造机器人完成。
- 拆除与取消：`order_deconstruction(force, player?, undo_index?) → boolean`、`cancel_deconstruction(force, player?)`、`to_be_deconstructed() → boolean`。可证的是：拆除是按 force 下达的标记指令，`cancel_deconstruction` 只能**取消尚未执行的拆除标记**（对应 `to_be_deconstructed` 状态）；`undo_index` 仅表示该指令可加入撤销动作组（undo），**不能推出**"拆除执行后恢复已拆实体"的整体能力，后者未核。
- 文档另注明"Most functions on LuaEntity also work when the entity is contained in a ghost"——ghost 与真体共享大部分接口，差异集中在实体库存等处（"also works for ghosts where the inventory is not available through LuaControl::get_inventory"）。

### 科技

**定义层** [TechnologyPrototype](https://lua-api.factorio.com/2.1.21/prototypes/TechnologyPrototype.html)：

- `prerequisites :: array[TechnologyID]`（前置科技名列表，如 `{"explosives", "military-2"}`）。
- `unit :: TechnologyUnit`（"Determines the cost in items and time"），子字段 `count`、`ingredients`（如 `{{"automation-science-pack", 1}}`）、`time`；文档写 "Mandatory unless `research_trigger` is set"——即 unit 与 research_trigger 至少其一；二者互斥性本页未见明确禁止条款，不作 XOR 断言。另注意 2.1.21 中 `research_trigger` 是**替代研究条件**（不以科技包计费、改由其他条件完成研究），因此**并非所有科技都必须消耗科技包**；unit（科技包成本）只是科技研究条件之一。
- `effects :: array[Modifier]`，解锁配方是其中一种：`{type = "unlock-recipe", recipe = "land-mine"}`。**科技启用配方的完整链路**：科技效果按名字引用配方 → force 的 recipes 可用性被改写。
- `enabled :: boolean` 默认 `true`；页面明文："**the enabled state is saved in the save file and not reloaded from the prototype**"，且对 `enabled` 与 `visible_when_disabled` 均注明"Changes to this property do not affect existing save files"——即科技初始可用性属于**存档态**，改原型不影响旧档。此存档规则原文针对 technology 的 `enabled`。

**运行层** [LuaTechnology](https://lua-api.factorio.com/2.1.21/classes/LuaTechnology.html)（"One research item"）与 [LuaForce](https://lua-api.factorio.com/2.1.21/classes/LuaForce.html)（"encapsulates data local to each 'force' or 'faction'"）：

| 成员 | 类型 | 证明点 |
|---|---|---|
| `LuaTechnology.force` | `Read LuaForce` | 科技实例归属于 force |
| `LuaTechnology.prototype` | `Read LuaTechnologyPrototype` | 运行态持有只读原型引用 |
| `LuaTechnology.researched` | `Read\|Write boolean` | 研究完成状态是可写运行态 |
| `LuaTechnology.level` / `saved_progress` | `uint32` / `Read\|Write double` | 无限科技等级与当前累计进度分开保存 |
| `LuaTechnology.prerequisites` | `Read dictionary[string → LuaTechnology]` | 前置在运行态解析为科技实例 |
| `LuaForce.technologies` | `LuaCustomTable[string → LuaTechnology]`（"indexed by `name`"） | 科技状态按 force 存放 |
| `LuaForce.current_research` / `research_progress` / `research_queue` | `LuaTechnology?` / `double [0,1]` / `array[TechnologyID]` | "当前研究 + 进度 + 队列"三态在 force 上 |
| `LuaForce.reset()` | — | "All technologies … to not researched"，按 force 重置 |

### 规则要点与推导

1. **建造意图与执行分离**：API 可证的是——ghost 以 `ghost_name`/`ghost_type` 携带"要建什么原型（名字）"与位置，`revive` 是把 ghost 转为真实实体的显式动作（脚本 API，触发者不限于建造机器人）；"意图"与"结果实体"两态在 API 层清楚分开。建造本体材料的供应与消耗账务**未核**（`item_requests` 按原句只涉实体建好后的内部物品请求，见上）；deconstruction 是可按 force 取消的标记指令。不据此对"真实消耗何时记账"作源码级断言。
2. **科技状态 = force 运行态的投影**：定义给结构与成本；运行态分两层——**单科技状态**（`researched`、`level`、`saved_progress`）挂在 force 的 LuaTechnology 实例上，**force 级研究活动**（`current_research` 当前进行的研究、`research_progress` 总进度、`research_queue` 队列）挂在 LuaForce 上。`saved_progress` 是某科技的累计进度，与 force 的 `current_research` 是两个分开的状态，不混同。前置关系在运行态被解析为实例间引用。
3. **原型只读不能代运行态迁移**：生命周期页（原型读档后不可改）证明定义层与运行态分开；Technology `enabled` 的特殊保存规则（"saved in the save file and not reloaded from the prototype"）已证**该字段**的运行态独立于原型。其他原型字段在存档中的保存/迁移行为**按具体字段规则**，公开文档未逐一列出，不能推广为"内容定义升级都不改写运行态"。官方公开可见的迁移通道是 migrations + `on_configuration_changed`。

### 证据边界

- 建造本体材料的供应与消耗链未核：`revive` 是脚本 API；`item_requests` 仅按原句照录，其完整语义与 item request proxy 细节未核（`LuaItemRequestProxy` 类页在 /2.1.21/ 与 /latest/ 路径各 404 一次）；"机器人搬运本体材料"的机制无官方 API 证据（wiki 有机制描述但属社区维护，不采为源码级证据）。
- Technology `unit` 与 `research_trigger` 互斥性未见明文，按"unit 在未设 research_trigger 时必填"陈述。
- 闭源引擎内部（序列化格式、ECS 与否、event-sourcing 与否）无公开文档证据，本文不作断言。

## 对余电的建议（明确推导，非实施授权）

1. **保留现有物资账本，把"内容定义"补成独立层**：Factorio 的 `(type, name)` 定义 + 运行态只持"名字+数量"与现有 `MaterialLedger` 七类物资账本兼容（运行态账本已是数量口径）；缺的是定义层——物品/配方的 `(type, name, stack 规则, place_result, ingredients/results)` 注册表与只读化，使运行态引用定义而非内嵌数值。推导依据：模块一/二的定义-运行态字段分离与"运行期原型不可变"。
2. **科技进度与教学分离**：Factorio 把"配方怎么合成"与"科技何时解锁配方"拆成 RecipePrototype 与 Technology+unlock-recipe 两条轴，科技研究状态（`researched`/`level`/`saved_progress`）独立于配方定义与内容教学。余电科技系统尚未实现；若后续实现，建议科技进度与操作教学分别建模，科技状态可独立存取，不与教学进度耦合。推导依据：模块三科技小节。
3. **保持现有"意图-执行"结构，持久规划为推导建议**：余电现状已是零承诺预览、确认预留、实际取卸转运、施工结算 consume，且取消回收已有单例；与 Factorio 可证的"ghost 意图 / revive 执行 / 拆除标记可取消"形状一致，应保持。ghost 是世界中的意图对象（ghost 跨保存是否持久**本轮 API 未核**）；余电持久规划是推导建议，实现另走任务。账务（预留/结算/取消回收）按余电现有合同处理，不引用 Factorio 的 cancel 作为资金/材料 refund 证据——`cancel_deconstruction` 只证取消拆除标记，不证退预留。推导依据：模块三建造小节 `revive`/`cancel_deconstruction`（revive 属脚本 API，本体材料供应未核）。
4. **能力缺口评估**：明确不照搬的是可复制字符串蓝图、多 force（队伍）、quality 维度——余电当前无对应需求。但持久建造规划（蓝图类能力）若未来需要，属真实能力缺口，应另行立项评估，不随本报告引入；本报告只记录该缺口存在。

## 来源清单（全部访问于 2026-10-13，页头均实测 2.1.21）

| 页面 | 固定版本 URL |
|---|---|
| ItemPrototype | https://lua-api.factorio.com/2.1.21/prototypes/ItemPrototype.html |
| RecipePrototype | https://lua-api.factorio.com/2.1.21/prototypes/RecipePrototype.html |
| TechnologyPrototype | https://lua-api.factorio.com/2.1.21/prototypes/TechnologyPrototype.html |
| LuaInventory | https://lua-api.factorio.com/2.1.21/classes/LuaInventory.html |
| LuaEntity | https://lua-api.factorio.com/2.1.21/classes/LuaEntity.html |
| LuaTechnology | https://lua-api.factorio.com/2.1.21/classes/LuaTechnology.html |
| LuaForce | https://lua-api.factorio.com/2.1.21/classes/LuaForce.html |
| Data Lifecycle | https://lua-api.factorio.com/2.1.21/auxiliary/data-lifecycle.html |

未核清单：配方 `enabled` 的存档持久化规则；除 technology `enabled` 外其他原型字段的存档迁移规则；ghost 跨保存持久性；LuaRecipe 单页；建造本体材料供应/消耗链与 `item_requests` 完整语义（`LuaItemRequestProxy` 类页两次 404）；库存所有权全局结构；`unit`/`research_trigger` 互斥性；闭源引擎内部序列化与架构；游戏内实际体验（未试玩）。
