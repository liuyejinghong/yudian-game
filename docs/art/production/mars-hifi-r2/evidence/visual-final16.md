# Stage16 独立最终视觉复验

会话：01a11623-f7ab-7d83-8824-33b9ba00654f，gpt-6.1-sol/high；cursor `6ea7facc-8ffc-4583-9339-2442b692bc33:4`。实际看当前Godot detail/reverse/near/ground与新烘焙baked-detail/baked-rear-close六图，结果 **REWORK**。

- detail右面约(850–1040,600–800)集中团状起伏，外围大面积光滑，仍呈局部纹理贴章；源baked-detail也可见集中分布。
- reverse背墙约(670–950,430–650)仍主要读成光滑大板；source近景的侵蚀/细颗粒在游戏里明显弱化。
- 薄层、接地与ground边界未退步，已有局部PASS保留。整套核心样件尚未PASS；Main、所有者审美与性能NOT_RUN。

root实际打开四张新烘焙图与七张固定Godot图，认同剩余问题。当前GLB为stage16候选02755e27…，技术检查绑定native-stage16/candidate-check.json；正式12源未覆盖。暂停继续改覆盖外围或烘焙，请architect重新计划面内连续性和背光可读性。只准两块ROI是本轮实现选择，并非用户要求，必要时可修整个指定裸露面，仍保留09宏观/薄层/节理/接地及地表。
