#!/usr/bin/env python3
# ============================================================================
# SHADOW CITY — tools/free3d.py
# 100% FREE 3D asset pipeline (no API key, no credits):
#   reference image  ->  TripoSR (free Hugging Face Space)  ->  GLB
#   -> decimate to mobile budget (fast_simplification)      -> *_mobile.glb
#
# Usage:
#   python3 free3d.py <ref.png> <out_name> [target_tris=3000]
#   python3 free3d.py --batch   (processes every ref_*.png in cwd)
# ============================================================================
import os, sys, shutil, time
import numpy as np

def generate(ref_png, out_name, target=3000):
    from gradio_client import Client, handle_file
    import trimesh, fast_simplification
    from scipy.spatial import cKDTree

    print(f"[{out_name}] connecting to TripoSR space…")
    c = Client("stabilityai/TripoSR", verbose=False)

    print(f"[{out_name}] preprocess (bg removal)…")
    pre = c.predict(handle_file(ref_png), True, 0.85, api_name="/preprocess")

    print(f"[{out_name}] generating 3D…")
    result = c.predict(handle_file(pre), 256, api_name="/generate")
    glb = None
    for r in (result if isinstance(result, (list, tuple)) else [result]):
        if isinstance(r, str) and r.endswith(".glb") and os.path.exists(r):
            glb = r
    if glb is None:
        print(f"[{out_name}] !! no GLB returned"); return False
    raw = out_name + "_raw.glb"
    shutil.copy(glb, raw)

    print(f"[{out_name}] decimating to {target} tris…")
    m = trimesh.load(raw, force="mesh")
    colors = np.array(m.visual.vertex_colors)
    if len(m.faces) > target:
        verts, faces = fast_simplification.simplify(
            m.vertices.view(np.ndarray).astype(np.float32),
            m.faces.view(np.ndarray).astype(np.int32),
            target_count=target)
        d = trimesh.Trimesh(vertices=verts, faces=faces)
        tree = cKDTree(m.vertices)
        _, idx = tree.query(d.vertices)
        d.visual = trimesh.visual.ColorVisuals(d, vertex_colors=colors[idx])
    else:
        d = m
    out = out_name + "_mobile.glb"
    d.export(out)
    print(f"[{out_name}] ✓ {out}: {len(d.faces)} tris, "
          f"{os.path.getsize(out)//1024} KB  (raw was {len(m.faces)} tris)")
    return True

def main():
    if len(sys.argv) >= 2 and sys.argv[1] == "--batch":
        refs = sorted(f for f in os.listdir(".") if f.startswith("ref_") and f.endswith(".png"))
        print(f"batch: {len(refs)} references")
        ok = 0
        for r in refs:
            name = r[4:-4]
            target = 6000 if "character" in name else 3000
            try:
                if generate(r, name, target): ok += 1
            except Exception as e:
                print(f"[{name}] !! {str(e)[:160]}")
            time.sleep(3)   # be polite to the free Space
        print(f"done: {ok}/{len(refs)}")
    elif len(sys.argv) >= 3:
        generate(sys.argv[1], sys.argv[2],
                 int(sys.argv[3]) if len(sys.argv) > 3 else 3000)
    else:
        print(__doc__)

if __name__ == "__main__":
    main()
