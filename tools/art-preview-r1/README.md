# tools/art-preview-r1 · probe GLB 生成器

`make_probe_glb.py`：用 CPython 标准库（struct/json/hashlib，无第三方依赖）一次性生成
test-only 探针 GLB，输出到 `prototype/scenes/art_preview_r1/probe_model_r1.glb`。

- 节点树：`Body`（0.4 m 盒，y=0.2）、`Socket_Cargo`（空节点，y=0.45）→ `Cargo`（0.24 m 盒）。
  双根设计使 Godot 导入场景根直接对应包装场景里的 `Model` 实例名，导入后实查路径为
  `Model/Body`、`Model/Socket_Cargo/Cargo`（与 probe manifest 的 material_bindings 一致）。
- 占位材质故意为亮品红/亮绿：任何未被 override 命中的 surface 在未来渲染里保持可见，
  便于发现漏接；预览合同本身禁止灰材质回退。
- 三角绕向：角点序从外侧看为顺时针，导出时反转索引保证 glTF 要求的 CCW（对外法线
  正行列式），主控面检查脚本实测 `GLB_FACE_AUDIT_OK`（2026-10-04 rework 修复）。
- 重新生成会改变 GLB 字节，从而改变 `glb_sha256`，必须同步更新
  `prototype/scenes/art_preview_r1/probe_manifest_r1.json` 并重新跑导入与自测。

```sh
python3 tools/art-preview-r1/make_probe_glb.py prototype/scenes/art_preview_r1/probe_model_r1.glb
# 然后按 prototype/scenes/art_preview_r1/README.md 重新导入并自测
```

当前提交的 GLB：2964 字节，SHA256
`aa274ff353a0c94d88f0f1b5f88a9d6f464d43df5b8542958660aa3bd913ce7b`（生成器 SHA256
`df99a87ea1d26b252a21123603caa24122edeb31f88c12322b2563ba05d51c23`，与 manifest source 一致）。

不做：通用 glTF 导出、动画/蒙皮、纹理写入、任何生产资产生成。

B 切片补充：probe manifest 的 `source.files` 逐项源哈希由采集记录（`preview_capture_r1.gd`）按实际文件核对，
不只抄声明值；生成器内容未变，`source.files` 与既有 `source.hash` 一致。
