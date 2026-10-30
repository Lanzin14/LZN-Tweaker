"""Verificações do grafo do graphify para o LznTweaker.

Lê apenas graphify-out/graph.json, o Git e tools/graph/expectations.json; nunca reescreve o grafo.
Comandos:
  check            integridade, cobertura de arquivos e atualidade do grafo (código de saída 1 em erro)
  snapshot [nome]  grava um resumo do grafo atual em graphify-out/snapshots/<nome>.json
  diff [nome]      compara o grafo atual com um snapshot: nós, dependências entre tipos e testes afetados
  risk [--top N]   tipos com mais dependentes, marcando áreas sensíveis e tipos sem teste que os referencie
"""
from __future__ import annotations

import argparse
import fnmatch
import json
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "graphify-out"
GRAPH = OUT / "graph.json"
SNAPSHOTS = OUT / "snapshots"
EXPECTATIONS = Path(__file__).with_name("expectations.json")

# Relações que ligam um tipo aos seus membros (métodos, campos, casos de enum, tipos aninhados).
MEMBER_RELATIONS = {"method", "defines", "case_of", "contains"}
# Relações que indicam dependência real no código (as inferidas ficam de fora da análise por tipo).
DEPENDENCY_RELATIONS = {"calls", "references", "inherits", "implements"}
TYPE_DECLARATION = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|file)\s+)*"
                              r"(?:class|record(?:\s+struct|\s+class)?|struct|interface|enum)\s+([A-Z]\w*)", re.MULTILINE)


def load_expectations() -> dict:
    return json.loads(EXPECTATIONS.read_text(encoding="utf-8"))


def load_graph() -> dict:
    if not GRAPH.exists():
        sys.exit(f"Grafo não encontrado em {GRAPH}. Rode /graphify ou 'graphify update .' primeiro.")
    return json.loads(GRAPH.read_text(encoding="utf-8"))


def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8").stdout


def ignored_patterns() -> list[str]:
    path = ROOT / ".graphifyignore"
    if not path.exists():
        return []
    return [line.strip() for line in path.read_text(encoding="utf-8").splitlines() if line.strip() and not line.startswith("#")]


def is_ignored(path: str, patterns: list[str]) -> bool:
    return any(path.startswith(p) if p.endswith("/") else fnmatch.fnmatch(path, p) for p in patterns)


def tracked_sources(expect: dict) -> list[str]:
    patterns = ignored_patterns()
    return [f for f in git("ls-files").splitlines()
            if Path(f).suffix.lower() in expect["source_extensions"] and not is_ignored(f, patterns)]


class Model:
    """Índices sobre graph.json: nós por arquivo, tipo dono de cada membro e dependências entre tipos."""

    def __init__(self, graph: dict):
        self.graph = graph
        self.nodes = {n["id"]: n for n in graph["nodes"]}
        self.links = graph.get("links") or graph.get("edges") or []
        self.by_file: dict[str, list[dict]] = defaultdict(list)
        for node in graph["nodes"]:
            if node.get("source_file"):
                self.by_file[node["source_file"].replace("\\", "/")].append(node)
        self.types = {i for i, n in self.nodes.items() if n.get("_callable_class") and n.get("source_file")}
        self.owner: dict[str, str] = {}
        for link in self.links:
            if link["relation"] in MEMBER_RELATIONS and link["source"] in self.types and link["target"] not in self.types:
                self.owner.setdefault(link["target"], link["source"])

    def type_of(self, node_id: str) -> str | None:
        seen = set()
        while node_id not in self.types and node_id in self.owner and node_id not in seen:
            seen.add(node_id)
            node_id = self.owner[node_id]
        return node_id if node_id in self.types else None

    def type_dependencies(self) -> set[tuple[str, str]]:
        deps = set()
        for link in self.links:
            if link["relation"] not in DEPENDENCY_RELATIONS or link.get("confidence") != "EXTRACTED":
                continue
            source, target = self.type_of(link["source"]), self.type_of(link["target"])
            if source and target and source != target:
                deps.add((source, target))
        return deps

    def name(self, node_id: str) -> str:
        node = self.nodes.get(node_id, {})
        return f"{node.get('label', node_id)} ({node.get('source_file', '?')})"


