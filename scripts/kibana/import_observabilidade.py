#!/usr/bin/env python3
"""Validate, export, or import the versioned media observability Kibana objects."""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import sys
import uuid
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SAVED_OBJECTS_PATH = Path(__file__).resolve().parent / "observabilidade-midia.ndjson"
ENV_PATH = REPOSITORY_ROOT / ".env"

DATA_VIEW_ID = "observabilidade-midia-metrics-generic"
DATA_VIEW_TITLE = "metrics-generic*"
DASHBOARD_ID = "pipeline-de-midia-v1"
DASHBOARD_TITLE = "Pipeline de Mídia"
TIME_FIELD = "@timestamp"

# The instrument names are the TechSpec contract. Elastic's OTel mapping stores
# each numeric point at metrics.<instrument name> in metrics-generic*.
INSTRUMENT_FIELDS = {
    "media.videos.count": "metrics.media.videos.count",
    "media.videos.stuck": "metrics.media.videos.stuck",
    "media.storage.used": "metrics.media.storage.used",
}

PANEL_TITLES = {
    "videos-by-state": "Vídeos por estado",
    "videos-stuck": "Vídeos presos",
    "storage-used": "Armazenamento usado (bytes)",
    "snapshot-staleness": "Idade do último snapshot",
}
PANEL_VISUALIZATIONS = {
    "videos-by-state": "lnsDatatable",
    "videos-stuck": "lnsMetric",
    "storage-used": "lnsMetric",
    "snapshot-staleness": "lnsMetric",
}
STALENESS_PANEL_TITLE = PANEL_TITLES["snapshot-staleness"]
REFRESH_INTERVAL_MS = 60_000


def stable_id(name: str) -> str:
    """Return the fixed child ID used by the dashboard definition."""
    return str(uuid.uuid5(uuid.NAMESPACE_URL, f"code-for-coders:{DASHBOARD_ID}:panel/{name}"))


def load_saved_objects(path: Path) -> tuple[list[dict], dict]:
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except OSError as error:
        raise ValueError(f"não foi possível ler {path}: {error}") from error

    if not lines:
        raise ValueError("o arquivo NDJSON está vazio")

    records = []
    for number, line in enumerate(lines, start=1):
        try:
            records.append(json.loads(line))
        except json.JSONDecodeError as error:
            raise ValueError(f"NDJSON inválido na linha {number}: {error.msg}") from error

    summary = records[-1]
    saved_objects = records[:-1]
    if "type" in summary or "id" in summary:
        raise ValueError("falta o resumo de exportação ao final do NDJSON")
    if summary.get("exportedCount") != len(saved_objects):
        raise ValueError("o resumo NDJSON não corresponde à quantidade de saved objects")
    return saved_objects, summary


def nested_strings(value) -> str:
    if isinstance(value, dict):
        return "\n".join(nested_strings(item) for item in value.values())
    if isinstance(value, list):
        return "\n".join(nested_strings(item) for item in value)
    if isinstance(value, str):
        try:
            decoded = json.loads(value)
        except json.JSONDecodeError:
            return value
        if isinstance(decoded, (dict, list)):
            return value + "\n" + nested_strings(decoded)
        return value
    return ""


