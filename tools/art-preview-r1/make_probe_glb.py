#!/usr/bin/env python3
"""Generate the test-only probe GLB for ART-PREVIEW-01 slice A.

Original box geometry, Python standard library only, no third-party deps.
Output node tree (glTF scene "ProbeScene"):
    Model
      Body               MeshInstance3D, 1 surface, 0.4 m box, node at y=0.2
      Socket_Cargo       empty Node3D at y=0.45
        Cargo            MeshInstance3D, 1 surface, 0.24 m box at socket origin

Placeholder materials are deliberately loud (magenta/green) so any surface the
preview fails to override stays visible; the preview contract forbids gray or
default-material fallbacks anyway.

Usage: python3 tools/art-preview-r1/make_probe_glb.py <output.glb>
"""

import hashlib
import json
import struct
import sys

def box_faces(size):
    hx, hy, hz = size[0] / 2.0, size[1] / 2.0, size[2] / 2.0
    faces = [
        ((1, 0, 0), [(hx, -hy, -hz), (hx, -hy, hz), (hx, hy, hz), (hx, hy, -hz)]),
        ((-1, 0, 0), [(-hx, -hy, hz), (-hx, -hy, -hz), (-hx, hy, -hz), (-hx, hy, hz)]),
        ((0, 1, 0), [(-hx, hy, -hz), (hx, hy, -hz), (hx, hy, hz), (-hx, hy, hz)]),
        ((0, -1, 0), [(-hx, -hy, hz), (hx, -hy, hz), (hx, -hy, -hz), (-hx, -hy, -hz)]),
        ((0, 0, 1), [(hx, -hy, hz), (-hx, -hy, hz), (-hx, hy, hz), (hx, hy, hz)]),
        ((0, 0, -1), [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz)]),
    ]
    positions, normals, indices = [], [], []
    for normal, corners in faces:
        base = len(positions)
        positions.extend(corners)
        normals.extend([normal] * 4)
        # Corner order is clockwise seen from outside, so emit reversed indices to
        # keep triangles CCW against the outward normals (glTF requires positive
        # determinant: glTF 2.0 spec, section "Instantiation").
        indices.extend([base, base + 2, base + 1, base, base + 3, base + 2])
    return positions, normals, indices


def primitive_buffers(size):
    positions, normals, indices = box_faces(size)
    pos_bytes = b"".join(struct.pack("<3f", *p) for p in positions)
    nrm_bytes = b"".join(struct.pack("<3f", *n) for n in normals)
    idx_bytes = b"".join(struct.pack("<H", i) for i in indices)
    mins = [min(p[i] for p in positions) for i in range(3)]
    maxs = [max(p[i] for p in positions) for i in range(3)]
    return pos_bytes, nrm_bytes, idx_bytes, mins, maxs, len(indices)


def build_glb():
    body = primitive_buffers((0.4, 0.4, 0.4))
    cargo = primitive_buffers((0.24, 0.24, 0.24))
    blobs = []
    views, accessors, meshes = [], [], []
    for name, (pos, nrm, idx, mins, maxs, idx_count) in (("BodyMesh", body), ("CargoMesh", cargo)):
        pos_view = len(views)
        views.append({"buffer": 0, "byteOffset": 0, "byteLength": len(pos), "target": 34962})
        nrm_view = len(views)
        views.append({"buffer": 0, "byteOffset": 0, "byteLength": len(nrm), "target": 34962})
        idx_view = len(views)
        views.append({"buffer": 0, "byteOffset": 0, "byteLength": len(idx), "target": 34963})
        blobs.append((pos, 4))
        blobs.append((nrm, 4))
        blobs.append((idx, 4))
        pos_acc = len(accessors)
        accessors.append({
            "bufferView": pos_view, "componentType": 5126, "count": len(pos) // 12,
            "type": "VEC3", "min": mins, "max": maxs,
        })
        nrm_acc = len(accessors)
        accessors.append({
            "bufferView": nrm_view, "componentType": 5126, "count": len(nrm) // 12,
            "type": "VEC3",
        })
        idx_acc = len(accessors)
        accessors.append({
            "bufferView": idx_view, "componentType": 5123, "count": idx_count,
            "type": "SCALAR",
        })
        meshes.append({
            "name": name,
            "primitives": [{"attributes": {"POSITION": pos_acc, "NORMAL": nrm_acc},
                            "indices": idx_acc, "material": len(meshes)}],
        })

    buffer = b""
    for view in views:
        view["byteOffset"] = len(buffer)
        blob, _align = blobs.pop(0)
        assert len(blob) % 4 == 0
        buffer += blob

    gltf = {
        "asset": {"version": "2.0",
                  "generator": "yudian tools/art-preview-r1/make_probe_glb.py (original, stdlib)"},
        "scene": 0,
        "scenes": [{"name": "ProbeScene", "nodes": [0, 1]}],
        "nodes": [
            {"name": "Body", "mesh": 0, "translation": [0.0, 0.2, 0.0]},
            {"name": "Socket_Cargo", "children": [2], "translation": [0.0, 0.45, 0.0]},
            {"name": "Cargo", "mesh": 1},
        ],
        "meshes": meshes,
        "materials": [
            {"name": "ProbePlaceholderBody",
             "pbrMetallicRoughness": {"baseColorFactor": [1.0, 0.0, 1.0, 1.0]}},
            {"name": "ProbePlaceholderCargo",
             "pbrMetallicRoughness": {"baseColorFactor": [0.0, 1.0, 0.0, 1.0]}},
        ],
        "accessors": accessors,
        "bufferViews": views,
        "buffers": [{"byteLength": len(buffer)}],
    }

    json_chunk = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    json_chunk += b" " * (-len(json_chunk) % 4)
    bin_chunk = buffer + b"\x00" * (-len(buffer) % 4)
    total = 12 + 8 + len(json_chunk) + 8 + len(bin_chunk)
    header = struct.pack("<III", 0x46546C67, 2, total)
    json_header = struct.pack("<II", len(json_chunk), 0x4E4F534A)
    bin_header = struct.pack("<II", len(bin_chunk), 0x004E4942)
    return header + json_header + json_chunk + bin_header + bin_chunk


def main():
    if len(sys.argv) != 2:
        sys.exit("usage: make_probe_glb.py <output.glb>")
    data = build_glb()
    with open(sys.argv[1], "wb") as fh:
        fh.write(data)
    print("wrote %s (%d bytes)" % (sys.argv[1], len(data)))
    print("sha256 " + hashlib.sha256(data).hexdigest())


if __name__ == "__main__":
    main()
