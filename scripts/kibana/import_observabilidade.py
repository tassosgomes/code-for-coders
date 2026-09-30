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

# Counters OTLP são cumulativos. Cada painel busca a última amostra anterior à
# janela e a última dentro dela por série; a soma só ocorre após o cálculo do
# incremento. Os painéis de counter ignoram o filtro temporal automático do
# Lens para que o baseline permaneça acessível, mas usam os limites do seletor
# do dashboard (?_tstart/?_tend) na própria query.
COUNTER_PANEL_FIELDS = {
    "upload-funnel": (
        "media.upload.created",
        "media.upload.completed",
        "media.upload.expired",
    ),
    "queue-claims": ("media.videos.claimed", "media.videos.retried"),
    "preparation-outcomes": ("media.videos.completed", "media.videos.retried"),
    "preparation-failures": ("media.videos.failed",),
    "outbox-publishes": ("media.outbox.published", "media.outbox.publish_failed"),
}
COUNTER_PANEL_DIMENSION = {
    "upload-funnel": None,
    "queue-claims": None,
    "preparation-outcomes": None,
    "preparation-failures": "reason",
    "outbox-publishes": "event",
}
CUMULATIVE_COUNTER_FIELDS = tuple(
    dict.fromkeys(field for fields in COUNTER_PANEL_FIELDS.values() for field in fields)
)


def _counter_alias(instrument_name: str) -> str:
    """Return the output column name for a counter (last path segment)."""
    return instrument_name.rsplit(".", 1)[-1]


def counter_increment_query(panel_key: str) -> str:
    """Return the canonical ES|QL for a cumulative-counter panel.

    Computes final-in-window minus the last pre-window sample for each resource
    and metric attribute set. Service instance distinguishes api/worker and
    replicas even when they share service.name. Missing baseline means a new
    series (start at zero); missing window point means zero increment.
    """
    fields = COUNTER_PANEL_FIELDS[panel_key]
    dimension = COUNTER_PANEL_DIMENSION[panel_key]
    where = " OR ".join(f"{INSTRUMENT_FIELDS[name]} IS NOT NULL" for name in fields)
    aggregations = ", ".join(
        f"{_counter_alias(name)}_base = LAST({INSTRUMENT_FIELDS[name]}, @timestamp) "
        f"WHERE in_window == 0, "
        f"{_counter_alias(name)}_last = LAST({INSTRUMENT_FIELDS[name]}, @timestamp) "
        f"WHERE in_window == 1"
        for name in fields
    )
    series = [
        "service = resource.attributes.service.name",
        "instance = resource.attributes.service.instance.id",
    ]
    if dimension is not None:
        series.append(f"{dimension} = attributes.{dimension}")
    increments = ", ".join(
        f"{_counter_alias(name)}_inc = CASE("
        f"{_counter_alias(name)}_end >= {_counter_alias(name)}_start, "
        f"{_counter_alias(name)}_end - {_counter_alias(name)}_start, "
        f"{_counter_alias(name)}_end)"
        for name in fields
    )
    bounds = ", ".join(
        f"{_counter_alias(name)}_start = COALESCE({_counter_alias(name)}_base, 0), "
        f"{_counter_alias(name)}_end = COALESCE({_counter_alias(name)}_last, "
        f"COALESCE({_counter_alias(name)}_base, 0))"
        for name in fields
    )
    totals = ", ".join(
        f"{_counter_alias(name)} = SUM({_counter_alias(name)}_inc)" for name in fields
    )
    query = (
        "FROM metrics-generic* | WHERE @timestamp <= ?_tend "
        f"AND ({where}) | EVAL in_window = CASE(@timestamp >= ?_tstart, 1, 0) "
        f"| STATS {aggregations} BY {', '.join(series)} "
        f"| EVAL {bounds} | EVAL {increments} | STATS {totals}"
    )
    if dimension is not None:
        query += f" BY {dimension} | SORT {dimension} ASC"
    return query


def verify_counter_increment_shape(query: str, panel_key: str, title: str) -> None:
    """Require baseline and end per resource series before summing increments."""
    fields = COUNTER_PANEL_FIELDS[panel_key]
    dimension = COUNTER_PANEL_DIMENSION[panel_key]
    required = (
        "@timestamp <= ?_tend",
        "in_window = CASE(@timestamp >= ?_tstart, 1, 0)",
        "BY service = resource.attributes.service.name, instance = resource.attributes.service.instance.id",
    )
    if any(snippet not in query for snippet in required):
        raise ValueError(f"painel {title!r} precisa preservar baseline e separar recursos/produtores")
    for name in fields:
        field = INSTRUMENT_FIELDS[name]
        if f"SUM({field})" in query:
            raise ValueError(
                f"painel {title!r} não pode usar SUM({field}): counters cumulativos "
                "repetem o total acumulado em cada snapshot; calcule incrementos na janela"
            )
        for aggregation in (
            f"LAST({field}, @timestamp) WHERE in_window == 0",
            f"LAST({field}, @timestamp) WHERE in_window == 1",
        ):
            if aggregation not in query:
                raise ValueError(
                    f"painel {title!r} precisa calcular baseline e fim na janela com {aggregation}"
                )
        alias = _counter_alias(name)
        bounds = (
            f"{alias}_start = COALESCE({alias}_base, 0)",
            f"{alias}_end = COALESCE({alias}_last, COALESCE({alias}_base, 0))",
        )
        if any(bound not in query for bound in bounds):
            raise ValueError(f"painel {title!r} precisa usar baseline como início de {alias}")
        expected = (
            f"CASE({alias}_end >= {alias}_start, {alias}_end - {alias}_start, {alias}_end)"
        )
        if expected not in query:
            raise ValueError(
                f"painel {title!r} precisa tratar reset de contador em {alias} "
                "(vale o final quando final < início)"
            )
        if f"{alias} = SUM({alias}_inc)" not in query:
            raise ValueError(f"painel {title!r} precisa somar incrementos de {alias} entre séries")
    if dimension is not None and (
        f"{dimension} = attributes.{dimension}" not in query or f"BY {dimension} | SORT" not in query
    ):
        raise ValueError(f"painel {title!r} precisa detalhar counters por {dimension}")


