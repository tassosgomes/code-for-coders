#!/usr/bin/env python3
"""Validate, export, or import the versioned media observability Kibana objects."""

from __future__ import annotations

import argparse
import base64
import copy
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
    "media.upload.created": "metrics.media.upload.created",
    "media.upload.completed": "metrics.media.upload.completed",
    "media.upload.size": "metrics.media.upload.size",
    "media.upload.expired": "metrics.media.upload.expired",
    "media.uploads.pending": "metrics.media.uploads.pending",
    "media.videos.oldest_waiting": "metrics.media.videos.oldest_waiting",
    "media.videos.claimed": "metrics.media.videos.claimed",
    "media.videos.retried": "metrics.media.videos.retried",
    "media.videos.wait": "metrics.media.videos.wait",
    "media.videos.completed": "metrics.media.videos.completed",
    "media.videos.failed": "metrics.media.videos.failed",
    "media.videos.prepare_duration": "metrics.media.videos.prepare_duration",
    "media.videos.time_to_ready": "metrics.media.videos.time_to_ready",
    "media.outbox.pending": "metrics.media.outbox.pending",
    "media.outbox.oldest_pending": "metrics.media.outbox.oldest_pending",
    "media.outbox.exhausted": "metrics.media.outbox.exhausted",
    "media.outbox.published": "metrics.media.outbox.published",
    "media.outbox.publish_failed": "metrics.media.outbox.publish_failed",
    "media.messaging.dlq.messages": "metrics.media.messaging.dlq.messages",
}

