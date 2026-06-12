"""Extract the MSBuild-layer dependency graph for RssBandit.

Parses every .csproj tracked by git: target framework, output type,
project references, binary (HintPath) references, NuGet packages.
Emits projects.json and a Mermaid project-dependency diagram.
Complements graphify-out/graph.json, which covers the type/member level
but knows nothing about project boundaries.

Usage: python docs/architecture/codegraph/extract_msbuild.py  (from repo root)
"""
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

OUT_DIR = Path("docs/architecture/codegraph")


def strip_ns(tree):
    for el in tree.iter():
        if "}" in el.tag:
            el.tag = el.tag.split("}", 1)[1]
    return tree


def parse_csproj(path: Path) -> dict:
    tree = strip_ns(ET.parse(path))
    root = tree.getroot()
    info = {
        "path": path.as_posix(),
        "name": path.stem,
        "sdk_style": root.get("Sdk") is not None,
        "tools_version": root.get("ToolsVersion"),
        "target_framework": None,
        "output_type": "Library",
        "use_windows_forms": False,
        "project_references": [],
        "binary_references": [],
        "package_references": [],
    }
    for el in root.iter("TargetFramework"):
        info["target_framework"] = (el.text or "").strip()
    for el in root.iter("TargetFrameworks"):
        info["target_framework"] = (el.text or "").strip()
    for el in root.iter("TargetFrameworkVersion"):
        info["target_framework"] = "net-fx " + (el.text or "").strip()
    for el in root.iter("OutputType"):
        if el.text:
            info["output_type"] = el.text.strip()
    for el in root.iter("UseWindowsForms"):
        info["use_windows_forms"] = (el.text or "").strip().lower() == "true"
    for el in root.iter("ProjectReference"):
        inc = el.get("Include")
        if inc:
            ref = (path.parent / Path(inc.replace("\\", "/"))).resolve()
            try:
                ref = ref.relative_to(Path.cwd().resolve())
            except ValueError:
                pass
            info["project_references"].append(Path(ref).as_posix())
    for el in root.iter("Reference"):
        inc = (el.get("Include") or "").split(",")[0].strip()
        hint = None
        for h in el.iter("HintPath"):
            hint = (h.text or "").strip().replace("\\", "/")
        if hint:
            info["binary_references"].append({"name": inc, "hint_path": hint})
    for el in root.iter("PackageReference"):
        inc = el.get("Include")
        if inc:
            info["package_references"].append(
                {"name": inc, "version": el.get("Version")}
            )
    return info


def solution_projects(sln: Path) -> list[str]:
    pat = re.compile(r'Project\("\{[^}]+\}"\)\s*=\s*"[^"]+",\s*"([^"]+\.csproj)"')
    names = []
    for m in pat.finditer(sln.read_text(encoding="utf-8", errors="replace")):
        rel = m.group(1).replace("\\", "/")
        names.append((sln.parent / rel).resolve())
    out = []
    for p in names:
        try:
            out.append(p.relative_to(Path.cwd().resolve()).as_posix())
        except ValueError:
            out.append(p.as_posix())
    return out


def mermaid_id(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9_]", "_", name)


def main() -> None:
    files = subprocess.run(
        ["git", "ls-files", "*.csproj"], capture_output=True, text=True, check=True
    ).stdout.splitlines()
    projects = {}
    for f in files:
        try:
            info = parse_csproj(Path(f))
            projects[info["path"]] = info
        except ET.ParseError as e:
            print(f"WARN: failed to parse {f}: {e}", file=sys.stderr)

    main_sln = Path("source/RSS Bandit.sln")
    in_main = set(solution_projects(main_sln)) if main_sln.exists() else set()
    for p in projects.values():
        p["in_main_solution"] = p["path"] in in_main

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / "projects.json").write_text(
        json.dumps(list(projects.values()), indent=2), encoding="utf-8"
    )

    lines = ["# RssBandit MSBuild Dependency Graph", ""]
    lines.append(f"{len(projects)} projects; {len(in_main)} in `source/RSS Bandit.sln`.")
    lines.append("")
    lines.append("## Project dependency diagram (ProjectReference edges)")
    lines.append("")
    lines.append("```mermaid")
    lines.append("flowchart TD")
    node_ids = {}
    used_ids = set()
    for p in projects.values():
        nid = mermaid_id(p["name"])
        while nid in used_ids:
            nid += "_"
        used_ids.add(nid)
        node_ids[p["path"]] = nid
    for p in projects.values():
        tfm = p["target_framework"] or "?"
        shape = ("[[", "]]") if p["output_type"].lower() == "winexe" else ("[", "]")
        cls = ":::main" if p["in_main_solution"] else ""
        lines.append(
            f'    {node_ids[p["path"]]}{shape[0]}"{p["name"]}<br/>{tfm}"{shape[1]}{cls}'
        )
    for p in projects.values():
        for ref in p["project_references"]:
            tgt = projects.get(ref)
            tgt_id = node_ids[tgt["path"]] if tgt else mermaid_id(Path(ref).stem)
            lines.append(f'    {node_ids[p["path"]]} --> {tgt_id}')
    lines.append("    classDef main fill:#cde4ff,stroke:#2266aa")
    lines.append("```")
    lines.append("")

    lines.append("## Projects")
    lines.append("")
    lines.append("| Project | TFM | Output | SDK-style | In main sln | Proj refs | Binary refs | Packages |")
    lines.append("|---|---|---|---|---|---|---|---|")
    for p in sorted(projects.values(), key=lambda x: (not x["in_main_solution"], x["name"])):
        lines.append(
            f'| {p["name"]} | {p["target_framework"] or "?"} | {p["output_type"]} '
            f'| {"yes" if p["sdk_style"] else "LEGACY"} | {"yes" if p["in_main_solution"] else ""} '
            f'| {len(p["project_references"])} | {len(p["binary_references"])} | {len(p["package_references"])} |'
        )
    lines.append("")

    lines.append("## Binary (HintPath) dependencies — modernization risk surface")
    lines.append("")
    bin_owners = {}
    for p in projects.values():
        for b in p["binary_references"]:
            bin_owners.setdefault(b["name"], []).append(p["name"])
    for name, owners in sorted(bin_owners.items()):
        lines.append(f"- `{name}` — used by {', '.join(sorted(set(owners)))}")
    lines.append("")

    lines.append("## NuGet packages")
    lines.append("")
    pkg_owners = {}
    for p in projects.values():
        for pk in p["package_references"]:
            pkg_owners.setdefault((pk["name"], pk["version"]), []).append(p["name"])
    for (name, ver), owners in sorted(pkg_owners.items()):
        lines.append(f"- `{name}` {ver or ''} — {', '.join(sorted(set(owners)))}")
    lines.append("")

    (OUT_DIR / "msbuild_graph.md").write_text("\n".join(lines), encoding="utf-8")
    n_edges = sum(len(p["project_references"]) for p in projects.values())
    n_legacy = sum(1 for p in projects.values() if not p["sdk_style"])
    print(f"projects: {len(projects)} ({n_legacy} legacy-style), edges: {n_edges}, "
          f"binary refs: {len(bin_owners)}, packages: {len(pkg_owners)}")
    print(f"wrote {OUT_DIR}/projects.json and {OUT_DIR}/msbuild_graph.md")


if __name__ == "__main__":
    main()