def verify_no_counter_sum(panels_query_text: str) -> None:
    """Reject SUM() over any cumulative counter in dashboard panels."""
    for name in CUMULATIVE_COUNTER_FIELDS:
        field = INSTRUMENT_FIELDS[name]
        if f"SUM({field})" in panels_query_text:
            raise ValueError(
                f"SUM({field}) em painel soma snapshots cumulativos e multiplica o valor; "
                "use incrementos na janela (LAST − FIRST por série, com tratamento de reset)"
            )


def counter_window_totals(
    panel_key: str, window_points: list[dict], baseline_points: list[dict]
) -> dict[str | None, dict[str, float]]:
    """Mirror the per-resource ES|QL aggregation for offline counter checks."""
    names = COUNTER_PANEL_FIELDS[panel_key]
    dimension = COUNTER_PANEL_DIMENSION[panel_key]
    series: dict[tuple, dict[str, dict[str, float | None]]] = {}
    for points, slot in ((baseline_points, "base"), (window_points, "last")):
        for point in points:
            key = (point["service"], point["instance"], point.get(dimension) if dimension else None)
            values = series.setdefault(
                key, {name: {"base": None, "last": None} for name in names}
            )
            for name in names:
                if name in point:
                    values[name][slot] = point[name]

    totals: dict[str | None, dict[str, float]] = {}
    for (_, _, group), values in series.items():
        row = totals.setdefault(group, {_counter_alias(name): 0.0 for name in names})
        for name in names:
            base = values[name]["base"] or 0
            end = values[name]["last"]
            if end is None:
                end = base
            row[_counter_alias(name)] += end - base if end >= base else end
    return totals


def verify_counter_panel_semantics() -> None:
    """Catch a missing baseline, first-snapshot loss, and producer mixing."""
    created = "media.upload.created"
    funnel_baseline = [{"service": "media", "instance": "api", created: 10}]
    funnel_window = [{"service": "media", "instance": "api", created: 14}]
    if counter_window_totals("upload-funnel", funnel_window, funnel_baseline)[None]["created"] != 4:
        raise ValueError("funil: um único snapshot após quatro eventos deve contar quatro")

    # O primeiro snapshot na janela já contém os eventos; FIRST dentro dela
    # subcontaria mesmo que uma segunda amostra estável fosse exportada.
    funnel_window.append({"service": "media", "instance": "api", created: 14})
    if counter_window_totals("upload-funnel", funnel_window, funnel_baseline)[None]["created"] != 4:
        raise ValueError("funil: eventos antes do primeiro snapshot da janela foram perdidos")

    published = "media.outbox.published"
    outbox_baseline = [
        {"service": "media", "instance": "api", "event": "ativo-pronto", published: 100},
        {"service": "media", "instance": "worker", "event": "ativo-pronto", published: 5},
    ]
    outbox_window = [
        {"service": "media", "instance": "api", "event": "ativo-pronto", published: 102},
        {"service": "media", "instance": "worker", "event": "ativo-pronto", published: 8},
    ]
    if counter_window_totals("outbox-publishes", outbox_window, outbox_baseline)["ativo-pronto"]["published"] != 5:
        raise ValueError("outbox: incrementos de api e worker devem somar cinco por evento")
    print("Painéis de counters: baseline, primeiro snapshot e produtores api/worker verificados offline.")

