# 最终地表边界局部复核 — PASS

会话01a11623-f7ab-7d83-8824-33b9ba00654f，gpt-6.1-sol/high，cursor 6ea7facc-8ffc-4583-9339-2442b692bc33:2。实际看native-mirror-sand的ground/reverse/near/normal与ground-unshaded；support hash 2493622470df2a0c4194bb35357c0b856d6e77fe595cc63c046528a784cf0791。

原ground左下(0,610)→(590,1000)颗粒/明暗突变消失；near/reverse不再形成明显方垫。unshaded仍能追到极淡斜线，但无明显亮边/成片色差，当前不阻断。四图可见边角及外侧下一重复处未发现新增亮线、错位或明显接缝。root实看受光/无光照图确认此改善。

实际方法：支撑共享patch Sand PBR；匹配600边点/法线，UV外围原生12m镜像折返，核心patch不变。几何没有重叠遮缝。当前仅独立看样外围使用重复贴图，大范围真实地形需要更多变体，未声称全场景高保真。

此为地表局部通过；岩面保留最终01 REWORK，整套最终候选/流程交接尚未完成。所有者审美、Main、整场性能NOT_RUN。
