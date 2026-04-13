#!/usr/bin/env python3
"""
Heuristic issue classifier for dotnet/aspire.

Classifies issues into quadrants (Integrations, Inner Loop, Polyglot,
Deployment, Dashboard, Infra) and type (bug/feature) using labels,
title keywords, and body text heuristics.

This module can be used standalone or swapped out for an LLM-based
classifier. The contract is:

    classify_issue(issue) -> (category: str, is_bug: bool)

Where `issue` is a dict with keys: number, title, labels, body.
"""

import re
from collections import Counter

# Categories used across all classifiers
CATEGORIES = [
    'Integrations', 'Inner Loop', 'Polyglot',
    'Deployment', 'Dashboard', 'Infra',
]

# Labels that are meta/process — excluded from theme analysis
SKIP_LABELS = frozenset({
    'needs-area-label', 'needs-retriage', 'needs-further-triage',
    'untriaged', 'needs-author-action', 'needs-design',
    'enhancement', 'bug', 'help wanted', 'good first issue',
    'tracking', 'blog-post', 'external', 'blocked',
    'aspirifriday', 'aspirifridays', 'hardproblems', 'refactoring',
})


def classify_issue(issue):
    """Classify an issue into a quadrant and bug/feature type.

    Args:
        issue: dict with keys number, title, labels (list of {name}), body.

    Returns:
        (category: str, is_bug: bool)
    """
    labels = {l['name'].lower() for l in issue.get('labels', [])}
    title = issue.get('title', '').lower()
    body = (issue.get('body') or '')[:500].lower()

    category = _classify_category(labels, title, body)
    is_bug = _classify_type(labels, title, body)
    return category, is_bug


def clean_label(name):
    """Strip emoji/unicode from label names for clean display."""
    return re.sub(r'[^\x20-\x7E]+', '', name).strip()


def get_top_themes(items, n=5):
    """Extract top theme labels from a list of classified issues.

    Args:
        items: list of dicts with 'labels' key.
        n: number of top themes to return.

    Returns:
        list of (label_name, count) tuples.
    """
    theme_counter = Counter()
    for c in items:
        for l in c['labels']:
            ln = l.lower()
            if ln not in SKIP_LABELS and not ln.startswith('area-'):
                cleaned = clean_label(l)
                if cleaned and cleaned.lower() not in SKIP_LABELS:
                    theme_counter[cleaned] += 1
    return theme_counter.most_common(n)


# ── Internal helpers ─────────────────────────────────────────────────────────

def _classify_category(labels, title, body):
    """Determine which quadrant an issue belongs to."""

    has_appmodel = bool(labels & {'area-app-model', 'area-orchestrator'})

    # Infra
    infra_labels = {
        'area-engineering-systems', 'area-testing', 'area-app-testing',
        'flaky-test', 'failing-test', 'disabled-tests', 'quarantined-test',
        'area-pipelines', 'area-samples', 'area-meta', 'area-copilot',
        'area-grafana', 'pod-e2e', 'perf',
    }
    if labels & infra_labels or any('testing' in l and l != 'area-app-testing' for l in labels):
        return 'Infra'

    # Deployment
    deploy_labels = {
        'area-deployment', 'deployment-e2e', 'azure-container-apps',
        'area-azure-aca', 'docker-compose', 'kubernetes',
        'azure-functions', 'azure.provisioning',
    }
    deploy_title_kw = [
        'deploy', 'publish', 'container app', 'docker compose',
        'kubernetes', 'k8s', 'helm', 'aca ', 'bicep',
        'manifest', 'terraform', 'azure container',
    ]
    if labels & deploy_labels:
        return 'Deployment'
    if has_appmodel and any(kw in title for kw in deploy_title_kw):
        return 'Deployment'

    # Dashboard
    dashboard_labels = {
        'area-dashboard', 'dashboard-filter-sort', 'dashboard-metrics',
        'dashboard-resource-details', 'dashboard-dashpages',
        'dashboard-logs', 'dashboard-resize', 'dashboard-persistence',
        'dashboard-settings', 'feature-dashboard-extensibility',
        'a11y', 'a11ysev3',
    }
    if labels & dashboard_labels:
        return 'Dashboard'
    if has_appmodel and 'dashboard' in title:
        return 'Dashboard'

    # Inner Loop
    inner_labels = {
        'area-cli', 'area-tooling', 'area-acquisition', 'area-extension',
        'ai', 'devtunnels', 'agentic-workflows', 'area-mcp',
        'area-aspire.dev', 'vs', 'wsl', 'interaction-service',
    }
    inner_title_kw = [
        'cli ', 'debugg', 'hot reload', 'inner loop', 'ai ',
        'copilot', 'agent', 'visual studio', 'vs code',
        'vscode', 'launch', 'f5 ', 'dotnet run', 'dotnet watch',
    ]
    if labels & inner_labels:
        return 'Inner Loop'
    if has_appmodel and any(kw in title for kw in inner_title_kw):
        return 'Inner Loop'

    # Integrations
    integration_labels = {
        'area-integrations', 'azure', 'azure-cosmosdb', 'azure-storage',
        'azure-servicebus', 'azure-eventhubs', 'azure-keyvault',
        'azure-signalr', 'azure-sqlserver', 'postgres', 'sqlserver',
        'redis', 'rabbitmq', 'kafka', 'mongodb', 'oracle', 'mysql',
        'nats', 'milvus', 'entityframework', 'keycloak', 'yarp',
        'orleans', 'blazor-wasm', 'podman',
    }
    integration_title_kw = [
        'integration', 'component', 'redis', 'postgres',
        'sql server', 'kafka', 'rabbitmq', 'mongo',
        'entity framework', 'ef core', 'mysql', 'oracle',
        'keycloak', 'yarp', 'orleans', 'nats', 'milvus',
    ]
    if labels & integration_labels:
        return 'Integrations'
    if has_appmodel and any(kw in title for kw in integration_title_kw):
        return 'Integrations'

    # Polyglot
    polyglot_labels = {
        'area-polyglot', 'area-templates',
        'javascript', 'python', 'area-service-discovery',
        'area-telemetry',
    }
    polyglot_title_kw = [
        'typescript', 'type system', 'polyglot',
        'python', 'node.js', 'nodejs', 'javascript',
        'js apphost', 'export', 'template',
        'service discovery', 'dns ', 'otel', 'opentelemetry',
    ]
    if labels & polyglot_labels:
        return 'Polyglot'
    if any(kw in title for kw in polyglot_title_kw):
        return 'Polyglot'

    # app-model/orchestrator fallback
    if has_appmodel:
        return 'Polyglot'

    # No area label: title keyword fallback
    if any(kw in title for kw in integration_title_kw + ['azure']):
        return 'Integrations'
    if any(kw in title for kw in deploy_title_kw):
        return 'Deployment'
    if any(kw in title for kw in inner_title_kw):
        return 'Inner Loop'
    if 'dashboard' in title:
        return 'Dashboard'

    return 'Uncategorized'


def _classify_type(labels, title, body):
    """Determine if an issue is a bug or feature request."""
    bug_labels = {
        'bug', 'silent-failure', 'flaky-test', 'failing-test',
        'disabled-tests', 'quarantined-test',
    }
    if any('regression' in l for l in labels):
        return True
    if labels & bug_labels:
        return True
    bug_kw = [
        'bug', 'crash', 'exception', 'error', 'fail', 'broken',
        "doesn't work", 'not working', 'regression', 'fix ',
    ]
    return any(kw in title for kw in bug_kw)