def is_test(path: str) -> bool:
    return path.replace("\\", "/").startswith("tests/")


def is_sensitive(path: str, expect: dict) -> bool:
    return any(path.replace("\\", "/").startswith(prefix) for prefix in expect["sensitive_paths"])


# ---------------------------------------------------------------- check

def command_check(_: argparse.Namespace) -> int:
    expect, graph = load_expectations(), load_graph()
    model = Model(graph)
    errors: list[str] = []
    warnings: list[str] = []

    # Integridade: o grafo final não deve ter pontas soltas nem placeholders de comunidade.
    dangling = [l for l in model.links if l["source"] not in model.nodes or l["target"] not in model.nodes]
    if dangling:
        errors.append(f"{len(dangling)} relação(ões) apontam para nós inexistentes no graph.json.")
    self_loops = [l for l in model.links if l["source"] == l["target"] and l["relation"] != "calls"]
    if self_loops:
        warnings.append(f"{len(self_loops)} relação(ões) de um nó com ele mesmo (chamadas recursivas não contam): " +
                        ", ".join(model.nodes[l['source']]['label'] for l in self_loops[:5]))
    placeholders = sorted({n.get("community_name") for n in graph["nodes"] if re.fullmatch(r"Community \d+", str(n.get("community_name")))})
    if placeholders:
        warnings.append(f"{len(placeholders)} comunidade(s) sem nome (ex.: {placeholders[0]}); rode /graphify para nomeá-las.")

    # Cobertura: todo arquivo-fonte versionado precisa gerar símbolos além do nó do próprio arquivo
    # (ou, como o .sln, ligar esse nó a outros), salvo os conhecidos como vazios.
    known_empty = expect["known_empty_files"]
    sources = tracked_sources(expect)
    linked = {e for l in model.links for e in (l["source"], l["target"])}
    symbols = {f: [n for n in nodes if n.get("label") != Path(f).name or n["id"] in linked]
               for f, nodes in model.by_file.items()}
    missing = [f for f in sources if not symbols.get(f) and f not in known_empty]
    if missing:
        errors.append("Arquivos sem símbolos no grafo (novos desde a última extração ou falha do extrator):\n    " + "\n    ".join(missing))
    stale_allow = [f for f in known_empty if symbols.get(f)]
    if stale_allow:
        warnings.append("Arquivos listados como vazios em expectations.json agora geram nós; remova-os da lista: " + ", ".join(stale_allow))
    excluded = [f for f in model.by_file if is_ignored(f, ignored_patterns())]
    if excluded:
        warnings.append(f"{len(excluded)} arquivo(s) ignorados pelo .graphifyignore ainda estão no grafo; reconstrua com /graphify (ex.: {excluded[0]}).")

    # Tipos C# declarados que não viraram nós indicam extração parcial (erro de parser).
    partial = []
    for path in sources:
        if not path.endswith(".cs") or path in known_empty:
            continue
        declared = set(TYPE_DECLARATION.findall((ROOT / path).read_text(encoding="utf-8", errors="replace")))
        present = {n["label"] for n in model.by_file.get(path, [])}
        lost = sorted(declared - present)
        if lost:
            partial.append(f"{path}: {', '.join(lost)}")
    if partial:
        errors.append("Tipos declarados ausentes do grafo (provável extração parcial):\n    " + "\n    ".join(partial))

    # Documentos precisam de extração semântica; o 'graphify update' só refaz o código.
    for path in sources:
        if Path(path).suffix.lower() in {".md", ".txt"} and not any(n.get("_origin") == "semantic" for n in model.by_file.get(path, [])):
            warnings.append(f"Documento sem extração semântica: {path} (rode /graphify --update no assistente).")

    # Atualidade: o grafo deve corresponder ao commit atual e não pode haver código alterado depois dele.
    head = git("rev-parse", "HEAD").strip()
    built = graph.get("built_at_commit")
    if built and built != head:
        changed = [f for f in git("diff", "--name-only", built, "HEAD").splitlines() if f in sources]
        if changed:
            warnings.append(f"Grafo gerado em {built[:7]}; {len(changed)} arquivo(s)-fonte mudaram até {head[:7]}. Rode 'graphify update .'.")
    dirty = [line[3:] for line in git("status", "--porcelain").splitlines() if line[3:] in sources]
    if dirty:
        warnings.append(f"{len(dirty)} arquivo(s)-fonte com alterações não commitadas podem não estar no grafo: " + ", ".join(dirty[:5]))

    origins = defaultdict(int)
    for n in graph["nodes"]:
        origins[n.get("_origin") or "externo"] += 1
    inferred = sum(1 for l in model.links if l.get("confidence") != "EXTRACTED")
    print(f"Grafo: {len(model.nodes)} nós ({dict(origins)}), {len(model.links)} relações ({inferred} inferidas), "
          f"{len({n.get('community') for n in graph['nodes']})} comunidades, commit {str(built)[:7]}.")
    print(f"Cobertura: {len(sources) - len(missing)}/{len(sources)} arquivos-fonte representados "
          f"({len(known_empty)} vazios esperados).")
    for message in warnings:
        print(f"AVISO: {message}")
    for message in errors:
        print(f"ERRO: {message}")
    print("Resultado: " + ("FALHOU" if errors else "OK"))
    return 1 if errors else 0