def verify_saved_objects(path: Path = SAVED_OBJECTS_PATH) -> tuple[list[dict], dict]:
    saved_objects, summary = load_saved_objects(path)
    by_type_and_id = {}
    for saved_object in saved_objects:
        object_type = saved_object.get("type")
        object_id = saved_object.get("id")
        if not object_type or not object_id:
            raise ValueError("cada saved object precisa declarar type e id")
        key = (object_type, object_id)
        if key in by_type_and_id:
            raise ValueError(f"ID duplicado em saved objects: {object_type}/{object_id}")
        by_type_and_id[key] = saved_object

    expected_objects = {
        ("index-pattern", DATA_VIEW_ID),
        ("dashboard", DASHBOARD_ID),
    }
    if set(by_type_and_id) != expected_objects:
        raise ValueError("o NDJSON deve conter somente o data view e o dashboard com IDs estáveis")

    data_view = by_type_and_id[("index-pattern", DATA_VIEW_ID)]
    data_view_attributes = data_view.get("attributes", {})
    if data_view_attributes.get("title") != DATA_VIEW_TITLE:
        raise ValueError(f"o data view precisa usar o padrão {DATA_VIEW_TITLE}")
    if data_view_attributes.get("timeFieldName") != TIME_FIELD:
        raise ValueError(f"o data view precisa usar o campo temporal {TIME_FIELD}")

    dashboard = by_type_and_id[("dashboard", DASHBOARD_ID)]
    attributes = dashboard.get("attributes", {})
    if attributes.get("title") != DASHBOARD_TITLE:
        raise ValueError(f"o título do dashboard deve ser {DASHBOARD_TITLE!r}")
    if attributes.get("timeRestore") is not True:
        raise ValueError("o dashboard deve restaurar o período de consulta")
    if attributes.get("timeFrom") != "now-30d" or attributes.get("timeTo") != "now":
        raise ValueError("o dashboard deve consultar a janela de retenção de 30 dias")
    if attributes.get("refreshInterval") != {"pause": False, "value": REFRESH_INTERVAL_MS}:
        raise ValueError("o dashboard deve atualizar os painéis a cada 60 segundos")

    try:
        panels = json.loads(attributes.get("panelsJSON", "[]"))
    except json.JSONDecodeError as error:
        raise ValueError(f"panelsJSON inválido: {error.msg}") from error
    if not isinstance(panels, list) or not panels:
        raise ValueError("o dashboard precisa ter painéis Lens")

    expected_panel_ids = {stable_id(key) for key in PANEL_TITLES}
    actual_panel_ids = [panel.get("panelIndex") for panel in panels]
    if len(actual_panel_ids) != len(set(actual_panel_ids)):
        raise ValueError("IDs duplicados nos painéis")
    if set(actual_panel_ids) != expected_panel_ids:
        raise ValueError("os painéis precisam manter seus IDs estáveis")

    panels_by_id = {panel["panelIndex"]: panel for panel in panels}
    query_text = nested_strings(panels)
    for instrument_name, field_name in INSTRUMENT_FIELDS.items():
        if instrument_name not in field_name or field_name not in query_text:
            raise ValueError(f"métrica do manifesto não referenciada: {instrument_name}")

    for key, title in PANEL_TITLES.items():
        panel = panels_by_id[stable_id(key)]
        if panel.get("type") != "vis":
            raise ValueError(f"painel {title!r} não usa a visualização Lens exportada pelo Kibana")
        embeddable = panel.get("embeddableConfig", {})
        if embeddable.get("title") != title:
            raise ValueError(f"título do painel ausente ou divergente: {title}")
        lens_attributes = embeddable.get("attributes", {})
        if lens_attributes.get("visualizationType") != PANEL_VISUALIZATIONS[key]:
            raise ValueError(f"tipo de visualização incorreto no painel {title!r}")

        state = lens_attributes.get("state", {})
        datasource = state.get("datasourceStates", {}).get("textBased", {})
        layers = datasource.get("layers", {})
        if len(layers) != 1:
            raise ValueError(f"painel {title!r} precisa declarar exatamente uma camada ES|QL")
        layer_id, layer = next(iter(layers.items()))
        query = layer.get("query", {}).get("esql", "")
        if not query.startswith("FROM metrics-generic*"):
            raise ValueError(f"painel {title!r} não consulta metrics-generic*")

        data_view_id = layer.get("index")
        ad_hoc_view = state.get("adHocDataViews", {}).get(data_view_id, {})
        if ad_hoc_view.get("title") != DATA_VIEW_TITLE:
            raise ValueError(f"painel {title!r} não usa o data view {DATA_VIEW_TITLE}")
        internal_references = state.get("internalReferences", [])
        if not any(
            reference.get("type") == "index-pattern"
            and reference.get("id") == data_view_id
            and reference.get("name") == f"indexpattern-datasource-layer-{layer_id}"
            for reference in internal_references
        ):
            raise ValueError(f"referência ES|QL do data view ausente no painel {title!r}")

    status_panel = panels_by_id[stable_id("videos-by-state")]
    status_state = status_panel["embeddableConfig"]["attributes"]["state"]
    status_query = next(iter(status_state["datasourceStates"]["textBased"]["layers"].values()))[
        "query"
    ]["esql"]
    if "attributes.status" not in status_query or "CASE(" in status_query:
        raise ValueError("o painel de estado deve preservar os valores canônicos da dimensão status")

    staleness_panel = panels_by_id[stable_id("snapshot-staleness")]
    staleness_query = next(
        iter(
            staleness_panel["embeddableConfig"]["attributes"]["state"]["datasourceStates"]
            ["textBased"]["layers"].values()
        )
    )["query"]["esql"]
    if (
        "MAX(@timestamp)" not in staleness_query
        or "DATE_DIFF(\"seconds\"" not in staleness_query
        or "NOW()" not in staleness_query
        or "age_seconds" not in staleness_query
    ):
        raise ValueError("painel de staleness precisa mostrar a idade crescente do último snapshot")

    if summary.get("exportedCount") != len(saved_objects):
        raise ValueError("exportedCount não corresponde à quantidade de saved objects")
    return saved_objects, summary


