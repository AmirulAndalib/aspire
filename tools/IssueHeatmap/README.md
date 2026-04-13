# Issue Heatmap Tool

Generates a visual heatmap of all open issues in the `dotnet/aspire` repository, categorized by feature quadrant.

## Quadrants

| Quadrant | What it covers |
|---|---|
| **Integrations** | Technology integrations — Redis, PostgreSQL, SQL Server, MongoDB, Kafka, Azure services (CosmosDB, Storage, ServiceBus, etc.), EF Core, YARP, Orleans, container runtimes |
| **Inner Loop** | Developer experience — CLI, tooling, IDE integration, debugging, hot reload, dev tunnels, AI-assisted dev, MCP, acquisition/installation |
| **Polyglot** | App model & type system — resource lifecycle, endpoints, volumes, parameters, secrets, service discovery, health checks, templates, telemetry/OTel, multi-language support (Python/Node.js/TypeScript app hosts), exports |
| **Deployment** | Publishing & cloud — Azure Container Apps, Kubernetes, Docker Compose, Azure Functions, Bicep, Terraform, Azure provisioning |
| **Dashboard** | Dashboard UI — resource views, logs/traces/metrics viewers, filtering, accessibility, settings |
| **Infra** | Engineering systems — CI/CD, test infrastructure, flaky/failing tests, build tooling, samples |

## Prerequisites

- Python 3.10+
- `gh` CLI (authenticated with access to `dotnet/aspire`)
- Python packages: `pip install matplotlib seaborn numpy`

## Usage

```bash
# Heuristic classification (fast, no AI needed):
python tools/IssueHeatmap/generate_heatmap.py

# With pre-computed LLM classifications:
python tools/IssueHeatmap/generate_heatmap.py --classifications output/classifications.json

# Custom output directory:
python tools/IssueHeatmap/generate_heatmap.py --output /tmp/heatmap
```

This will:
1. Fetch all open issues via `gh issue list`
2. Classify each issue (heuristic or pre-computed)
3. Generate a heatmap PNG and a CSV with all classifications

Output files are saved to `tools/IssueHeatmap/output/` with the current date.

## File Structure

```
tools/IssueHeatmap/
├── README.md                  # This file
├── classify.py                # Heuristic classifier (swappable)
├── generate_heatmap.py        # Main script: fetch, visualize, export
└── output/                    # Generated artifacts (date-stamped)
```

### classify.py

Contains the heuristic classification logic. The key function is:

```python
classify_issue(issue) -> (category: str, is_bug: bool)
```

This can be swapped for LLM-based classification by producing a JSON file:

```json
[{"n": 12345, "c": "Integrations", "y": "bug"}, ...]
```

And passing it via `--classifications`.

## AI-Enhanced Classification

For higher accuracy (recommended for periodic reports), use the Copilot CLI:

```
Ask Copilot: "Generate an issue heatmap for aspire using AI classification"
```

This classifies each issue individually using an LLM rather than heuristics,
producing more accurate results especially for ambiguous issues. The LLM
output can be saved as a classifications JSON and passed to the script.

## Output

- `aspire_heatmap_YYYY-MM-DD.png` — 6-panel visualization with count heatmap, stacked bars, donut chart, age heatmap, bug ratio, and key themes
- `aspire_issues_YYYY-MM-DD.csv` — Full classification data (number, url, title, quadrant, type, age_days, created, labels)