# ---------------------------------------------------------------- snapshot / diff

def summarize(graph: dict) -> dict:
    model = Model(graph)
    return {
        "commit": graph.get("built_at_commit"),
        "nodes": {i: [n.get("label"), n.get("source_file")] for i, n in model.nodes.items()},
        "type_dependencies": sorted(f"{a}|{b}" for a, b in model.type_dependencies()),
    }


def command_snapshot(args: argparse.Namespace) -> int:
    SNAPSHOTS.mkdir(parents=True, exist_ok=True)
    target = SNAPSHOTS / f"{args.name}.json"
    target.write_text(json.dumps(summarize(load_graph()), ensure_ascii=False), encoding="utf-8")
    print(f"Snapshot gravado em {target.relative_to(ROOT)}")
    return 0


def command_diff(args: argparse.Namespace) -> int:
    source = SNAPSHOTS / f"{args.name}.json"
    if not source.exists():
        sys.exit(f"Snapshot {source} não existe. Grave um antes da mudança com: snapshot {args.name}")
    expect, before, graph = load_expectations(), json.loads(source.read_text(encoding="utf-8")), load_graph()
    after, model = summarize(graph), Model(graph)

    removed = set(before["nodes"]) - set(after["nodes"])
    added = set(after["nodes"]) - set(before["nodes"])
    changed_files: dict[str, list[str]] = defaultdict(list)
    for node_id in removed:
        changed_files[before["nodes"][node_id][1] or "(externo)"].append(f"- {before['nodes'][node_id][0]}")
    for node_id in added:
        changed_files[after["nodes"][node_id][1] or "(externo)"].append(f"+ {after['nodes'][node_id][0]}")

    deps_before, deps_after = set(before["type_dependencies"]), set(after["type_dependencies"])
    label = lambda node_id: (after["nodes"].get(node_id) or before["nodes"].get(node_id) or [node_id])[0]
    print(f"Comparando snapshot '{args.name}' ({str(before['commit'])[:7]}) com o grafo atual ({str(after['commit'])[:7]})")
    print(f"Nós: +{len(added)} / -{len(removed)} em {len(changed_files)} arquivo(s)")
    for path in sorted(changed_files):
        flag = " [SENSÍVEL]" if is_sensitive(path, expect) else ""
        items = sorted(changed_files[path])
        print(f"  {path}{flag}: " + ", ".join(items[:12]) + (f" … (+{len(items) - 12})" if len(items) > 12 else ""))

    for title, deps in (("Novas dependências entre tipos", deps_after - deps_before),
                        ("Dependências removidas", deps_before - deps_after)):
        if deps:
            print(f"{title}: {len(deps)}")
            for dep in sorted(deps):
                a, b = dep.split("|")
                print(f"  {label(a)} -> {label(b)}")

    # Tipos tocados = tipos donos de nós alterados + extremidades de dependências alteradas.
    touched = {model.type_of(i) or i for i in added if i in model.nodes}
    touched |= {i for dep in deps_after ^ deps_before for i in dep.split("|") if i in model.nodes}
    touched |= {i for i in model.types if model.nodes[i].get("source_file") in changed_files}
    impacted = impact_closure(model, touched, depth=args.depth)
    tests = sorted({model.nodes[i]["source_file"] for i in impacted if is_test(model.nodes[i].get("source_file", ""))})
    sensitive = sorted({model.nodes[i]["source_file"] for i in impacted if is_sensitive(model.nodes[i].get("source_file", ""), expect)})
    print(f"Tipos impactados (dependentes até profundidade {args.depth}): {len(impacted)}")
    if sensitive:
        print("  Áreas sensíveis no raio de impacto: " + ", ".join(sensitive))
    print("  Testes que exercitam esses tipos: " + (", ".join(tests) if tests else "nenhum encontrado no grafo"))
    return 0