def dotenv_values(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except FileNotFoundError:
        return values
    except OSError as error:
        raise ValueError(f"não foi possível ler .env: {error}") from error

    for line in lines:
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        if stripped.startswith("export "):
            stripped = stripped[len("export ") :]
        key, separator, value = stripped.partition("=")
        if not separator:
            continue
        key = key.strip()
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
            value = value[1:-1]
        values[key] = value
    return values


def setting(name: str, values: dict[str, str], default: str = "") -> str:
    return os.environ.get(name) or values.get(name) or default


def kibana_connection() -> tuple[str, str, str, str]:
    dotenv = dotenv_values(ENV_PATH)
    username = setting("ELASTIC_USERNAME", dotenv)
    password = setting("ELASTIC_PASSWORD", dotenv)
    kibana_url = setting("KIBANA_URL", dotenv, "https://kibana.tasso.dev.br").rstrip("/")
    if not username or not password:
        raise ValueError("ELASTIC_USERNAME e ELASTIC_PASSWORD são necessários no ambiente ou .env")
    if not re.fullmatch(r"https?://[^\s]+", kibana_url):
        raise ValueError("KIBANA_URL precisa ser uma URL HTTP ou HTTPS válida")
    authorization = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("ascii")
    return username, password, kibana_url, authorization


def safe_http_error(error: HTTPError, username: str, password: str) -> str:
    body = error.read().decode("utf-8", errors="replace").strip()
    for secret in (username, password):
        if secret:
            body = body.replace(secret, "[redigido]")
    if len(body) > 1000:
        body = body[:1000] + "…"
    detail = f": {body}" if body else ""
    return f"HTTP {error.code}{detail}"


def export_saved_objects(path: Path = SAVED_OBJECTS_PATH) -> None:
    username, password, kibana_url, authorization = kibana_connection()
    payload = json.dumps(
        {
            "objects": [
                {"type": "index-pattern", "id": DATA_VIEW_ID},
                {"type": "dashboard", "id": DASHBOARD_ID},
            ],
            "includeReferencesDeep": True,
        },
        separators=(",", ":"),
    ).encode("utf-8")
    request = Request(
        f"{kibana_url}/api/saved_objects/_export",
        data=payload,
        method="POST",
        headers={
            "Authorization": f"Basic {authorization}",
            "Content-Type": "application/json",
            "kbn-xsrf": "true",
        },
    )
    try:
        with urlopen(request, timeout=30) as response:
            exported_ndjson = response.read()
    except HTTPError as error:
        raise ValueError(f"falha ao exportar saved objects: {safe_http_error(error, username, password)}") from error
    except URLError as error:
        raise ValueError(f"não foi possível acessar Kibana: {error.reason}") from error
    except TimeoutError as error:
        raise ValueError("tempo limite ao exportar saved objects do Kibana") from error

    temporary_path = path.with_suffix(path.suffix + ".tmp")
    try:
        temporary_path.write_bytes(exported_ndjson)
        saved_objects, _ = verify_saved_objects(temporary_path)
        if len(saved_objects) != 2:
            raise ValueError("o export do Kibana não retornou os dois saved objects esperados")
        os.replace(temporary_path, path)
    except (OSError, ValueError):
        temporary_path.unlink(missing_ok=True)
        raise


def import_saved_objects(path: Path = SAVED_OBJECTS_PATH) -> None:
    saved_objects, _ = verify_saved_objects(path)
    username, password, kibana_url, authorization = kibana_connection()
    boundary = "----codex-kibana-import-" + uuid.uuid4().hex
    file_content = path.read_bytes()
    body = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="observabilidade-midia.ndjson"\r\n'
        "Content-Type: application/ndjson\r\n\r\n"
    ).encode("utf-8") + file_content + f"\r\n--{boundary}--\r\n".encode("ascii")
    request = Request(
        f"{kibana_url}/api/saved_objects/_import?overwrite=true",
        data=body,
        method="POST",
        headers={
            "Authorization": f"Basic {authorization}",
            "Content-Type": f"multipart/form-data; boundary={boundary}",
            "kbn-xsrf": "true",
        },
    )

    try:
        with urlopen(request, timeout=30) as response:
            result = json.loads(response.read().decode("utf-8"))
    except HTTPError as error:
        raise ValueError(f"Kibana recusou a importação: {safe_http_error(error, username, password)}") from error
    except URLError as error:
        raise ValueError(f"não foi possível acessar Kibana: {error.reason}") from error
    except TimeoutError as error:
        raise ValueError("tempo limite ao importar saved objects no Kibana") from error
    except json.JSONDecodeError as error:
        raise ValueError("Kibana retornou uma resposta inválida à importação") from error

    errors = result.get("errors", [])
    imported_count = result.get("successCount")
    if not result.get("success") or errors or imported_count != len(saved_objects):
        error_types = ", ".join(
            str(item.get("error", {}).get("type", "erro de importação"))
            for item in errors
            if isinstance(item, dict)
        )
        detail = f": {error_types}" if error_types else ""
        raise ValueError(f"importação incompleta ({imported_count}/{len(saved_objects)} objetos){detail}")

    print(f"Importados {imported_count} saved objects com overwrite em {kibana_url}.")
    print(f"Dashboard: {DASHBOARD_TITLE} (ID {DASHBOARD_ID}).")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_mutually_exclusive_group()
    actions.add_argument(
        "--verify-only",
        action="store_true",
        help="valida localmente o NDJSON, os IDs, as métricas e o painel de staleness; não usa rede",
    )
    actions.add_argument(
        "--export",
        action="store_true",
        help="exporta os dois saved objects atuais do Kibana para o NDJSON versionado",
    )
    actions.add_argument(
        "--import",
        dest="do_import",
        action="store_true",
        help="importa os saved objects no Kibana configurado, sobrescrevendo IDs estáveis",
    )
    args = parser.parse_args()

    try:
        if args.export:
            export_saved_objects()
            print(f"Saved objects exportados para {SAVED_OBJECTS_PATH.relative_to(REPOSITORY_ROOT)}.")
            return 0

        if args.do_import:
            import_saved_objects()
            return 0

        saved_objects, _ = verify_saved_objects()
        print(
            "Verificação estrutural passou: data view metrics-generic* e dashboard "
            f"{DASHBOARD_TITLE!r} com IDs estáveis; métricas verificadas contra o manifesto "
            f"({', '.join(INSTRUMENT_FIELDS)}); painel de staleness presente. "
            f"Saved objects: {len(saved_objects)}."
        )
        return 0
    except (OSError, ValueError) as error:
        print(f"Erro: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