# Alert rules A1–A5 (TechSpec § Infraestrutura de visualização, tabela contratual).
# Threshold rules over metrics-generic*, evaluated every 1 min on a 15 min window,
# with NO notification connector (PRD DP-02: alcance é a tela de Alertas do Kibana).
# The ES|QL query returns rows only while the breach holds; the rule threshold
# ([0], ">") fires on row count, so the alert recovers alone when rows stop.
# A2 consome counters cumulativos: calcula incrementos (valor final na janela −
# baseline imediatamente anterior à janela — ou 0 quando a série é nova, sem
# amostra anterior — por série, com tratamento de reset)
# em vez de somar snapshots — ver verify_a2_* abaixo.
ALERT_RULE_TYPE_ID = ".es-query"
ALERT_CONSUMER = "stackAlerts"
ALERT_SCHEDULE_INTERVAL = "1m"
ALERT_TIME_WINDOW_SIZE = 15
ALERT_TIME_WINDOW_UNIT = "m"
ALERT_TAG = "observabilidade-midia"
ALERT_RULES = {
    "midia-a1-video-preso": {
        "code": "A1",
        "name": "[Mídia] A1 · Vídeo preso",
        "instruments": ("media.videos.stuck",),
        "threshold_markers": ("stuck > 0",),
        "esql": (
            "FROM metrics-generic*"
            " | WHERE @timestamp >= NOW() - 15 minutes"
            " AND metrics.media.videos.stuck IS NOT NULL"
            " | STATS stuck = MAX(metrics.media.videos.stuck)"
            " | WHERE stuck > 0"
        ),
    },
    "midia-a2-taxa-falha-preparacao": {
        "code": "A2",
        "name": "[Mídia] A2 · Taxa de falha de preparação",
        "instruments": ("media.videos.completed", "media.videos.failed"),
        "threshold_markers": ("total >= 4", "failure_rate > 0.10"),
        # media.videos.completed/failed são counters cumulativos (o exportador OTLP
        # .NET usa temporality cumulativa): cada snapshot repete o total acumulado,
        # então SOMAR os pontos multiplica o valor. Os incrementos reais na janela
        # são valor_final_na_janela − baseline_antes_da_janela por série ordenada
        # por @timestamp (FIRST/LAST com sort field, GA desde o ES 9.4), com reset
        # tratado como "acumulado desde o restart" (vale o final quando
        # final < início, sempre ≥ 0), somados entre séries. `failed` tem a
        # dimensão `reason` (uma série por motivo); `completed` não tem dimensão
        # (série única "__completed__").
        # O filtro busca 16 min (15 da janela + 1 de baseline) para capturar a
        # amostra imediatamente anterior ao início da janela: sem ela, eventos
        # entre o início da janela e o primeiro snapshot produziriam FIRST = LAST
        # (incremento 0) e A2 perderia o disparo. O baseline é o LAST antes da
        # janela (WHERE in_window == 0); sem amostra anterior, a série é nova —
        # o counter começou em 0 quando a série apareceu — então o início é 0
        # (COALESCE(base, 0), nunca o FIRST dentro da janela).
        "esql": (
            "FROM metrics-generic*"
            " | WHERE @timestamp >= NOW() - 16 minutes"
            " AND (metrics.media.videos.completed IS NOT NULL"
            " OR metrics.media.videos.failed IS NOT NULL)"
            " | EVAL reason = COALESCE(attributes.reason, \"__completed__\"),"
            " in_window = CASE(@timestamp >= NOW() - 15 minutes, 1, 0)"
            " | STATS c_base = LAST(metrics.media.videos.completed, @timestamp) WHERE in_window == 0,"
            " c_first = FIRST(metrics.media.videos.completed, @timestamp) WHERE in_window == 1,"
            " c_last = LAST(metrics.media.videos.completed, @timestamp) WHERE in_window == 1,"
            " f_base = LAST(metrics.media.videos.failed, @timestamp) WHERE in_window == 0,"
            " f_first = FIRST(metrics.media.videos.failed, @timestamp) WHERE in_window == 1,"
            " f_last = LAST(metrics.media.videos.failed, @timestamp) WHERE in_window == 1 BY reason"
            " | EVAL c_start = COALESCE(c_base, 0), c_end = COALESCE(c_last, c_start),"
            " f_start = COALESCE(f_base, 0), f_end = COALESCE(f_last, f_start)"
            " | EVAL c_inc = CASE(c_end >= c_start, c_end - c_start, c_end),"
            " f_inc = CASE(f_end >= f_start, f_end - f_start, f_end)"
            " | STATS completed = SUM(c_inc), failed = SUM(f_inc)"
            " | EVAL c = COALESCE(completed, 0), f = COALESCE(failed, 0)"
            " | EVAL total = c + f, failure_rate = f * 1.0 / total"
            " | WHERE total >= 4 AND failure_rate > 0.10"
        ),
    },
    "midia-a3-outbox": {
        "code": "A3",
        "name": "[Mídia] A3 · Outbox esgotado/atrasado",
        "instruments": ("media.outbox.exhausted", "media.outbox.oldest_pending"),
        "threshold_markers": ("exhausted > 0", "oldest_pending_seconds > 600"),
        "esql": (
            "FROM metrics-generic*"
            " | WHERE @timestamp >= NOW() - 15 minutes"
            " AND (metrics.media.outbox.exhausted IS NOT NULL"
            " OR metrics.media.outbox.oldest_pending IS NOT NULL)"
            " | STATS exhausted = MAX(metrics.media.outbox.exhausted),"
            " oldest_pending_seconds = MAX(metrics.media.outbox.oldest_pending)"
            " | WHERE exhausted > 0 OR oldest_pending_seconds > 600"
        ),
    },
    "midia-a4-fila-parada": {
        "code": "A4",
        "name": "[Mídia] A4 · Fila parada",
        "instruments": ("media.videos.oldest_waiting",),
        "threshold_markers": ("oldest_waiting_seconds > 1800",),
        "esql": (
            "FROM metrics-generic*"
            " | WHERE @timestamp >= NOW() - 15 minutes"
            " AND metrics.media.videos.oldest_waiting IS NOT NULL"
            " | STATS oldest_waiting_seconds = MAX(metrics.media.videos.oldest_waiting)"
            " | WHERE oldest_waiting_seconds > 1800"
        ),
    },
    "midia-a5-dlq": {
        "code": "A5",
        "name": "[Mídia] A5 · DLQ não vazia",
        "instruments": ("media.messaging.dlq.messages",),
        "threshold_markers": ("dlq_messages > 0",),
        "esql": (
            "FROM metrics-generic*"
            " | WHERE @timestamp >= NOW() - 15 minutes"
            " AND metrics.media.messaging.dlq.messages IS NOT NULL"
            " | STATS dlq_messages = MAX(metrics.media.messaging.dlq.messages)"
            " | WHERE dlq_messages > 0"
        ),
    },
}
ALERT_CORE_MIGRATION_VERSION = "8.8.0"
ALERT_TYPE_MIGRATION_VERSION = "10.14.0"


def stable_id(name: str) -> str:
    """Return the fixed child ID used by the dashboard definition."""
    return str(uuid.uuid5(uuid.NAMESPACE_URL, f"code-for-coders:{DASHBOARD_ID}:panel/{name}"))


def alert_rule_params(esql: str) -> dict:
    """Return the canonical .es-query params for an A1–A5 rule."""
    return {
        "aggType": "count",
        "esqlQuery": {"esql": esql},
        "excludeHitsFromPreviousRun": True,
        "groupBy": "all",
        "searchType": "esqlQuery",
        "size": 0,
        "threshold": [0],
        "thresholdComparator": ">",
        "timeField": TIME_FIELD,
        "timeWindowSize": ALERT_TIME_WINDOW_SIZE,
        "timeWindowUnit": ALERT_TIME_WINDOW_UNIT,
    }


def build_alert_saved_object(rule_id: str, rule: dict) -> dict:
    """Return the versioned saved object for an A1–A5 alert rule."""
    return {
        "type": "alert",
        "id": rule_id,
        "attributes": {
            "name": rule["name"],
            "tags": [ALERT_TAG, rule["code"]],
            "consumer": ALERT_CONSUMER,
            "schedule": {"interval": ALERT_SCHEDULE_INTERVAL},
            "alertTypeId": ALERT_RULE_TYPE_ID,
            "params": alert_rule_params(rule["esql"]),
            "actions": [],
            "enabled": True,
            "throttle": None,
            "notifyWhen": None,
            "muteAll": False,
        },
        "references": [],
        "coreMigrationVersion": ALERT_CORE_MIGRATION_VERSION,
        "typeMigrationVersion": ALERT_TYPE_MIGRATION_VERSION,
    }