def impact_closure(model: Model, start: set[str], depth: int) -> set[str]:
    dependents: dict[str, set[str]] = defaultdict(set)
    for a, b in model.type_dependencies():
        dependents[b].add(a)
    frontier, seen = set(start), set(start)
    for _ in range(depth):
        frontier = {d for t in frontier for d in dependents[t]} - seen
        seen |= frontier
    return seen


# ---------------------------------------------------------------- risk

def command_risk(args: argparse.Namespace) -> int:
    expect, model = load_expectations(), Model(load_graph())
    dependents: dict[str, set[str]] = defaultdict(set)
    for a, b in model.type_dependencies():
        dependents[b].add(a)
    # O extrator nem sempre liga acesso estático (ex.: SupportedGames.All) ao tipo; por isso a menção
    # textual nos testes complementa o grafo, e a coluna diz de onde veio a evidência.
    test_text = "\n".join((ROOT / f).read_text(encoding="utf-8", errors="replace")
                          for f in git("ls-files", "tests").splitlines() if f.endswith(".cs"))
    rows = []
    for type_id in model.types:
        path = model.nodes[type_id]["source_file"]
        if is_test(path):
            continue
        production = {u for u in dependents[type_id] if not is_test(model.nodes[u]["source_file"])}
        if any(is_test(model.nodes[u]["source_file"]) for u in impact_closure(model, {type_id}, 2)):
            evidence = "grafo"
        elif re.search(rf"\b{re.escape(model.nodes[type_id]['label'])}\b", test_text):
            evidence = "texto"
        else:
            evidence = "NÃO"
        rows.append((len(production), is_sensitive(path, expect), evidence, type_id))
    rows.sort(key=lambda r: (-r[0], not r[1], r[2] != "NÃO"))
    print(f"{'Dependentes':>11}  {'Sensível':8}  {'Teste':5}  Tipo")
    for count, sensitive, evidence, type_id in rows[:args.top]:
        print(f"{count:>11}  {'sim' if sensitive else '':8}  {evidence:5}  {model.name(type_id)}")
    untested = [r for r in rows if r[1] and r[2] == "NÃO" and r[0] > 0]
    if untested:
        print("\nTipos em áreas sensíveis, usados pelo app, sem nenhum teste que os alcance ou mencione:")
        for count, _, _, type_id in untested:
            print(f"  {count} dependente(s)  {model.name(type_id)}")
    print("\nDependentes = tipos de produção que referenciam ou chamam o tipo (só relações EXTRACTED).")
    print("Teste: 'grafo' = um teste alcança o tipo em até 2 níveis; 'texto' = o nome aparece em tests/ sem ligação no grafo.")
    print("Muitos dependentes indicam raio de impacto, não necessariamente um problema de design.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("check").set_defaults(run=command_check)
    snap = sub.add_parser("snapshot")
    snap.add_argument("name", nargs="?", default="baseline")
    snap.set_defaults(run=command_snapshot)
    diff = sub.add_parser("diff")
    diff.add_argument("name", nargs="?", default="baseline")
    diff.add_argument("--depth", type=int, default=2)
    diff.set_defaults(run=command_diff)
    risk = sub.add_parser("risk")
    risk.add_argument("--top", type=int, default=20)
    risk.set_defaults(run=command_risk)
    args = parser.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    return args.run(args)


if __name__ == "__main__":
    sys.exit(main())
