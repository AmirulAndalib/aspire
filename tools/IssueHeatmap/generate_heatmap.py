#!/usr/bin/env python3
"""
Aspire Issue Heatmap Generator

Fetches all open issues from dotnet/aspire, classifies them, and generates
a heatmap visualization + CSV export.

Classification is pluggable: by default uses the heuristic classifier in
classify.py. To use pre-computed classifications (e.g. from an LLM), pass
--classifications path/to/classifications.json containing:

    [{"n": 12345, "c": "Integrations", "y": "bug"}, ...]

Usage:
    # Heuristic classification (fast, no AI)
    python generate_heatmap.py

    # Pre-computed LLM classifications
    python generate_heatmap.py --classifications output/classifications.json

Prerequisites:
    - gh CLI authenticated with repo access
    - pip install matplotlib numpy
"""

import argparse
import csv
import json
import os
import subprocess
import sys
from collections import Counter
from datetime import datetime, timezone

from classify import CATEGORIES, SKIP_LABELS, clean_label, classify_issue, get_top_themes


# ── Data fetching ────────────────────────────────────────────────────────────

def fetch_issues(repo):
    """Fetch all open issues from GitHub using gh CLI."""
    print(f"Fetching open issues from {repo}...")
    result = subprocess.run(
        ['gh', 'issue', 'list', '--repo', repo, '--state', 'open',
         '--limit', '5000', '--json', 'number,title,labels,createdAt,body'],
        capture_output=True, text=True, encoding='utf-8',
    )
    if result.returncode != 0:
        print(f"Error fetching issues: {result.stderr}", file=sys.stderr)
        sys.exit(1)

    issues = json.loads(result.stdout)
    print(f"  Fetched {len(issues)} open issues")
    return issues


# ── Stats aggregation ────────────────────────────────────────────────────────

CAT_COLORS = {
    'Integrations': '#58a6ff',
    'Inner Loop': '#f0883e',
    'Polyglot': '#a371f7',
    'Deployment': '#3fb950',
    'Dashboard': '#f778ba',
    'Infra': '#8b949e',
}