def verify_a2_esql_shape(esql: str, rule_id: str) -> None:
    """A2 consome counters cumulativos: exige final − baseline por série."""
    required = (
        "NOW() - 16 minutes",
        "in_window",
        "c_base = LAST(metrics.media.videos.completed, @timestamp) WHERE in_window == 0",
        "c_last = LAST(metrics.media.videos.completed, @timestamp) WHERE in_window == 1",
        "f_base = LAST(metrics.media.videos.failed, @timestamp) WHERE in_window == 0",
        "f_last = LAST(metrics.media.videos.failed, @timestamp) WHERE in_window == 1",
        "COALESCE(c_base, 0)",
        "COALESCE(f_base, 0)",
        "BY reason",
    )
    for snippet in required:
        if snippet not in esql:
            raise ValueError(
                f"regra A2 ({rule_id}) precisa calcular incrementos por série "
                f"(falta {snippet!r}); somar snapshots cumulativos multiplica o valor"
            )
    for forbidden in (
        "SUM(metrics.media.videos.completed)",
        "SUM(metrics.media.videos.failed)",
        "COALESCE(c_base, c_first",
        "COALESCE(f_base, f_first",
    ):
        if forbidden in esql:
            raise ValueError(
                f"regra A2 ({rule_id}) usa forma proibida {forbidden!r}: "
                "baseline ausente significa série nova (início 0); "
                "recuar para FIRST perde as finalizações antes do 1º snapshot"
            )


def a2_old_window_totals(points: list[dict]) -> tuple[float, float, float, float]:
    """Matemática antiga (incorreta) de A2: SUM dos snapshots cumulativos."""
    completed = sum(0 if point.get("completed") is None else point["completed"] for point in points)
    failed = sum(0 if point.get("failed") is None else point["failed"] for point in points)
    total = completed + failed
    return completed, failed, total, (failed / total if total else 0.0)


def a2_window_increments(points: list[dict]) -> tuple[float, float, float, float]:
    """Matemática antiga (incorreta) de A2: início recua para FIRST na janela.

    Espelha o COALESCE(base, first, 0) anterior: perde as finalizações entre o
    início da janela e o primeiro snapshot — ver casos (d) e (e) abaixo."""
    series: dict[str, dict[str, float | None]] = {}
    for point in points:
        key = point.get("reason") or "__completed__"
        entry = series.setdefault(
            key,
            {"c_base": None, "c_first": None, "c_last": None, "f_base": None, "f_first": None, "f_last": None},
        )
        completed, failed = point.get("completed"), point.get("failed")
        if completed is not None:
            if entry["c_first"] is None:
                entry["c_first"] = completed
            entry["c_last"] = completed
        if failed is not None:
            if entry["f_first"] is None:
                entry["f_first"] = failed
            entry["f_last"] = failed

    def increment(start: float | None, first: float | None, last: float | None) -> float:
        start = first if start is None else start
        start = 0 if start is None else start
        last = start if last is None else last
        return (last - start) if last >= start else last

    completed = sum(increment(entry["c_base"], entry["c_first"], entry["c_last"]) for entry in series.values())
    failed = sum(increment(entry["f_base"], entry["f_first"], entry["f_last"]) for entry in series.values())
    total = completed + failed
    return completed, failed, total, (failed / total if total else 0.0)


def a2_window_increments_with_baseline(
    window_points: list[dict], baseline_points: list[dict]
) -> tuple[float, float, float, float]:
    """Matemática nova de A2, espelhando a query ES|QL: final − baseline por série.

    `window_points` e `baseline_points` estão em ordem de @timestamp. Cada série
    é a dimensão `reason` (`failed`) ou "__completed__" (`completed`, sem
    dimensão). O início é o LAST do baseline; sem amostra anterior, a série é
    nova e o counter começou em 0 (COALESCE(base, 0), nunca o FIRST na janela);
    o fim é o LAST na janela (fallback para o início). Reset
    (fim < início) vale o fim — o acumulado desde o restart — sempre ≥ 0.
    """
    series: dict[str, dict[str, float | None]] = {}
    for point in baseline_points:
        key = point.get("reason") or "__completed__"
        entry = series.setdefault(
            key,
            {"c_base": None, "c_first": None, "c_last": None, "f_base": None, "f_first": None, "f_last": None},
        )
        if point.get("completed") is not None:
            entry["c_base"] = point["completed"]
        if point.get("failed") is not None:
            entry["f_base"] = point["failed"]
    for point in window_points:
        key = point.get("reason") or "__completed__"
        entry = series.setdefault(
            key,
            {"c_base": None, "c_first": None, "c_last": None, "f_base": None, "f_first": None, "f_last": None},
        )
        completed, failed = point.get("completed"), point.get("failed")
        if completed is not None:
            if entry["c_first"] is None:
                entry["c_first"] = completed
            entry["c_last"] = completed
        if failed is not None:
            if entry["f_first"] is None:
                entry["f_first"] = failed
            entry["f_last"] = failed

    def increment(start: float | None, first: float | None, last: float | None) -> float:
        # Série nova (sem baseline): o counter começou em 0 — nunca o FIRST.
        start = 0 if start is None else start
        last = start if last is None else last
        return (last - start) if last >= start else last

    completed = sum(increment(entry["c_base"], entry["c_first"], entry["c_last"]) for entry in series.values())
    failed = sum(increment(entry["f_base"], entry["f_first"], entry["f_last"]) for entry in series.values())
    total = completed + failed
    return completed, failed, total, (failed / total if total else 0.0)


