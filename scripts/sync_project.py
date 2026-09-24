#!/usr/bin/env python3
"""Carga do GitHub Project #6 (tassosgomes) a partir da fonte de verdade do repo.

Fontes (nunca editadas aqui, apenas lidas):
  - backlog/capabilities.md  -> titulo, dominio, fase, prioridade, dependencias, origem
  - flow-state.json          -> etapa (stage) e artefatos de cada CAP
  - tasks/*/tasks.md         -> checklist de tasks, mapa de entrega e status do plano

O que cria/atualiza (idempotente, reexecutavel):
  1. 5 milestones de fase no repo (sem due date: fase abre por criterio, nao por calendario)
  2. labels: cap, task e dominio:<slug> (16 dominios)
  3. issue por capacidade  -> [CAP-XXX] titulo
  4. issue por task        -> [CAP-XXX/N.N] titulo, ligada ao CAP por sub-issue
  5. itens no Project #6   -> campos Status e Priority (Milestone/Parent/Repository
                              sao espelhados automaticamente pelo GitHub)

Uso:
  python3 scripts/sync_project.py --dry-run          # mostra o plano sem escrever
  python3 scripts/sync_project.py --only CAP-030     # só uma capacidade (+ suas tasks)
  python3 scripts/sync_project.py                    # carga completa

Requer: gh autenticado (GH_TOKEN ou `gh auth login`) com permissao de escrita.
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import unicodedata
from pathlib import Path

REPO = "tassosgomes/code-for-coders"
REPO_OWNER, REPO_NAME = REPO.split("/")
REPO_ID = f"{REPO_OWNER}/{REPO_NAME}"
PROJECT_OWNER = "tassosgomes"
PROJECT_NUMBER = 6
BLOB_BASE = f"https://github.com/{REPO}/blob/main"
ROOT = Path(__file__).resolve().parent.parent

# Fases do capabilities.md ("Estrategia de Sequenciamento" 4. e "Definicao do MVP")
MILESTONES = {
    "MVP": {
        "title": "MVP (Fase 1)",
        "description": (
            "Vender e assistir fim a fim. Passos 1-11 do backlog. "
            "Criterio de conclusao: uma compra real percorre CAP-011 -> CAP-008 sem intervencao "
            "manual, e um aluno percorre CAP-007 -> CAP-017 com marca d'agua, URL assinada e "
            "progresso persistido."
        ),
    },
    "Fase 2": {
        "title": "Fase 2 — Receita recorrente",
        "description": (
            "Passos 12-18 do backlog. Criterio: assinatura renova sozinha, falha dispara a regua "
            "e suspende acesso, e a venda gera NFS-e."
        ),
    },
    "Fase 3": {
        "title": "Fase 3 — Aprendizagem comprovada",
        "description": (
            "Passos 19-22 do backlog. Criterio: trilha condicionada a avaliacao e certificado "
            "validado por um terceiro."
        ),
    },
    "Fase 4": {
        "title": "Fase 4 — Retencao e comunidade",
        "description": (
            "Passos 23-26 do backlog. Criterio: duvida pedagogica e chamado correm separados; "
            "engajamento e mensuravel."
        ),
    },
    "Fase 5": {
        "title": "Fase 5 — Escala e governanca",
        "description": (
            "Passos 27-31 do backlog. Abre quando os passos 23-26 (Fase 4) estiverem concluidos. "
            "Capacidades presas a decisoes de negocio (A1, AB03, AB04, regras de turma e "
            "comissionamento)."
        ),
    },
}

STAGE_TO_STATUS = {
    "backlog": "Backlog",
    "domain": "In progress",
    "prd": "In progress",
    "techspec": "In progress",
    "contract": "In progress",
    "tasks": "Ready",
    "in_progress": "In progress",
    "done": "Done",
}
PRIORITY_TO_FIELD = {"Alta": "P0", "Média": "P1", "Baixa": "P2"}
LABEL_COLOR = {"cap": "1d76db", "task": "fbca04", "dominio": "c2e0c6"}

CAP_HEADER = re.compile(r"^#### (CAP-\d{3}) — (.+)$", re.MULTILINE)
DOMAIN_HEADER = re.compile(r"^### Domínio: (.+)$", re.MULTILINE)
BULLET = re.compile(r"^- \*\*(.+?):\*\*\s*(.*)$")
FASE_IN_LINE = re.compile(r"·\s*\*\*Fase:\*\*\s*(.+)$")
PRIO_IN_LINE = re.compile(r"^(\w+)\s*·")
TASK_LINE = re.compile(r"^- \[( |x)\] (\d+\.\d+) (.+)$")
MAPA_TASK_CELL = re.compile(r"(?:\[(\d+\.\d+)\]\((\S+?)\)|(\d+\.\d+))")


# --------------------------------------------------------------------------- util
def slugify(text: str) -> str:
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    text = re.sub(r"[^a-zA-Z0-9]+", "-", text.lower())
    return text.strip("-")


def clean(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


class Gh:
    """Wrapper fino sobre `gh`. Em dry-run, so registra o que faria."""

    def __init__(self, dry_run: bool):
        self.dry_run = dry_run

    def run(self, *args: str, check: bool = True) -> str:
        cmd = ["gh", *args]
        if self.dry_run:
            shown = [arg if len(arg) <= 120 else arg[:117] + "..." for arg in args]
            print(f"  DRY  {' '.join(shown)}")
            return ""
        proc = subprocess.run(cmd, capture_output=True, text=True)
        if check and proc.returncode != 0:
            raise RuntimeError(f"falhou: {' '.join(cmd)}\n{proc.stderr.strip()}")
        return proc.stdout

    def gql(self, query: str, variables: dict | None = None, check: bool = True) -> dict:
        # --input com JSON: gh -f/-F nao converte arrays (so int/bool/null),
        # e labelIds/[ID!] precisa de lista de verdade.
        body = json.dumps({"query": query, "variables": variables or {}}, ensure_ascii=False)
        if self.dry_run:
            print(f"  DRY gql {query.splitlines()[0].strip()[:60]}... vars={variables}")
            return {}
        proc = subprocess.run(
            ["gh", "api", "graphql", "--input", "-"],
            input=body, capture_output=True, text=True,
        )
        if proc.returncode != 0 or not proc.stdout.strip():
            if not check:
                return {}
            raise RuntimeError(f"gql falhou:\n{proc.stderr.strip()}")
        data = json.loads(proc.stdout)
        if data.get("errors"):
            if not check:
                return {}
            raise RuntimeError(f"gql errors: {json.dumps(data['errors'], ensure_ascii=False)}")
        return data["data"]


# ----------------------------------------------------------------- fonte: capacidades
def load_capabilities() -> list[dict]:
    text = (ROOT / "backlog" / "capabilities.md").read_text(encoding="utf-8")
    caps: list[dict] = []
    current_domain = None
    block_lines: list[str] = []
    header: tuple[str, str] | None = None

    def flush():
        if not header:
            return
        caps.append(parse_cap(header[0], header[1], current_domain, block_lines))

    for line in text.splitlines():
        if m := DOMAIN_HEADER.match(line):
            flush()
            header, block_lines = None, []
            current_domain = m.group(1).strip()
        elif m := CAP_HEADER.match(line):
            flush()
            header, block_lines = (m.group(1), m.group(2).strip()), []
        elif header is not None:
            block_lines.append(line)
    flush()
    return caps


def parse_cap(cap_id: str, title: str, domain: str | None, lines: list[str]) -> dict:
    fields: dict[str, str] = {}
    current: str | None = None
    for line in lines:
        if line.startswith("#") or line.strip() == "---":  # fim do bloco
            break
        if m := BULLET.match(line):
            current = m.group(1)
            fields[current] = m.group(2).strip()
        elif current and line.strip():
            fields[current] = f"{fields[current]} {line.strip()}"
    fields = {k: clean(v) for k, v in fields.items()}

    prio_line = fields.get("Prioridade", "")
    prioridade = (PRIO_IN_LINE.match(prio_line) or re.match(r"^(\w+)", prio_line) or None)
    prioridade = prioridade.group(1) if prioridade else ""
    fase = FASE_IN_LINE.search(prio_line)
    fase = fase.group(1).strip() if fase else ""
    fase = clean(re.sub(r"\*+", "", fase))  # "**MVP em versão mínima** (…)" -> "MVP em versão mínima (…)"
    if fase.startswith("MVP"):
        fase_key = "MVP"
    elif fase in MILESTONES:
        fase_key = fase
    else:
        fase_key = ""

    return {
        "id": cap_id,
        "title": title,
        "domain": domain or "",
        "domain_slug": slugify(domain or ""),
        "objetivo": fields.get("Objetivo", ""),
        "valor": fields.get("Valor de negócio", ""),
        "fluxo": fields.get("Resumo do fluxo", ""),
        "dependencias": fields.get("Dependências", ""),
        "origem": fields.get("Origem na visão", ""),
        "prioridade": prioridade,
        "fase": fase,
        "fase_key": fase_key if fase_key in MILESTONES else "",
        "escopo_minimo": fields.get("Escopo mínimo no MVP", ""),
    }


def load_flow_state() -> dict:
    return json.loads((ROOT / "flow-state.json").read_text(encoding="utf-8"))


# ------------------------------------------------------------------- fonte: tasks
def load_tasks(flow: dict) -> dict[str, list[dict]]:
    """dir relativo -> lista de tasks. Mapeia dir -> CAP pelos artefatos do flow-state."""
    dir_to_cap = {
        str(Path(art["prd"]).parent) or ".": cap["id"]
        for cap in flow["capabilities"]
        for art in [cap.get("artifacts", {})]
        if art.get("prd")
    }
    tasks_by_cap: dict[str, list[dict]] = {}
    for dir_rel, cap_id in dir_to_cap.items():
        tasks_md = ROOT / dir_rel / "tasks.md"
        if not tasks_md.exists():
            continue
        text = tasks_md.read_text(encoding="utf-8")
        plan_status = ""
        if m := re.search(r"^\s*>?\s*\*\*Status do plano:\*\*\s*(.+)$", text, re.MULTILINE):
            plan_status = clean(m.group(1))

        mapa: dict[str, dict] = {}
        for line in text.splitlines():
            if not line.lstrip().startswith("|"):
                continue
            cells = [clean(c) for c in line.strip().strip("|").split("|")]
            if len(cells) < 5 or not (m := MAPA_TASK_CELL.match(cells[1])):
                continue
            num = m.group(1) or m.group(3)
            mapa[num] = {
                "fatia": cells[0],
                "file": m.group(2) or f"{num.split('.')[0]}_task.md",
                "cells": cells,
            }

        tasks: list[dict] = []
        for line in text.splitlines():
            if m := TASK_LINE.match(line):
                num, title = m.group(2), clean(m.group(3))
                info = mapa.get(num, {})
                cells = info.get("cells", [])
                tasks.append(
                    {
                        "num": num,
                        "title": title,
                        "done": m.group(1) == "x",
                        "dir": dir_rel,
                        "file": info.get("file", f"{num.split('.')[0]}_task.md"),
                        "behavior": cells[2] if len(cells) > 3 else "",
                        "gate": cells[3] if len(cells) > 3 else "",
                        "blocked": cells[4] if len(cells) > 4 else "",
                        "plan_status": plan_status,
                    }
                )
        tasks_by_cap[cap_id] = tasks
    return tasks_by_cap


# ------------------------------------------------------------------------ ensure
def repo_node_id(gh: Gh) -> str:
    if gh.dry_run:
        return "DRY"
    if not hasattr(repo_node_id, "_cache"):
        data = gh.gql(
            'query { repository(owner: "%s", name: "%s") { id } }' % (REPO_OWNER, REPO_NAME)
        )
        repo_node_id._cache = data["repository"]["id"]
    return repo_node_id._cache


def ensure_milestones(gh: Gh) -> dict[str, dict]:
    """title -> {number, id} (REST: GraphQL nao tem createMilestone)."""
    existing = {
        m["title"]: {"number": m["number"], "id": m["node_id"]}
        for m in json.loads(
            gh.run("api", f"repos/{REPO_ID}/milestones?state=all&per_page=100") or "[]"
        )
    }
    for spec in MILESTONES.values():
        if spec["title"] in existing:
            continue
        print(f"milestone: {spec['title']}")
        out = gh.run(
            "api", "--method", "POST", f"repos/{REPO_ID}/milestones",
            "-f", f"title={spec['title']}",
            "-f", f"description={spec['description']}",
        )
        created = json.loads(out) if out else {"number": 0, "node_id": None}
        existing[spec["title"]] = {"number": created["number"], "id": created["node_id"]}
    return existing


def ensure_labels(gh: Gh, caps: list[dict]) -> dict[str, str]:
    """name -> node id."""
    existing = {
        l["name"]: l["node_id"]
        for l in json.loads(gh.run("api", f"repos/{REPO_ID}/labels?per_page=100") or "[]")
    }
    wanted = {
        "cap": ("Capacidade do backlog (CAP-XXX)", LABEL_COLOR["cap"]),
        "task": ("Task de implementacao de um PRD", LABEL_COLOR["task"]),
    }
    for cap in caps:
        wanted[f"dominio:{cap['domain_slug']}"] = (
            f"Dominio dono: {cap['domain']}",
            LABEL_COLOR["dominio"],
        )
    for name, (desc, color) in sorted(wanted.items()):
        if name in existing:
            continue
        print(f"label: {name}")
        out = gh.run(
            "api", "--method", "POST", f"repos/{REPO_ID}/labels",
            "-f", f"name={name}", "-f", f"color={color}", "-f", f"description={desc}",
        )
        if out:
            existing[name] = json.loads(out)["node_id"]
    return existing


def fetch_issues(gh: Gh) -> dict[str, dict]:
    """title -> {number, id} de todas as issues (abertas e fechadas)."""
    query = """
    query($cursor: String) {
      repository(owner: "%s", name: "%s") {
        issues(first: 100, after: $cursor, states: [OPEN, CLOSED]) {
          pageInfo { hasNextPage endCursor }
          nodes { number id title parent { number } }
        }
      }
    }""" % (REPO_OWNER, REPO_NAME)
    issues: dict[str, dict] = {}
    cursor = None
    while True:
        data = gh.gql(query, {"cursor": cursor} if cursor else {})
        if not data:
            return issues
        conn = data["repository"]["issues"]
        for node in conn["nodes"]:
            issues[node["title"]] = node
        if not conn["pageInfo"]["hasNextPage"]:
            return issues
        cursor = conn["pageInfo"]["endCursor"]


def ensure_issue(
    gh: Gh,
    title: str,
    body: str,
    milestone_id: str | None,
    label_ids: list[str],
    known: dict,
    parent_id: str | None = None,
) -> dict:
    if title in known:
        return known[title]
    print(f"issue: {title}")
    data = gh.gql(
        """
        mutation($repoId: ID!, $title: String!, $body: String!,
                 $milestoneId: ID, $labelIds: [ID!], $parentIssueId: ID) {
          createIssue(input: {
            repositoryId: $repoId, title: $title, body: $body,
            milestoneId: $milestoneId, labelIds: $labelIds,
            parentIssueId: $parentIssueId
          }) { issue { number id title parent { number } } }
        }""",
        {
            "repoId": repo_node_id(gh),
            "title": title,
            "body": body,
            "milestoneId": milestone_id,
            "labelIds": label_ids or None,
            "parentIssueId": parent_id,
        },
    )
    if not data:
        return {"number": None, "id": None, "title": title, "parent": None}
    issue = data["createIssue"]["issue"]
    known[title] = issue
    return issue


def ensure_sub_issue(gh: Gh, parent: dict, child: dict, known: dict) -> None:
    if gh.dry_run:
        print(f"sub-issue: {child.get('title')} -> {parent.get('title')}")
        return
    if not parent.get("id") or not child.get("id"):
        return
    # reconsulta o parent atual (a issue pode ter sido criada numa execucao anterior)
    fresh = gh.gql(
        """
        query { repository(owner: "%s", name: "%s") { issue(number: %d) { parent { number } } } }
        """ % (REPO_OWNER, REPO_NAME, child["number"]),
        check=not gh.dry_run,
    )
    current = (fresh.get("repository", {}).get("issue") or {}).get("parent")
    if current and current["number"] == parent["number"]:
        return
    print(f"sub-issue: {child['title']} -> {parent['title']}")
    gh.gql(
        """
        mutation($issueId: ID!, $subIssueId: ID!) {
          addSubIssue(input: {issueId: $issueId, subIssueId: $subIssueId}) {
            subIssue { id }
          }
        }""",
        {"issueId": parent["id"], "subIssueId": child["id"]},
    )


# ------------------------------------------------------------------------- project
def project_state(gh: Gh) -> dict:
    query = """
    query($cursor: String) {
      user(login: "%s") {
        projectV2(number: %d) {
          id
          fields(first: 40) {
            nodes {
              ... on ProjectV2FieldCommon { id name dataType }
              ... on ProjectV2SingleSelectField { id name options { id name } }
            }
          }
          items(first: 100, after: $cursor) {
            pageInfo { hasNextPage endCursor }
            nodes {
              id
              content { ... on Issue { number } }
              fieldValues(first: 20) {
                nodes {
                  ... on ProjectV2ItemFieldSingleSelectValue {
                    name
                    field { ... on ProjectV2FieldCommon { name } }
                  }
                }
              }
            }
          }
        }
      }
    }""" % (PROJECT_OWNER, PROJECT_NUMBER)

    if gh.dry_run:
        statuses = sorted(set(STAGE_TO_STATUS.values()) | {"Backlog", "Ready", "In progress", "In review", "Done"})
        return {
            "id": "DRY",
            "fields": {"Status": "DRY", "Priority": "DRY"},
            "options": {
                "Status": {name: f"DRY-{name}" for name in statuses},
                "Priority": {name: f"DRY-{name}" for name in PRIORITY_TO_FIELD.values()},
            },
            "items": {},
        }
    state = {"id": None, "fields": {}, "options": {}, "items": {}}
    cursor = None
    while True:
        data = gh.gql(query, {"cursor": cursor} if cursor else {})
        if not data:
            return state
        project = data["user"]["projectV2"]
        state["id"] = project["id"]
        for f in project["fields"]["nodes"]:
            if not f:
                continue
            state["fields"][f["name"]] = f["id"]
            if f.get("options"):
                state["options"][f["name"]] = {o["name"]: o["id"] for o in f["options"]}
        for item in project["items"]["nodes"]:
            number = (item.get("content") or {}).get("number")
            if number is None:
                continue
            values = {
                v["field"]["name"]: v["name"]
                for v in item["fieldValues"]["nodes"]
                if v and v.get("field")
            }
            state["items"][number] = {"id": item["id"], "values": values}
        if not project["items"]["pageInfo"]["hasNextPage"]:
            return state
        cursor = project["items"]["pageInfo"]["endCursor"]


def ensure_project_item(gh: Gh, state: dict, issue: dict) -> dict | None:
    if gh.dry_run:
        print(f"project +: {issue.get('title')}")
        item = {"id": "DRY", "values": {}}
        state["items"].setdefault(id(item), item)  # nao persiste: so simula
        return item
    if not issue.get("id") or not issue.get("number"):
        return None
    if issue["number"] in state["items"]:
        return state["items"][issue["number"]]
    print(f"project +: #{issue['number']} {issue['title']}")
    data = gh.gql(
        """
        mutation($projectId: ID!, $contentId: ID!) {
          addProjectV2ItemById(input: {projectId: $projectId, contentId: $contentId}) {
            item { id }
          }
        }""",
        {"projectId": state["id"], "contentId": issue["id"]},
        check=False,
    )
    if data:
        item = {"id": data["addProjectV2ItemById"]["item"]["id"], "values": {}}
        state["items"][issue["number"]] = item
        return item

    # "Content already exists": sub-issue pode herdar o item do pai no projeto.
    fresh = project_state(gh)
    state["items"].update(fresh["items"])
    return state["items"].get(issue["number"])


def set_field(gh: Gh, state: dict, item: dict, field: str, option: str, issue_number: int) -> None:
    if item["values"].get(field) == option:
        return
    field_id = state["fields"].get(field)
    option_id = state["options"].get(field, {}).get(option)
    if not field_id or not option_id:
        raise RuntimeError(f"campo/opcao nao encontrada no project: {field} / {option}")
    print(f"project field: {('#' + str(issue_number)) if issue_number else '(item)'} {field}={option}")
    gh.gql(
        """
        mutation($projectId: ID!, $itemId: ID!, $fieldId: ID!, $optionId: String!) {
          updateProjectV2ItemFieldValue(input: {
            projectId: $projectId, itemId: $itemId, fieldId: $fieldId,
            value: {singleSelectOptionId: $optionId}
          }) { projectV2Item { id } }
        }""",
        {
            "projectId": state["id"],
            "itemId": item["id"],
            "fieldId": field_id,
            "optionId": option_id,
        },
    )
    item["values"][field] = option


# ---------------------------------------------------------------------------- body
def cap_body(cap: dict, flow_cap: dict, tasks: list[dict]) -> str:
    stage = flow_cap.get("stage", "?")
    status = STAGE_TO_STATUS.get(stage, "Backlog")
    lines = [
        "> **Cadastro automatico** a partir de "
        f"[`backlog/capabilities.md`]({BLOB_BASE}/backlog/capabilities.md) e "
        f"[`flow-state.json`]({BLOB_BASE}/flow-state.json). "
        "O estado TSG vive no repo: atualize la e reexecute "
        "`python3 scripts/sync_project.py`.",
        "",
        "| | |",
        "|---|---|",
        f"| **Dominio** | {cap['domain']} |",
        f"| **Fase** | {cap['fase']} |",
        f"| **Prioridade** | {cap['prioridade']} ({PRIORITY_TO_FIELD.get(cap['prioridade'], '-')}) |",
        f"| **Origem na visao** | {cap['origem'] or '-'} |",
        f"| **Etapa TSG (flow-state)** | `{stage}` → Status *{status}* |",
        f"| **Ordem no MVP** | {flow_cap.get('mvp_order') or '-'} |",
        "",
        "### Objetivo",
        cap["objetivo"],
        "",
        "### Valor de negocio",
        cap["valor"],
        "",
        "### Resumo do fluxo",
        cap["fluxo"],
        "",
        "### Dependencias",
        cap["dependencias"] or "—",
    ]
    if cap.get("escopo_minimo"):
        lines += ["", "### Escopo minimo no MVP", cap["escopo_minimo"]]

    lines += ["", "### Artefatos"]
    arts = flow_cap.get("artifacts") or {}
    if arts:
        for kind, path in arts.items():
            lines.append(f"- **{kind}**: [`{path}`]({BLOB_BASE}/{path})")
    else:
        lines.append("- nenhum ainda (etapa `backlog`)")

    lines += ["", "### Backlog", f"- [`backlog/capabilities.md`]({BLOB_BASE}/backlog/capabilities.md) — secao `{cap['id']}`"]
    if tasks:
        first = tasks[0]
        lines.append(
            f"- Plano de implementacao: [`{first['dir']}/tasks.md`]({BLOB_BASE}/{first['dir']}/tasks.md)"
            f" ({len(tasks)} tasks, viram sub-issues deste item)"
        )
    return "\n".join(lines) + "\n"


def task_body(cap: dict, task: dict) -> str:
    base = f"{BLOB_BASE}/{task['dir']}"
    review = f"{task['num'].split('.')[0]}.0_task_review.md"
    lines = [
        f"> Task **{task['num']}** do plano de {cap['id']} — *{cap['title']}*. "
        "Cadastro automatico a partir do `tasks.md` do PRD.",
        "",
        f"- **Comportamento observavel:** {task['behavior']}",
        f"- **Gate:** {task['gate']}",
        f"- **Bloqueado por:** {task['blocked']}",
        f"- **Arquivo:** [`{task['dir']}/{task['file']}`]({base}/{task['file']})",
        f"- **Status do plano:** {task['plan_status']}",
    ]
    if (ROOT / task["dir"] / review).exists():
        lines.append(f"- **Revisao:** [`{task['dir']}/{review}`]({base}/{review})")
    lines += [
        "",
        "O checkbox de verdade e o do `tasks.md` no repo; este issue espelha o checklist.",
    ]
    return "\n".join(lines) + "\n"


# ----------------------------------------------------------------------------- main
def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="mostra o plano sem escrever")
    parser.add_argument("--only", action="append", metavar="CAP-XXX",
                        help="processa so estas capacidades (repetivel)")
    args = parser.parse_args()

    caps = load_capabilities()
    flow = load_flow_state()
    flow_by_id = {c["id"]: c for c in flow["capabilities"]}
    tasks_by_cap = load_tasks(flow)

    unknown_stage = [
        c["id"] for c in caps
        if c["id"] in flow_by_id
        and flow_by_id[c["id"]].get("stage") not in STAGE_TO_STATUS
    ]
    missing = [c["id"] for c in caps if c["id"] not in flow_by_id]
    if unknown_stage or missing:
        print(f"ERRO: stages invalidos {unknown_stage} / caps fora do flow-state {missing}",
              file=sys.stderr)
        return 1

    if args.only:
        caps = [c for c in caps if c["id"] in args.only]
        if not caps:
            print("ERRO: --only nao casou nenhuma capacidade", file=sys.stderr)
            return 1

    print(f"== {len(caps)} capacidades, {sum(len(tasks_by_cap.get(c['id'], [])) for c in caps)} tasks"
          f"{' (dry-run)' if args.dry_run else ''}")

    gh = Gh(dry_run=args.dry_run)
    milestones = ensure_milestones(gh)
    label_ids = ensure_labels(gh, caps)
    known_issues = fetch_issues(gh)
    state = project_state(gh)

    def lid(*names: str) -> list[str]:
        return [label_ids[n] for n in names if n in label_ids]

    stats = {"caps": 0, "tasks": 0, "items": 0, "fields": 0}

    def place(issue: dict, status: str, priority: str) -> None:
        item = ensure_project_item(gh, state, issue)
        if not item:
            return
        stats["items"] += 1
        for field, option in (("Status", status), ("Priority", priority)):
            before = item["values"].get(field)
            set_field(gh, state, item, field, option, issue.get("number"))
            if item["values"].get(field) != before:
                stats["fields"] += 1

    for cap in caps:
        flow_cap = flow_by_id[cap["id"]]
        tasks = tasks_by_cap.get(cap["id"], [])
        phase = cap["fase_key"]
        if not phase:
            raise RuntimeError(f"{cap['id']}: fase nao mapeada para milestone -> {cap['fase']!r}")
        milestone = milestones[MILESTONES[phase]["title"]]
        priority = PRIORITY_TO_FIELD.get(cap["prioridade"], "P1")

        cap_issue = ensure_issue(
            gh, f"[{cap['id']}] {cap['title']}", cap_body(cap, flow_cap, tasks),
            milestone["id"], lid("cap", f"dominio:{cap['domain_slug']}"), known_issues,
        )
        stats["caps"] += 1
        place(cap_issue, STAGE_TO_STATUS[flow_cap.get("stage", "backlog")], priority)

        for task in tasks:
            task_issue = ensure_issue(
                gh,
                f"[{cap['id']}/{task['num']}] {task['title']}",
                task_body(cap, task),
                milestone["id"],
                lid("task", f"dominio:{cap['domain_slug']}"),
                known_issues,
                parent_id=cap_issue.get("id"),
            )
            stats["tasks"] += 1
            ensure_sub_issue(gh, cap_issue, task_issue, known_issues)
            place(task_issue, "Done" if task["done"] else "Ready", priority)

    print(
        f"ok: {stats['caps']} CAPs · {stats['tasks']} tasks · "
        f"{stats['items']} itens no project · {stats['fields']} campos atualizados"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