PANEL_TITLES = {
    "videos-by-state": "Fila · Profundidade por estado",
    "videos-stuck": "Fila · Vídeos presos",
    "storage-used": "Armazenamento usado (bytes)",
    "snapshot-staleness": "Idade do último snapshot",
    "upload-funnel": "Envio · Funil na janela",
    "upload-sizes": "Envio · Distribuição de tamanho (bytes)",
    "upload-pending": "Envio · Sessões pendentes",
    "queue-oldest-waiting": "Fila · Idade do vídeo mais antigo",
    "queue-claims": "Fila · Claims e retentativas",
    "queue-wait": "Fila · Tempo de espera (s)",
    "preparation-stage-duration": "Preparação · Duração por etapa (s)",
    "preparation-time-to-ready": "Preparação · Tempo até pronto (s)",
    "preparation-outcomes": "Preparação · Conclusões e tentativas na janela",
    "preparation-failures": "Preparação · Falhas por motivo na janela",
    "outbox-snapshot": "Outbox · Pendentes, idade e esgotados",
    "outbox-publishes": "Outbox · Publicações por evento na janela",
    "dlq-depth": "Outbox · Profundidade da DLQ",
}
PANEL_VISUALIZATIONS = {
    "videos-by-state": "lnsDatatable",
    "videos-stuck": "lnsMetric",
    "storage-used": "lnsMetric",
    "snapshot-staleness": "lnsMetric",
    "upload-funnel": "lnsDatatable",
    "upload-sizes": "lnsDatatable",
    "upload-pending": "lnsMetric",
    "queue-oldest-waiting": "lnsMetric",
    "queue-claims": "lnsDatatable",
    "queue-wait": "lnsDatatable",
    "preparation-stage-duration": "lnsDatatable",
    "preparation-time-to-ready": "lnsDatatable",
    "preparation-outcomes": "lnsDatatable",
    "preparation-failures": "lnsDatatable",
    "outbox-snapshot": "lnsDatatable",
    "outbox-publishes": "lnsDatatable",
    "dlq-depth": "lnsMetric",
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

    funnel_query = (
        panels_by_id[stable_id("upload-funnel")]["embeddableConfig"]["attributes"]["state"]["query"]["esql"]
    )
    funnel_instruments = ("media.upload.created", "media.upload.completed", "media.upload.expired")
    for instrument_name in funnel_instruments:
        if INSTRUMENT_FIELDS[instrument_name] not in funnel_query:
            raise ValueError(f"painel de funil não referencia {instrument_name}")
    if any(
        f"SUM({INSTRUMENT_FIELDS[instrument]})" not in funnel_query
        for instrument in funnel_instruments
    ):
        raise ValueError("painel de funil deve comparar os três counters no mesmo período selecionado")

    sizes_query = (
        panels_by_id[stable_id("upload-sizes")]["embeddableConfig"]["attributes"]["state"]["query"]["esql"]
    )
    if INSTRUMENT_FIELDS["media.upload.size"] not in sizes_query or "PERCENTILE(" not in sizes_query:
        raise ValueError("painel de tamanho deve calcular percentis do histograma media.upload.size")

    pending_query = (
        panels_by_id[stable_id("upload-pending")]["embeddableConfig"]["attributes"]["state"]["query"]["esql"]
    )
    if INSTRUMENT_FIELDS["media.uploads.pending"] not in pending_query or "LATEST(" not in pending_query:
        raise ValueError("painel de pendências deve exibir o snapshot mais recente de media.uploads.pending")

    oldest_waiting_query = (
        panels_by_id[stable_id("queue-oldest-waiting")]["embeddableConfig"]["attributes"]["state"]["query"]["esql"]
    )
    if INSTRUMENT_FIELDS["media.videos.oldest_waiting"] not in oldest_waiting_query or "LATEST(" not in oldest_waiting_query:
        raise ValueError("painel da fila deve exibir o snapshot mais recente de media.videos.oldest_waiting")

    claims_query = (
        panels_by_id[stable_id("queue-claims")]["embeddableConfig"]["attributes"]["state"]["query"]["esql"]
    )
    claim_instruments = ("media.videos.claimed", "media.videos.retried")
    if any(INSTRUMENT_FIELDS[instrument] not in claims_query for instrument in claim_instruments):
        raise ValueError("painel da fila deve comparar media.videos.claimed e media.videos.retried")
    if any(f"SUM({INSTRUMENT_FIELDS[instrument]})" not in claims_query for instrument in claim_instruments):
        raise ValueError("painel da fila deve somar os counters de claim e retentativa")

    wait_query = (
        panels_by_id[stable_id("queue-wait")]["embeddableConfig"]["attributes"]["state"]["query"]["esql"]
    )
    if INSTRUMENT_FIELDS["media.videos.wait"] not in wait_query or "PERCENTILE(" not in wait_query:
        raise ValueError("painel da fila deve calcular percentis de media.videos.wait")

    preparation_duration_query = panel_query(panels_by_id[stable_id("preparation-stage-duration")])
    if (
        INSTRUMENT_FIELDS["media.videos.prepare_duration"] not in preparation_duration_query
        or "PERCENTILE(" not in preparation_duration_query
        or "BY stage = attributes.stage" not in preparation_duration_query
    ):
        raise ValueError("painel de preparação deve calcular percentis por stage de media.videos.prepare_duration")

    time_to_ready_query = panel_query(panels_by_id[stable_id("preparation-time-to-ready")])
    if (
        INSTRUMENT_FIELDS["media.videos.time_to_ready"] not in time_to_ready_query
        or "PERCENTILE(" not in time_to_ready_query
    ):
        raise ValueError("painel de preparação deve calcular percentis de media.videos.time_to_ready")

    outcomes_query = panel_query(panels_by_id[stable_id("preparation-outcomes")])
    outcome_instruments = ("media.videos.completed", "media.videos.retried")
    if any(INSTRUMENT_FIELDS[instrument] not in outcomes_query for instrument in outcome_instruments):
        raise ValueError("painel de preparação deve comparar conclusões e retentativas na janela")
    if any(f"SUM({INSTRUMENT_FIELDS[instrument]})" not in outcomes_query for instrument in outcome_instruments):
        raise ValueError("painel de preparação deve somar conclusões e retentativas")

    failures_query = panel_query(panels_by_id[stable_id("preparation-failures")])
    if (
        INSTRUMENT_FIELDS["media.videos.failed"] not in failures_query
        or "SUM(" + INSTRUMENT_FIELDS["media.videos.failed"] + ")" not in failures_query
        or "BY reason = attributes.reason" not in failures_query
    ):
        raise ValueError("painel de preparação deve somar media.videos.failed por reason")

    outbox_snapshot_query = panel_query(panels_by_id[stable_id("outbox-snapshot")])
    outbox_gauges = ("media.outbox.pending", "media.outbox.oldest_pending", "media.outbox.exhausted")
    if any(INSTRUMENT_FIELDS[instrument] not in outbox_snapshot_query for instrument in outbox_gauges):
        raise ValueError("painel de outbox deve exibir o snapshot de pendentes, idade e esgotados")
    if any(
        f"LATEST({INSTRUMENT_FIELDS[instrument]})" not in outbox_snapshot_query
        for instrument in outbox_gauges
    ):
        raise ValueError("painel de outbox deve exibir o snapshot mais recente dos gauges de outbox")

    outbox_publishes_query = panel_query(panels_by_id[stable_id("outbox-publishes")])
    outbox_counters = ("media.outbox.published", "media.outbox.publish_failed")
    if any(INSTRUMENT_FIELDS[instrument] not in outbox_publishes_query for instrument in outbox_counters):
        raise ValueError("painel de outbox deve comparar publicações e falhas por evento na janela")
    if any(
        f"SUM({INSTRUMENT_FIELDS[instrument]})" not in outbox_publishes_query
        for instrument in outbox_counters
    ):
        raise ValueError("painel de outbox deve somar os counters de publicação e falha")
    if "BY event = attributes.event" not in outbox_publishes_query:
        raise ValueError("painel de outbox deve detalhar publicações por event")

    dlq_query = panel_query(panels_by_id[stable_id("dlq-depth")])
    if (
        INSTRUMENT_FIELDS["media.messaging.dlq.messages"] not in dlq_query
        or "LATEST(" not in dlq_query
    ):
        raise ValueError("painel de outbox deve exibir o snapshot mais recente de media.messaging.dlq.messages")

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


def panel_query(panel: dict) -> str:
    state = panel["embeddableConfig"]["attributes"]["state"]
    return state["query"]["esql"]


def make_lens_panel(
    panel_id: str,
    title: str,
    query: str,
    columns: list[tuple[str, str]],
    data_view_id: str,
    data_view: dict,
    x: int,
    y: int,
    width: int,
    height: int,
) -> dict:
    datasource_columns = []
    visualization_columns = []
    metric_index = 0
    row_index = 0
    for field_name, field_type in columns:
        if field_type == "string":
            column_id = f"datatable_accessor_row_{row_index}"
            row_index += 1
            datasource_columns.append({"columnId": column_id, "fieldName": field_name, "meta": {"type": "string"}})
            visualization_columns.append({"columnId": column_id, "isTransposed": False, "isMetric": False})
        else:
            column_id = f"datatable_accessor_metric_{metric_index}"
            metric_index += 1
            datasource_columns.append({
                "columnId": column_id,
                "fieldName": field_name,
                "meta": {"type": "number"},
                "inMetricDimension": True,
            })
            visualization_columns.append({"columnId": column_id, "isTransposed": False, "isMetric": True})

    title_text = title
    internal_reference_name = "indexpattern-datasource-layer-layer_0"
    layer = {
        "index": data_view_id,
        "query": {"esql": query},
        "columns": datasource_columns,
        "ignoreGlobalFilters": False,
    }
    state = {
        "datasourceStates": {"textBased": {"layers": {"layer_0": layer}}},
        "internalReferences": [{"type": "index-pattern", "id": data_view_id, "name": internal_reference_name}],
        "visualization": {"layerId": "layer_0", "layerType": "data", "columns": visualization_columns},
        "adHocDataViews": {data_view_id: copy.deepcopy(data_view)},
        "query": {"esql": query},
        "filters": [],
    }
    return {
        "type": "vis",
        "embeddableConfig": {
            "title": title_text,
            "attributes": {
                "visualizationType": "lnsDatatable",
                "title": "",
                "references": [],
                "version": 2,
                "state": state,
            },
        },
        "panelIndex": panel_id,
        "gridData": {"x": x, "y": y, "w": width, "h": height, "i": panel_id},
    }


def make_lens_metric_panel(
    panel_id: str,
    title: str,
    query: str,
    metric_field: str,
    data_view_id: str,
    data_view: dict,
    x: int,
    y: int,
    width: int,
    height: int,
) -> dict:
    """Build a single-value Lens metric panel following the Kibana-exported shape."""
    internal_reference_name = "indexpattern-datasource-layer-layer_0"
    layer = {
        "index": data_view_id,
        "query": {"esql": query},
        "columns": [{"columnId": "metric_accessor_metric", "fieldName": metric_field, "meta": {"type": "number"}}],
        "ignoreGlobalFilters": False,
    }
    state = {
        "datasourceStates": {"textBased": {"layers": {"layer_0": layer}}},
        "internalReferences": [{"type": "index-pattern", "id": data_view_id, "name": internal_reference_name}],
        "visualization": {
            "layerId": "layer_0",
            "layerType": "data",
            "metricAccessor": "metric_accessor_metric",
            "showBar": False,
            "density": "default",
        },
        "adHocDataViews": {data_view_id: copy.deepcopy(data_view)},
        "query": {"esql": query},
        "filters": [],
    }
    return {
        "type": "vis",
        "embeddableConfig": {
            "title": title,
            "attributes": {
                "visualizationType": "lnsMetric",
                "title": "",
                "references": [],
                "version": 2,
                "state": state,
            },
        },
        "panelIndex": panel_id,
        "gridData": {"x": x, "y": y, "w": width, "h": height, "i": panel_id},
    }


def generate_saved_objects(path: Path = SAVED_OBJECTS_PATH) -> int:
    saved_objects, summary = load_saved_objects(path)
    dashboard = next(item for item in saved_objects if item["type"] == "dashboard" and item["id"] == DASHBOARD_ID)
    attributes = dashboard["attributes"]
    panels = json.loads(attributes["panelsJSON"])
    existing_panels = {panel["panelIndex"]: panel for panel in panels}
    queue_panel = existing_panels[stable_id("queue-wait")]
    queue_state = queue_panel["embeddableConfig"]["attributes"]["state"]
    queue_layer = queue_state["datasourceStates"]["textBased"]["layers"]["layer_0"]
    data_view_id = queue_layer["index"]
    data_view = queue_state["adHocDataViews"][data_view_id]

    panel_definitions = [
        (
            "preparation-stage-duration",
            "FROM metrics-generic* | WHERE metrics.media.videos.prepare_duration IS NOT NULL | STATS p50_seconds = PERCENTILE(metrics.media.videos.prepare_duration, 50), p95_seconds = PERCENTILE(metrics.media.videos.prepare_duration, 95) BY stage = attributes.stage | SORT stage ASC",
            [("stage", "string"), ("p50_seconds", "number"), ("p95_seconds", "number")],
            0,
            54,
        ),
        (
            "preparation-time-to-ready",
            "FROM metrics-generic* | WHERE metrics.media.videos.time_to_ready IS NOT NULL | STATS p50_seconds = PERCENTILE(metrics.media.videos.time_to_ready, 50), p95_seconds = PERCENTILE(metrics.media.videos.time_to_ready, 95)",
            [("p50_seconds", "number"), ("p95_seconds", "number")],
            24,
            54,
        ),
        (
            "preparation-outcomes",
            "FROM metrics-generic* | WHERE metrics.media.videos.completed IS NOT NULL OR metrics.media.videos.retried IS NOT NULL | STATS completed = SUM(metrics.media.videos.completed), retried = SUM(metrics.media.videos.retried)",
            [("completed", "number"), ("retried", "number")],
            0,
            66,
        ),
        (
            "preparation-failures",
            "FROM metrics-generic* | WHERE metrics.media.videos.failed IS NOT NULL | STATS failed = SUM(metrics.media.videos.failed) BY reason = attributes.reason | SORT reason ASC",
            [("reason", "string"), ("failed", "number")],
            24,
            66,
        ),
        (
            "outbox-snapshot",
            "FROM metrics-generic* | WHERE metrics.media.outbox.pending IS NOT NULL OR metrics.media.outbox.oldest_pending IS NOT NULL OR metrics.media.outbox.exhausted IS NOT NULL | STATS pending = LATEST(metrics.media.outbox.pending), oldest_pending_seconds = LATEST(metrics.media.outbox.oldest_pending), exhausted = LATEST(metrics.media.outbox.exhausted)",
            [("pending", "number"), ("oldest_pending_seconds", "number"), ("exhausted", "number")],
            0,
            78,
        ),
        (
            "outbox-publishes",
            "FROM metrics-generic* | WHERE metrics.media.outbox.published IS NOT NULL OR metrics.media.outbox.publish_failed IS NOT NULL | STATS published = SUM(metrics.media.outbox.published), publish_failed = SUM(metrics.media.outbox.publish_failed) BY event = attributes.event | SORT event ASC",
            [("event", "string"), ("published", "number"), ("publish_failed", "number")],
            24,
            78,
        ),
    ]
    for key, query, columns, x, y in panel_definitions:
        panel_id = stable_id(key)
        existing_panels[panel_id] = make_lens_panel(
            panel_id,
            PANEL_TITLES[key],
            query,
            columns,
            data_view_id,
            data_view,
            x,
            y,
            24,
            12,
        )

    dlq_panel_id = stable_id("dlq-depth")
    existing_panels[dlq_panel_id] = make_lens_metric_panel(
        dlq_panel_id,
        PANEL_TITLES["dlq-depth"],
        "FROM metrics-generic* | WHERE metrics.media.messaging.dlq.messages IS NOT NULL | STATS dlq_messages = LATEST(metrics.media.messaging.dlq.messages)",
        "dlq_messages",
        data_view_id,
        data_view,
        0,
        90,
        24,
        10,
    )

    attributes["panelsJSON"] = json.dumps(list(existing_panels.values()), ensure_ascii=False, separators=(",", ":"))
    summary["exportedCount"] = len(saved_objects)
    content = "\n".join(json.dumps(item, ensure_ascii=False, separators=(",", ":")) for item in [*saved_objects, summary]) + "\n"
    temporary_path = path.with_suffix(path.suffix + ".tmp")
    try:
        temporary_path.write_text(content, encoding="utf-8")
        verify_saved_objects(temporary_path)
        os.replace(temporary_path, path)
    except (OSError, ValueError):
        temporary_path.unlink(missing_ok=True)
        raise
    return len(existing_panels)


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
        help="valida o NDJSON, os IDs, as métricas e os painéis de envio, fila, preparação e outbox/DLQ; não usa rede",
    )
    actions.add_argument(
        "--generate-only",
        action="store_true",
        help="gera os painéis de preparação e outbox/DLQ no NDJSON versionado e valida o resultado; não usa rede",
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
        if args.generate_only:
            panel_count = generate_saved_objects()
            print(f"NDJSON gerado e validado com {panel_count} painéis em {SAVED_OBJECTS_PATH.relative_to(REPOSITORY_ROOT)}.")
            return 0

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
            f"({', '.join(INSTRUMENT_FIELDS)}); painéis de envio, fila, preparação e outbox/DLQ presentes. "
            f"Saved objects: {len(saved_objects)}."
        )
        return 0
    except (OSError, ValueError) as error:
        print(f"Erro: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