def verify_a2_counter_semantics() -> None:
    """Demonstração offline de que A2 conta finalizações reais, não snapshots.

    (a) Uma falha isolada repetida em 15 snapshots: a forma antiga (SUM) atinge o
    mínimo e dispara com taxa 100%; a forma nova conta 0 incrementos e não dispara.
    (b) 4 conclusões + 2 falhas reais na janela: a forma nova dispara (total 6,
    taxa 1/3). (c) Reset de contador no meio da janela: a forma nova conta só o
    acumulado pós-restart; a forma antiga multiplica os totais pré-reset.
    (d) Eventos no início da janela: baseline conhecido + 4 finalizações entre o
    início da janela e o primeiro snapshot → a forma sem baseline conta 0 (não
    dispara) e a forma com baseline conta 4 (dispara).
    (e) Série nova sem baseline (ex.: um `reason` que nunca falhou antes):
    baseline ausente + 4 falhas na janela → a forma nova (início 0) conta 4
    (dispara); a forma antiga (recuo para FIRST) conta 3 em rampa 1→4, ou 0 se
    as 4 falhas chegam antes do primeiro snapshot (abaixo do mínimo de 4,
    perde o disparo).
    """
    # (a) Uma única falha (cumulativo failed=1), repetida a cada 60 s na janela.
    # A série já existia antes da janela (baseline failed=1), então não há
    # incremento real.
    lonely_failure = [
        {"completed": 0, "failed": 1, "reason": "unreadable-file"} for _ in range(15)
    ]
    lonely_baseline = [
        {"completed": 0, "failed": 1, "reason": "unreadable-file"},
    ]
    lonely_old = a2_old_window_totals(lonely_failure)
    lonely_new = a2_window_increments_with_baseline(lonely_failure, lonely_baseline)
    if not (lonely_old[2] >= 4 and lonely_old[3] > 0.10):
        raise ValueError("demonstração A2(a) inconsistente: forma antiga deveria disparar")
    if lonely_new[2] != 0:
        raise ValueError(
            f"demonstração A2(a) inconsistente: falha isolada repetida contou {lonely_new[2]} "
            "finalizações na forma nova (esperado 0)"
        )

    # (b) Volume real: completed 100→104; failed em 2 motivos (50→51, 20→21).
    # As séries já existiam (baselines 100/50/20), então os incrementos são reais.
    real_volume = (
        [{"completed": 100 + i, "failed": None, "reason": None} for i in range(5)]
        + [{"completed": None, "failed": 50 + (i // 8), "reason": "unreadable-file"} for i in range(15)]
        + [{"completed": None, "failed": 20 + (i // 8), "reason": "unsupported-format"} for i in range(15)]
    )
    volume_baseline = [
        {"completed": 100, "failed": None, "reason": None},
        {"completed": None, "failed": 50, "reason": "unreadable-file"},
        {"completed": None, "failed": 20, "reason": "unsupported-format"},
    ]
    volume_new = a2_window_increments_with_baseline(real_volume, volume_baseline)
    if not (volume_new[0] == 4 and volume_new[1] == 2 and volume_new[2] >= 4 and volume_new[3] > 0.10):
        raise ValueError(
            f"demonstração A2(b) inconsistente: forma nova contou "
            f"completed={volume_new[0]} failed={volume_new[1]} (esperado 4 e 2)"
        )

    # (c) Reset no meio da janela: completed 100,101 → restart → 0,1,2.
    # A série existia em 100 antes da janela; após o reset vale o acumulado
    # pós-restart.
    with_reset = [
        {"completed": value, "failed": None, "reason": None}
        for value in (100, 101, 0, 1, 2)
    ]
    reset_baseline = [
        {"completed": 100, "failed": None, "reason": None},
    ]
    reset_old = a2_old_window_totals(with_reset)
    reset_new = a2_window_increments_with_baseline(with_reset, reset_baseline)
    if reset_new[0] != 2:
        raise ValueError(
            f"demonstração A2(c) inconsistente: após reset a forma nova contou "
            f"{reset_new[0]} conclusões (esperado 2, o acumulado pós-restart)"
        )
    if reset_old[2] != 204:
        raise ValueError(
            f"demonstração A2(c) inconsistente: forma antiga somou {reset_old[2]} (esperado 204)"
        )

    # (d) Eventos no início da janela: baseline completed=100 / failed=50 e,
    # já no primeiro snapshot da janela, completed=103 / failed=51 (3 conclusões
    # + 1 falha entre o início da janela e o primeiro snapshot; depois estável).
    # Sem baseline, FIRST = LAST na janela → incremento 0, A2 perde o disparo.
    early_baseline = [
        {"completed": 100, "failed": None, "reason": None},
        {"completed": None, "failed": 50, "reason": "unreadable-file"},
    ]
    early_window = (
        [{"completed": 103, "failed": None, "reason": None} for _ in range(14)]
        + [{"completed": None, "failed": 51, "reason": "unreadable-file"} for _ in range(14)]
    )
    early_without_baseline = a2_window_increments(early_window)
    early_with_baseline = a2_window_increments_with_baseline(early_window, early_baseline)
    if early_without_baseline[2] != 0:
        raise ValueError(
            "demonstração A2(d) inconsistente: forma sem baseline contou "
            f"{early_without_baseline[2]} finalizações (esperado 0)"
        )
    if not (early_with_baseline[0] == 3 and early_with_baseline[1] == 1):
        raise ValueError(
            "demonstração A2(d) inconsistente: forma com baseline contou "
            f"completed={early_with_baseline[0]} failed={early_with_baseline[1]} (esperado 3 e 1)"
        )
    if not (early_with_baseline[2] >= 4 and early_with_baseline[3] > 0.10):
        raise ValueError(
            "demonstração A2(d) inconsistente: forma com baseline deveria disparar "
            f"(total={early_with_baseline[2]} taxa={early_with_baseline[3]:.2f})"
        )

    # (e) Série nova sem baseline: um reason que nunca falhou antes — o counter
    # começa em 0 quando a série aparece, então o início é 0. Rampa 1→4 na
    # janela: a forma nova conta 4 (dispara); a forma antiga, recuando para
    # FIRST=1, conta 3 (abaixo do mínimo de 4, perde o disparo). Se as 4 falhas
    # chegam antes do primeiro snapshot (janela estável em 4), a forma antiga
    # conta 0.
    new_series_ramp = [
        {"completed": None, "failed": value, "reason": "corrupt-frame"}
        for value in (1, 2, 3, 4)
    ]
    new_series_flat = [
        {"completed": None, "failed": 4, "reason": "corrupt-frame"} for _ in range(14)
    ]
    ramp_old = a2_window_increments(new_series_ramp)
    ramp_new = a2_window_increments_with_baseline(new_series_ramp, [])
    flat_old = a2_window_increments(new_series_flat)
    flat_new = a2_window_increments_with_baseline(new_series_flat, [])
    if ramp_old[1] != 3 or ramp_old[2] >= 4:
        raise ValueError(
            "demonstração A2(e) inconsistente: forma antiga na rampa contou "
            f"failed={ramp_old[1]} total={ramp_old[2]} (esperado 3, sem disparo)"
        )
    if not (ramp_new[1] == 4 and ramp_new[2] >= 4 and ramp_new[3] > 0.10):
        raise ValueError(
            "demonstração A2(e) inconsistente: forma nova na rampa contou "
            f"failed={ramp_new[1]} total={ramp_new[2]} (esperado 4, com disparo)"
        )
    if flat_old[2] != 0:
        raise ValueError(
            "demonstração A2(e) inconsistente: forma antiga na janela estável contou "
            f"{flat_old[2]} finalizações (esperado 0)"
        )
    if not (flat_new[1] == 4 and flat_new[2] >= 4 and flat_new[3] > 0.10):
        raise ValueError(
            "demonstração A2(e) inconsistente: forma nova na janela estável contou "
            f"failed={flat_new[1]} total={flat_new[2]} (esperado 4, com disparo)"
        )

    print(
        "A2 (counters cumulativos): falha isolada repetida em 15 snapshots → "
        f"forma antiga total={lonely_old[2]:.0f} taxa={lonely_old[3]:.2f} (dispararia), "
        f"forma nova total={lonely_new[2]:.0f} (não dispara); 4 conclusões + 2 falhas "
        f"reais → total={volume_new[2]:.0f} taxa={volume_new[3]:.2f} (dispara); reset "
        f"100,101→0,1,2 → forma nova incrementos={reset_new[0]:.0f}, "
        f"forma antiga {reset_old[2]:.0f}; eventos no início da janela "
        f"(baseline + 4 finalizações antes do 1º snapshot) → sem baseline "
        f"total={early_without_baseline[2]:.0f} (não dispara), com baseline "
        f"total={early_with_baseline[2]:.0f} taxa={early_with_baseline[3]:.2f} (dispara); "
        f"série nova sem baseline (4 falhas, rampa 1→4) → forma antiga "
        f"failed={ramp_old[1]:.0f} (não dispara), forma nova "
        f"total={ramp_new[2]:.0f} taxa={ramp_new[3]:.2f} (dispara); janela estável "
        f"em 4 → forma antiga total={flat_old[2]:.0f} (não dispara), forma nova "
        f"total={flat_new[2]:.0f} (dispara)."
    )


def verify_alert_rules(by_type_and_id: dict) -> None:
    """Validate the five A1–A5 rules against the TechSpec contract."""
    allowed_fields = set(INSTRUMENT_FIELDS.values())
    for rule_id, rule in ALERT_RULES.items():
        saved_object = by_type_and_id.get(("alert", rule_id))
        if saved_object is None:
            raise ValueError(f"regra de alerta ausente no NDJSON: {rule['code']} ({rule_id})")
        attributes = saved_object.get("attributes", {})
        if attributes.get("name") != rule["name"]:
            raise ValueError(f"nome da regra {rule['code']} divergente do versionado")
        tags = attributes.get("tags", [])
        if ALERT_TAG not in tags or rule["code"] not in tags:
            raise ValueError(f"regra {rule['code']} precisa das tags {ALERT_TAG} e {rule['code']}")
        if attributes.get("consumer") != ALERT_CONSUMER:
            raise ValueError(f"regra {rule['code']} precisa do consumer {ALERT_CONSUMER}")
        if attributes.get("schedule") != {"interval": ALERT_SCHEDULE_INTERVAL}:
            raise ValueError(f"regra {rule['code']} precisa avaliar a cada {ALERT_SCHEDULE_INTERVAL}")
        if attributes.get("alertTypeId") != ALERT_RULE_TYPE_ID:
            raise ValueError(f"regra {rule['code']} precisa ser do tipo {ALERT_RULE_TYPE_ID}")
        if attributes.get("actions") != []:
            raise ValueError(f"regra {rule['code']} não pode ter conector de notificação (DP-02)")
        if attributes.get("enabled") is not True:
            raise ValueError(f"regra {rule['code']} precisa nascer habilitada")

        params = attributes.get("params", {})
        if params.get("searchType") != "esqlQuery":
            raise ValueError(f"regra {rule['code']} precisa consultar por ES|QL")
        esql = (params.get("esqlQuery") or {}).get("esql", "")
        if not esql.startswith("FROM metrics-generic*"):
            raise ValueError(f"regra {rule['code']} não consulta metrics-generic*")
        if params.get("timeField") != TIME_FIELD:
            raise ValueError(f"regra {rule['code']} precisa usar o campo temporal {TIME_FIELD}")
        if params.get("timeWindowSize") != ALERT_TIME_WINDOW_SIZE or params.get("timeWindowUnit") != ALERT_TIME_WINDOW_UNIT:
            raise ValueError(f"regra {rule['code']} precisa da janela de 15 min")
        if params.get("threshold") != [0] or params.get("thresholdComparator") != ">":
            raise ValueError(f"regra {rule['code']} dispara por contagem de linhas acima de zero")

        expected_fields = {INSTRUMENT_FIELDS[name] for name in rule["instruments"]}
        for field in expected_fields:
            if field not in esql:
                raise ValueError(f"regra {rule['code']} não referencia {field}")
        for marker in rule["threshold_markers"]:
            if marker not in esql:
                raise ValueError(f"regra {rule['code']} com threshold divergente da TechSpec (falta {marker!r})")
        referenced = set(re.findall(r"metrics\.[A-Za-z0-9_.]+", esql))
        unknown = referenced - allowed_fields
        if unknown:
            raise ValueError(f"regra {rule['code']} referencia instrumentos fora da TechSpec: {sorted(unknown)}")
        if rule["code"] == "A2":
            verify_a2_esql_shape(esql, rule_id)

    alert_ids = {key[1] for key in by_type_and_id if key[0] == "alert"}
    if alert_ids != set(ALERT_RULES):
        raise ValueError("o NDJSON deve conter exatamente as regras A1–A5 com IDs estáveis")
    verify_a2_counter_semantics()


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
    } | {("alert", rule_id) for rule_id in ALERT_RULES}
    if set(by_type_and_id) != expected_objects:
        raise ValueError(
            "o NDJSON deve conter o data view, o dashboard e as regras A1–A5 com IDs estáveis"
        )

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
        if key in COUNTER_PANEL_FIELDS and embeddable.get("ignoreTimerange") is not True:
            raise ValueError(f"painel {title!r} precisa consultar o baseline anterior à janela")
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
    verify_counter_increment_shape(funnel_query, "upload-funnel", PANEL_TITLES["upload-funnel"])

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
    verify_counter_increment_shape(claims_query, "queue-claims", PANEL_TITLES["queue-claims"])

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
    verify_counter_increment_shape(
        outcomes_query, "preparation-outcomes", PANEL_TITLES["preparation-outcomes"]
    )

    failures_query = panel_query(panels_by_id[stable_id("preparation-failures")])
    if INSTRUMENT_FIELDS["media.videos.failed"] not in failures_query:
        raise ValueError("painel de preparação deve comparar media.videos.failed por reason")
    verify_counter_increment_shape(
        failures_query, "preparation-failures", PANEL_TITLES["preparation-failures"]
    )

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
    verify_counter_increment_shape(
        outbox_publishes_query, "outbox-publishes", PANEL_TITLES["outbox-publishes"]
    )

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
    verify_no_counter_sum(query_text)
    verify_counter_panel_semantics()
    verify_alert_rules(by_type_and_id)
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
    ignore_timerange: bool = False,
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
    panel = {
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
    if ignore_timerange:
        panel["embeddableConfig"]["ignoreTimerange"] = True
    return panel


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
            counter_increment_query("preparation-outcomes"),
            [("completed", "number"), ("retried", "number")],
            0,
            66,
        ),
        (
            "preparation-failures",
            counter_increment_query("preparation-failures"),
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
            counter_increment_query("outbox-publishes"),
            [("event", "string"), ("published", "number"), ("publish_failed", "number")],
            24,
            78,
        ),
    ]
    # Counter panels owned by earlier slices (funnel, claims) are rewritten in
    # place with the canonical increment query, preserving their grid position.
    counter_rewrites = {
        "upload-funnel": [("created", "number"), ("completed", "number"), ("expired", "number")],
        "queue-claims": [("claimed", "number"), ("retried", "number")],
    }
    for key, columns in counter_rewrites.items():
        panel_id = stable_id(key)
        existing = existing_panels[panel_id]
        grid = existing.get("gridData", {})
        existing_panels[panel_id] = make_lens_panel(
            panel_id,
            PANEL_TITLES[key],
            counter_increment_query(key),
            columns,
            data_view_id,
            data_view,
            grid.get("x", 0),
            grid.get("y", 0),
            grid.get("w", 24),
            grid.get("h", 12),
            ignore_timerange=True,
        )
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
            ignore_timerange=key in COUNTER_PANEL_FIELDS,
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
    by_id = {(item.get("type"), item.get("id")): item for item in saved_objects}
    for rule_id, rule in ALERT_RULES.items():
        by_id[("alert", rule_id)] = build_alert_saved_object(rule_id, rule)
    saved_objects = [by_id[key] for key in sorted(by_id, key=lambda key: (key[0], key[1]))]
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


def alerting_request(
    method: str,
    kibana_url: str,
    authorization: str,
    path: str,
    username: str,
    password: str,
    payload: dict | None = None,
) -> tuple[int | None, dict | str]:
    """Call the Kibana Alerting API, returning (status, body). 404 yields (None, {})."""
    data = json.dumps(payload, separators=(",", ":")).encode("utf-8") if payload is not None else None
    request = Request(
        f"{kibana_url}{path}",
        data=data,
        method=method,
        headers={
            "Authorization": f"Basic {authorization}",
            "Content-Type": "application/json",
            "kbn-xsrf": "true",
        },
    )
    try:
        with urlopen(request, timeout=30) as response:
            raw = response.read().decode("utf-8")
            return response.status, json.loads(raw) if raw else {}
    except HTTPError as error:
        if error.code == 404:
            return None, {}
        raise ValueError(
            f"falha na API de alertas ({method} {path}): {safe_http_error(error, username, password)}"
        ) from error
    except URLError as error:
        raise ValueError(f"não foi possível acessar Kibana: {error.reason}") from error
    except TimeoutError as error:
        raise ValueError("tempo limite ao falar com a API de alertas do Kibana") from error
    except json.JSONDecodeError as error:
        raise ValueError("Kibana retornou uma resposta inválida da API de alertas") from error


def fetch_alert_rule(
    kibana_url: str, authorization: str, username: str, password: str, rule_id: str
) -> dict | None:
    """Fetch a rule from the Alerting API, or None when it does not exist."""
    status, body = alerting_request(
        "GET", kibana_url, authorization, f"/api/alerting/rule/{rule_id}", username, password
    )
    if status is None:
        return None
    if not isinstance(body, dict):
        raise ValueError(f"resposta inválida da API de alertas para a regra {rule_id}")
    return body


def upsert_alert_rule(
    kibana_url: str, authorization: str, username: str, password: str, rule_id: str, rule: dict
) -> str:
    """Create or replace an A1–A5 rule via the Alerting API; returns created|updated."""
    params = alert_rule_params(rule["esql"])
    existing = fetch_alert_rule(kibana_url, authorization, username, password, rule_id)
    if existing is None:
        _, body = alerting_request(
            "POST",
            kibana_url,
            authorization,
            f"/api/alerting/rule/{rule_id}",
            username,
            password,
            {
                "name": rule["name"],
                "tags": [ALERT_TAG, rule["code"]],
                "rule_type_id": ALERT_RULE_TYPE_ID,
                "consumer": ALERT_CONSUMER,
                "schedule": {"interval": ALERT_SCHEDULE_INTERVAL},
                "params": params,
                "actions": [],
                "enabled": True,
            },
        )
        if not isinstance(body, dict) or body.get("id") != rule_id:
            raise ValueError(f"criação da regra {rule['code']} retornou resposta inválida")
        return "created"
    _, body = alerting_request(
        "PUT",
        kibana_url,
        authorization,
        f"/api/alerting/rule/{rule_id}",
        username,
        password,
        {
            "name": rule["name"],
            "tags": [ALERT_TAG, rule["code"]],
            "schedule": {"interval": ALERT_SCHEDULE_INTERVAL},
            "params": params,
            "actions": [],
            "throttle": None,
            "notify_when": None,
        },
    )
    if not isinstance(body, dict) or body.get("id") != rule_id:
        raise ValueError(f"atualização da regra {rule['code']} retornou resposta inválida")
    alerting_request(
        "POST", kibana_url, authorization, f"/api/alerting/rule/{rule_id}/_enable", username, password, {}
    )
    return "updated"


def rule_to_saved_object(rule_body: dict) -> dict:
    """Map a live Alerting API rule to its versioned saved object shape."""
    rule_id = rule_body.get("id", "")
    expected = ALERT_RULES.get(rule_id, {})
    params = rule_body.get("params", {})
    return {
        "type": "alert",
        "id": rule_id,
        "attributes": {
            "name": rule_body.get("name", expected.get("name", rule_id)),
            "tags": rule_body.get("tags", [ALERT_TAG, expected.get("code", "")]),
            "consumer": rule_body.get("consumer", ALERT_CONSUMER),
            "schedule": rule_body.get("schedule", {"interval": ALERT_SCHEDULE_INTERVAL}),
            "alertTypeId": rule_body.get("rule_type_id", ALERT_RULE_TYPE_ID),
            "params": params,
            "actions": rule_body.get("actions", []),
            "enabled": True,
            "throttle": rule_body.get("throttle"),
            "notifyWhen": rule_body.get("notify_when"),
            "muteAll": rule_body.get("mute_all", False),
        },
        "references": [],
        "coreMigrationVersion": ALERT_CORE_MIGRATION_VERSION,
        "typeMigrationVersion": ALERT_TYPE_MIGRATION_VERSION,
    }


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
        exported_objects, _ = load_saved_objects(temporary_path)
        dashboard_objects = [
            item
            for item in exported_objects
            if (item.get("type"), item.get("id"))
            in {("index-pattern", DATA_VIEW_ID), ("dashboard", DASHBOARD_ID)}
        ]
        if len(dashboard_objects) != 2:
            raise ValueError("o export do Kibana não retornou o data view e o dashboard esperados")
        alert_objects = []
        for rule_id in ALERT_RULES:
            live = fetch_alert_rule(kibana_url, authorization, username, password, rule_id)
            if live is None:
                raise ValueError(f"regra {ALERT_RULES[rule_id]['code']} não existe no Kibana para exportar")
            alert_objects.append(rule_to_saved_object(live))
        saved_objects = dashboard_objects + sorted(alert_objects, key=lambda item: item["id"])
        summary = {
            "exportedCount": len(saved_objects),
            "excludedObjects": [],
            "excludedObjectsCount": 0,
            "missingRefCount": 0,
            "missingReferences": [],
        }
        content = "\n".join(
            json.dumps(item, ensure_ascii=False, separators=(",", ":")) for item in [*saved_objects, summary]
        ) + "\n"
        temporary_path.write_text(content, encoding="utf-8")
        verify_saved_objects(temporary_path)
        os.replace(temporary_path, path)
    except (OSError, ValueError):
        temporary_path.unlink(missing_ok=True)
        raise


def import_saved_objects(path: Path = SAVED_OBJECTS_PATH) -> None:
    saved_objects, _ = verify_saved_objects(path)
    username, password, kibana_url, authorization = kibana_connection()
    dashboard_objects = [item for item in saved_objects if item.get("type") != "alert"]
    payload_ndjson = "\n".join(
        json.dumps(item, ensure_ascii=False, separators=(",", ":")) for item in dashboard_objects
    ) + "\n"
    boundary = "----codex-kibana-import-" + uuid.uuid4().hex
    body = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="observabilidade-midia.ndjson"\r\n'
        "Content-Type: application/ndjson\r\n\r\n"
    ).encode("utf-8") + payload_ndjson.encode("utf-8") + f"\r\n--{boundary}--\r\n".encode("ascii")
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
    if not result.get("success") or errors or imported_count != len(dashboard_objects):
        error_types = ", ".join(
            str(item.get("error", {}).get("type", "erro de importação"))
            for item in errors
            if isinstance(item, dict)
        )
        detail = f": {error_types}" if error_types else ""
        raise ValueError(f"importação incompleta ({imported_count}/{len(dashboard_objects)} objetos){detail}")

    print(f"Importados {imported_count} saved objects com overwrite em {kibana_url}.")
    print(f"Dashboard: {DASHBOARD_TITLE} (ID {DASHBOARD_ID}).")
    for rule_id, rule in ALERT_RULES.items():
        outcome = upsert_alert_rule(kibana_url, authorization, username, password, rule_id, rule)
        print(f"Regra {rule['code']}: {rule['name']} (ID {rule_id}) {outcome} e habilitada, sem conector.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_mutually_exclusive_group()
    actions.add_argument(
        "--verify-only",
        action="store_true",
        help="valida o NDJSON, os IDs, as métricas, os painéis e as regras A1–A5 (IDs, instrumentos, thresholds, zero conectores); não usa rede",
    )
    actions.add_argument(
        "--generate-only",
        action="store_true",
        help="gera os painéis e as regras A1–A5 no NDJSON versionado e valida o resultado; não usa rede",
    )
    actions.add_argument(
        "--export",
        action="store_true",
        help="exporta o data view, o dashboard e as regras A1–A5 atuais do Kibana para o NDJSON versionado",
    )
    actions.add_argument(
        "--import",
        dest="do_import",
        action="store_true",
        help="importa o dashboard e cria/atualiza as regras A1–A5 no Kibana configurado, habilitando-as sem conector",
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
            f"({', '.join(INSTRUMENT_FIELDS)}); painéis de envio, fila, preparação e outbox/DLQ presentes; "
            "regras A1–A5 presentes com IDs estáveis, cada uma referencia só instrumentos da tabela "
            "da TechSpec, thresholds iguais aos da spec, nenhuma com conector de notificação. "
            "A2 calcula incrementos de counters cumulativos na janela (valor final "
            "na janela − baseline imediatamente anterior à janela, por série "
            "ordenada por @timestamp, com tratamento de reset, somados entre as séries de reason), "
            "em vez de somar snapshots; a demonstração offline acima confirma a semântica, "
            "incluindo o caso de eventos no início da janela e o de série nova sem "
            "baseline (início 0). Os painéis de funil, claims, "
            "resultados, falhas por motivo e publicações por evento calculam fim na janela "
            "menos o último baseline anterior por serviço, instância e atributo, tratam reset "
            "e somam incrementos das séries. O filtro temporal automático é ignorado nesses "
            "painéis para buscar o baseline; os limites do seletor entram via ?_tstart/?_tend. "
            "SUM() sobre counter cumulativo é reprovado. "
            f"Saved objects: {len(saved_objects)}."
        )
        return 0
    except (OSError, ValueError) as error:
        print(f"Erro: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