def compute_stats(categorized):
    """Compute aggregate statistics per category."""
    stats = {}
    for cat in CATEGORIES:
        items = [c for c in categorized if c['category'] == cat]
        bugs = [c for c in items if c['is_bug']]
        features = [c for c in items if not c['is_bug']]
        ages = [c['age_days'] for c in items]
        median_age = sorted(ages)[len(ages) // 2] if ages else 0
        avg_age = sum(ages) / len(ages) if ages else 0

        stats[cat] = {
            'total': len(items),
            'bugs': len(bugs),
            'features': len(features),
            'median_age': median_age,
            'avg_age': avg_age,
            'top_themes': get_top_themes(items),
        }
    return stats


def print_summary(stats, total):
    """Print a text summary to stdout."""
    print(f"\n{'=' * 90}")
    print(f"  ASPIRE OPEN ISSUES HEATMAP  —  {total} issues")
    print(f"{'=' * 90}\n")

    for cat in CATEGORIES:
        s = stats[cat]
        pct = s['total'] / total * 100
        bug_pct = s['bugs'] / s['total'] * 100 if s['total'] > 0 else 0
        print(f"  {cat:15s}  |  {s['total']:4d} ({pct:4.1f}%)  |  "
              f"Bugs: {s['bugs']:3d} ({bug_pct:.0f}%)  Features: {s['features']:3d}  |  "
              f"Median: {s['median_age']:3d}d  Avg: {s['avg_age']:.0f}d")
        if s['top_themes']:
            themes_str = ', '.join(f"{n}({c})" for n, c in s['top_themes'])
            print(f"  {'':15s}  |  Themes: {themes_str}")
        print()


# ── Visualization ────────────────────────────────────────────────────────────

def generate_heatmap(stats, total, date_str, output_dir):
    """Generate the 6-panel heatmap PNG."""
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    import matplotlib.gridspec as gridspec
    from matplotlib.colors import LinearSegmentedColormap
    import numpy as np

    fig = plt.figure(figsize=(26, 17), facecolor='#0d1117')
    gs = gridspec.GridSpec(2, 3, height_ratios=[1.2, 1], hspace=0.4, wspace=0.4,
                           left=0.06, right=0.96, top=0.90, bottom=0.06)

    text_color = '#c9d1d9'
    grid_color = '#30363d'
    display_cats = CATEGORIES

    # Panel 1: Count heatmap
    ax1 = fig.add_subplot(gs[0, 0])
    data = np.array([[stats[c]['features'], stats[c]['bugs']] for c in display_cats])
    cmap = LinearSegmentedColormap.from_list(
        'aspire', ['#161b22', '#1f6feb', '#58a6ff', '#f0883e', '#da3633'], N=256)
    im = ax1.imshow(data, cmap=cmap, aspect='auto', vmin=0)
    ax1.set_xticks([0, 1])
    ax1.set_xticklabels(['Features', 'Bugs'], fontsize=13, color=text_color, fontweight='bold')
    ax1.set_yticks(range(len(display_cats)))
    ax1.set_yticklabels(display_cats, fontsize=13, color=text_color, fontweight='bold')
    ax1.set_title('Issue Count: Quadrant x Type', fontsize=15, color='white',
                  fontweight='bold', pad=15)
    for i in range(len(display_cats)):
        for j in range(2):
            val = data[i, j]
            ax1.text(j, i, f'{val}', ha='center', va='center', fontsize=16,
                     fontweight='bold', color='white' if val > 80 else text_color)
    cbar = plt.colorbar(im, ax=ax1, fraction=0.03, pad=0.04)
    cbar.ax.tick_params(colors=text_color)
    ax1.tick_params(colors=text_color)
    for spine in ax1.spines.values():
        spine.set_color(grid_color)

    # Panel 2: Stacked bar
    ax2 = fig.add_subplot(gs[0, 1])
    y_pos = np.arange(len(display_cats))
    feat_data = [stats[c]['features'] for c in display_cats]
    bugs_data = [stats[c]['bugs'] for c in display_cats]
    ax2.barh(y_pos, feat_data, color='#1f6feb', label='Features', height=0.6)
    ax2.barh(y_pos, bugs_data, left=feat_data, color='#da3633', label='Bugs', height=0.6)
    ax2.set_yticks(y_pos)
    ax2.set_yticklabels(display_cats, fontsize=12, color=text_color, fontweight='bold')
    ax2.set_xlabel('Issue Count', fontsize=12, color=text_color)
    ax2.set_title('Features vs Bugs by Quadrant', fontsize=15, color='white',
                  fontweight='bold', pad=15)
    ax2.legend(loc='lower right', fontsize=11, facecolor='#161b22',
               edgecolor=grid_color, labelcolor=text_color)
    ax2.set_facecolor('#0d1117')
    ax2.tick_params(colors=text_color)
    ax2.xaxis.grid(True, color=grid_color, alpha=0.5)
    for spine in ax2.spines.values():
        spine.set_color(grid_color)
    for i, (f, b) in enumerate(zip(feat_data, bugs_data)):
        ax2.text(f + b + 5, i, f'{f + b}', va='center', fontsize=12,
                 color=text_color, fontweight='bold')

    # Panel 3: Donut chart
    ax3 = fig.add_subplot(gs[0, 2])
    sizes = [stats[c]['total'] for c in display_cats]
    colors_list = [CAT_COLORS[c] for c in display_cats]
    wedges, texts, autotexts = ax3.pie(
        sizes, labels=display_cats, colors=colors_list,
        autopct='%1.0f%%', startangle=90, pctdistance=0.8,
        wedgeprops=dict(width=0.4, edgecolor='#0d1117', linewidth=2))
    for t in texts:
        t.set_color(text_color)
        t.set_fontsize(11)
        t.set_fontweight('bold')
    for t in autotexts:
        t.set_color('white')
        t.set_fontsize(10)
        t.set_fontweight('bold')
    ax3.set_title('Share of Open Issues', fontsize=15, color='white',
                  fontweight='bold', pad=15)

    # Panel 4: Age heatmap
    ax4 = fig.add_subplot(gs[1, 0])
    age_data = np.array([[stats[c]['median_age'], stats[c]['avg_age']] for c in display_cats])
    cmap_age = LinearSegmentedColormap.from_list('age', ['#238636', '#f0883e', '#da3633'], N=256)
    im3 = ax4.imshow(age_data, cmap=cmap_age, aspect='auto', vmin=0)
    ax4.set_xticks([0, 1])
    ax4.set_xticklabels(['Median Age (days)', 'Avg Age (days)'],
                        fontsize=12, color=text_color, fontweight='bold')
    ax4.set_yticks(range(len(display_cats)))
    ax4.set_yticklabels(display_cats, fontsize=12, color=text_color, fontweight='bold')
    ax4.set_title('Issue Age by Quadrant', fontsize=15, color='white',
                  fontweight='bold', pad=15)
    for i in range(len(display_cats)):
        for j in range(2):
            val = age_data[i, j]
            ax4.text(j, i, f'{val:.0f}d', ha='center', va='center', fontsize=14,
                     fontweight='bold', color='white')
    cbar3 = plt.colorbar(im3, ax=ax4, fraction=0.03, pad=0.04)
    cbar3.ax.tick_params(colors=text_color)
    cbar3.set_label('Days', color=text_color, fontsize=10)
    ax4.tick_params(colors=text_color)
    for spine in ax4.spines.values():
        spine.set_color(grid_color)

    # Panel 5: Bug ratio
    ax5 = fig.add_subplot(gs[1, 1])
    bug_pcts = [stats[c]['bugs'] / stats[c]['total'] * 100
                if stats[c]['total'] > 0 else 0 for c in display_cats]
    bar_colors = [CAT_COLORS[c] for c in display_cats]
    ax5.barh(y_pos, bug_pcts, color=bar_colors, height=0.6,
             edgecolor='#0d1117', linewidth=1)
    ax5.set_yticks(y_pos)
    ax5.set_yticklabels(display_cats, fontsize=12, color=text_color, fontweight='bold')
    ax5.set_xlabel('Bug %', fontsize=12, color=text_color)
    ax5.set_title('Bug Ratio by Quadrant', fontsize=15, color='white',
                  fontweight='bold', pad=15)
    ax5.set_xlim(0, 100)
    ax5.set_facecolor('#0d1117')
    ax5.tick_params(colors=text_color)
    ax5.xaxis.grid(True, color=grid_color, alpha=0.5)
    for spine in ax5.spines.values():
        spine.set_color(grid_color)
    ax5.axvline(x=50, color='#da3633', linestyle='--', alpha=0.5, linewidth=1)
    for i, pct in enumerate(bug_pcts):
        ax5.text(pct + 2, i, f'{pct:.0f}%', va='center', fontsize=12,
                 color=text_color, fontweight='bold')

    # Panel 6: Key themes
    ax6 = fig.add_subplot(gs[1, 2])
    ax6.axis('off')
    ax6.set_title('Key Themes per Quadrant', fontsize=15, color='white',
                  fontweight='bold', pad=15)
    y_start = 0.97
    for i, cat in enumerate(display_cats):
        s = stats[cat]
        y = y_start - i * 0.16
        ax6.text(0.02, y, cat, fontsize=14, color=CAT_COLORS[cat], fontweight='bold',
                 transform=ax6.transAxes, va='top')
        bug_pct_val = s['bugs'] * 100 // s['total'] if s['total'] else 0
        ax6.text(0.02, y - 0.035,
                 f'{s["total"]} issues  |  {s["bugs"]} bugs ({bug_pct_val}%)'
                 f'  |  median {s["median_age"]}d',
                 fontsize=10, color='#8b949e', transform=ax6.transAxes, va='top',
                 fontfamily='monospace')
        if s['top_themes']:
            themes = ',  '.join(f'{n} ({c})' for n, c in s['top_themes'][:4])
            ax6.text(0.02, y - 0.07, themes, fontsize=9, color=text_color,
                     transform=ax6.transAxes, va='top')

    fig.suptitle(
        f'Aspire Open Issues Heatmap  --  {total} issues  --  {date_str}',
        fontsize=20, color='white', fontweight='bold', y=0.97)

    out_path = os.path.join(output_dir, f'aspire_heatmap_{date_str}.png')
    plt.savefig(out_path, dpi=150, facecolor='#0d1117')
    plt.close()
    print(f"  Saved heatmap: {out_path}")
    return out_path


# ── CSV export ───────────────────────────────────────────────────────────────

def export_csv(categorized, issues_by_num, date_str, output_dir):
    """Export classification data to CSV."""
    out_path = os.path.join(output_dir, f'aspire_issues_{date_str}.csv')
    with open(out_path, 'w', newline='', encoding='utf-8-sig') as f:
        writer = csv.DictWriter(f, fieldnames=[
            'number', 'url', 'title', 'quadrant', 'type',
            'age_days', 'created', 'labels',
        ])
        writer.writeheader()
        for c in categorized:
            issue = issues_by_num.get(c['number'], {})
            labels = '; '.join(l['name'] for l in issue.get('labels', []))
            writer.writerow({
                'number': c['number'],
                'url': f'https://github.com/dotnet/aspire/issues/{c["number"]}',
                'title': c['title'],
                'quadrant': c['category'],
                'type': 'bug' if c['is_bug'] else 'feature',
                'age_days': c['age_days'],
                'created': issue.get('createdAt', '')[:10],
                'labels': labels,
            })
    print(f"  Saved CSV: {out_path}")
    return out_path


# ── Main ─────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description='Generate Aspire issue heatmap')
    parser.add_argument('--repo', default='dotnet/aspire',
                        help='GitHub repo (default: dotnet/aspire)')
    parser.add_argument('--output', default=None,
                        help='Output directory (default: tools/IssueHeatmap/output)')
    parser.add_argument('--classifications', default=None,
                        help='Path to pre-computed classifications JSON '
                             '(e.g. from LLM). Format: [{"n": 123, "c": "Integrations", "y": "bug"}, ...]')
    args = parser.parse_args()

    # Resolve output dir
    if args.output:
        output_dir = args.output
    else:
        script_dir = os.path.dirname(os.path.abspath(__file__))
        output_dir = os.path.join(script_dir, 'output')
    os.makedirs(output_dir, exist_ok=True)

    now = datetime.now(timezone.utc)
    date_str = now.strftime('%Y-%m-%d')

    # Fetch issues (always needed for dates, labels, titles)
    issues = fetch_issues(args.repo)
    issues_by_num = {i['number']: i for i in issues}

    # Classify — either from pre-computed file or heuristics
    if args.classifications:
        print(f"Loading pre-computed classifications from {args.classifications}...")
        with open(args.classifications, encoding='utf-8') as f:
            raw = json.load(f)
        cl_map = {c['n']: c for c in raw}
        categorized = []
        for issue in issues:
            cl = cl_map.get(issue['number'])
            if cl:
                cat = cl['c']
                is_bug = cl['y'] == 'bug'
            else:
                # Fall back to heuristic for issues not in the file
                cat, is_bug = classify_issue(issue)
            created = datetime.fromisoformat(
                issue['createdAt'].replace('Z', '+00:00'))
            age_days = (now - created).days
            labels = [l['name'] for l in issue.get('labels', [])]
            categorized.append({
                'number': issue['number'],
                'title': issue['title'],
                'category': cat,
                'is_bug': is_bug,
                'age_days': age_days,
                'labels': labels,
            })
        print(f"  Loaded {len(cl_map)} classifications "
              f"({len(categorized) - len(cl_map)} fell back to heuristic)")
    else:
        print("Classifying issues with heuristics...")
        categorized = []
        for issue in issues:
            cat, is_bug = classify_issue(issue)
            created = datetime.fromisoformat(
                issue['createdAt'].replace('Z', '+00:00'))
            age_days = (now - created).days
            labels = [l['name'] for l in issue.get('labels', [])]
            categorized.append({
                'number': issue['number'],
                'title': issue['title'],
                'category': cat,
                'is_bug': is_bug,
                'age_days': age_days,
                'labels': labels,
            })

    total = len(categorized)
    stats = compute_stats(categorized)

    # Output
    print_summary(stats, total)
    generate_heatmap(stats, total, date_str, output_dir)
    export_csv(categorized, issues_by_num, date_str, output_dir)

    print(f"\nDone! {total} issues classified into {len(CATEGORIES)} quadrants.")


if __name__ == '__main__':
    main()
