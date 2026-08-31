#!/usr/bin/env python3
"""Render one or more JMH JSON result files as a self-contained HTML report.

Usage:
  python3 tools/render_report.py results/jmh-results.json [more.json ...] [-o results/report.html]

Each input file becomes one labeled "run" (label = file basename), so passing a
baseline JSON and a candidate JSON gives side-by-side bars per benchmark.
"""
import argparse
import html
import json
import math
import os
import sys

# Fixed categorical slot order (validated palette; see dataviz notes). Color follows
# the impl, never its position in a particular chart.
LIGHT_SERIES = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#4a3aa7", "#e34948"]
DARK_SERIES = ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"]
IMPL_ORDER = ["K1V1", "K2V2", "K4V4", "FASTUTIL"]

PLOT_H = 220
M_TOP, M_BOT, M_LEFT, M_RIGHT = 16, 34, 64, 12
BAR_W, BAR_GAP, GROUP_PAD = 34, 2, 28


def load_runs(paths):
    runs = []
    for p in paths:
        with open(p) as f:
            entries = json.load(f)
        label = os.path.splitext(os.path.basename(p))[0]
        runs.append((label, entries))
    return runs


def short_name(entry):
    return entry["benchmark"].rsplit(".", 1)[-1]


def collect(runs):
    """-> benchmarks (ordered), impls (ordered), data[(bench, run_label, impl)] = entry"""
    benches, impls, data = [], [], {}
    for label, entries in runs:
        for e in entries:
            b = short_name(e)
            impl = e.get("params", {}).get("impl", "?")
            if b not in benches:
                benches.append(b)
            if impl not in impls:
                impls.append(impl)
            data[(b, label, impl)] = e
    impls.sort(key=lambda i: (IMPL_ORDER.index(i) if i in IMPL_ORDER else 99, i))
    return benches, impls, data


def nice_ticks(vmax, n=5):
    if vmax <= 0:
        return [0, 1]
    raw = vmax / n
    mag = 10 ** math.floor(math.log10(raw))
    for mult in (1, 2, 2.5, 5, 10):
        step = mag * mult
        if step >= raw:
            break
    top = step * math.ceil(vmax / step)
    ticks, t = [], 0.0
    while t <= top + 1e-9:
        ticks.append(round(t, 10))
        t += step
    return ticks


def fmt(v):
    if v == int(v) and v < 1e6:
        return f"{int(v):,}"
    if v >= 100:
        return f"{v:,.0f}"
    if v >= 10:
        return f"{v:.1f}"
    return f"{v:.2f}"


def panel_svg(bench, run_labels, impls, data, unit):
    cells = []
    vmax = 0.0
    for r in run_labels:
        for i in impls:
            e = data.get((bench, r, i))
            if e is None:
                continue
            m = e["primaryMetric"]
            err = m.get("scoreError")
            err = 0.0 if err is None or (isinstance(err, str)) or math.isnan(err) else err
            vmax = max(vmax, m["score"] + err)
    ticks = nice_ticks(vmax)
    top = ticks[-1]
    group_w = len(impls) * BAR_W + (len(impls) - 1) * BAR_GAP
    width = M_LEFT + M_RIGHT + len(run_labels) * (group_w + GROUP_PAD) - GROUP_PAD + 8
    height = M_TOP + PLOT_H + M_BOT

    def y(v):
        return M_TOP + PLOT_H * (1 - v / top)

    parts = [f'<svg viewBox="0 0 {width} {height}" role="img" '
             f'aria-label="{html.escape(bench)} results, {html.escape(unit)}">']
    for t in ticks:
        yy = y(t)
        parts.append(f'<line x1="{M_LEFT}" y1="{yy:.1f}" x2="{width - M_RIGHT}" y2="{yy:.1f}" '
                     f'class="{"axisline" if t == 0 else "gridline"}"/>')
        parts.append(f'<text x="{M_LEFT - 8}" y="{yy + 4:.1f}" class="ticklabel" text-anchor="end">{fmt(t)}</text>')
    gx = M_LEFT + 4
    for r in run_labels:
        for k, i in enumerate(impls):
            e = data.get((bench, r, i))
            x = gx + k * (BAR_W + BAR_GAP)
            if e is not None:
                m = e["primaryMetric"]
                score = m["score"]
                err = m.get("scoreError") or 0.0
                if isinstance(err, str) or math.isnan(err):
                    err = 0.0
                by = y(score)
                bh = max(M_TOP + PLOT_H - by, 1.5)
                size = int(e.get("params", {}).get("size", "0") or 0)
                nsop = score * 1e6 / size if size else None
                tip = (f"{i} — {bench} ({r})&#10;{fmt(score)} ± {fmt(err)} {unit}"
                       + (f"&#10;≈ {fmt(nsop)} ns per operation" if nsop else ""))
                parts.append(
                    f'<path class="bar s{impls.index(i)}" d="M{x},{M_TOP + PLOT_H} v-{bh - 4:.1f} '
                    f'q0,-4 4,-4 h{BAR_W - 8} q4,0 4,4 v{bh - 4:.1f} z"/>')
                if err > 0:
                    cx = x + BAR_W / 2
                    e0, e1 = y(min(score + err, top)), y(max(score - err, 0))
                    parts.append(f'<g class="whisker"><line x1="{cx}" y1="{e0:.1f}" x2="{cx}" y2="{e1:.1f}"/>'
                                 f'<line x1="{cx - 5}" y1="{e0:.1f}" x2="{cx + 5}" y2="{e0:.1f}"/>'
                                 f'<line x1="{cx - 5}" y1="{e1:.1f}" x2="{cx + 5}" y2="{e1:.1f}"/></g>')
                parts.append(f'<rect class="hit" x="{x - 1}" y="{M_TOP}" width="{BAR_W + 2}" height="{PLOT_H}" '
                             f'data-tip="{tip}"/>')
        if len(run_labels) > 1:
            parts.append(f'<text x="{gx + group_w / 2}" y="{M_TOP + PLOT_H + 18}" class="ticklabel" '
                         f'text-anchor="middle">{html.escape(r)}</text>')
        gx += group_w + GROUP_PAD
    parts.append(f'<text x="{M_LEFT}" y="{M_TOP + PLOT_H + (30 if len(run_labels) > 1 else 20)}" '
                 f'class="unitlabel">{html.escape(unit)}</text>')
    parts.append("</svg>")
    return "".join(parts)


def build_table(benches, run_labels, impls, data):
    rows = []
    for b in benches:
        for r in run_labels:
            for i in impls:
                e = data.get((b, r, i))
                if e is None:
                    continue
                m = e["primaryMetric"]
                err = m.get("scoreError") or 0.0
                if isinstance(err, str) or math.isnan(err):
                    err = 0.0
                size = int(e.get("params", {}).get("size", "0") or 0)
                nsop = fmt(m["score"] * 1e6 / size) if size else "—"
                rows.append(f"<tr><td>{html.escape(b)}</td><td>{html.escape(r)}</td><td>{html.escape(i)}</td>"
                            f'<td class="num">{fmt(m["score"])}</td><td class="num">± {fmt(err)}</td>'
                            f'<td class="num">{nsop}</td></tr>')
    return "\n".join(rows)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("inputs", nargs="+")
    ap.add_argument("-o", "--output", default="results/report.html")
    args = ap.parse_args()

    runs = load_runs(args.inputs)
    run_labels = [r[0] for r in runs]
    benches, impls, data = collect(runs)
    first = runs[0][1][0]
    unit = first["primaryMetric"]["scoreUnit"]
    meta = (f'JDK {first.get("jdkVersion", "?")} · {first.get("vmName", "?")} · '
            f'{first.get("forks", "?")} fork(s), {first.get("warmupIterations", "?")}×{first.get("warmupTime", "?")} warmup, '
            f'{first.get("measurementIterations", "?")}×{first.get("measurementTime", "?")} measurement · '
            f'size={first.get("params", {}).get("size", "?")}')

    light_css = "".join(f".s{k}{{fill:{LIGHT_SERIES[k % 8]}}} .chip{k}{{background:{LIGHT_SERIES[k % 8]}}}"
                        for k in range(len(impls)))
    dark_css = "".join(f".s{k}{{fill:{DARK_SERIES[k % 8]}}} .chip{k}{{background:{DARK_SERIES[k % 8]}}}"
                       for k in range(len(impls)))
    legend = "".join(f'<span class="key"><span class="chip chip{k}"></span>{html.escape(i)}'
                     f'{" (baseline)" if i == "FASTUTIL" else ""}</span>'
                     for k, i in enumerate(impls))
    panels = "".join(f'<figure class="panel"><figcaption>{html.escape(b)}</figcaption>'
                     f'{panel_svg(b, run_labels, impls, data, unit)}</figure>'
                     for b in benches)

    page = f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>HashMapLockFreeKnVn — JMH results</title>
<style>
  :root {{ color-scheme: light dark; }}
  .viz-root {{
    --surface-1:#fcfcfb; --page:#f9f9f7; --ink:#0b0b0b; --ink-2:#52514e; --muted:#898781;
    --grid:#e1e0d9; --axis:#c3c2b7; --border:rgba(11,11,11,.10);
    font: 14px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif;
    background: var(--page); color: var(--ink); min-height: 100vh; margin: 0;
    padding: 24px; box-sizing: border-box;
  }}
  .viz-root {{ {light_css} }}
  @media (prefers-color-scheme: dark) {{
    .viz-root {{
      --surface-1:#1a1a19; --page:#0d0d0d; --ink:#ffffff; --ink-2:#c3c2b7; --muted:#898781;
      --grid:#2c2c2a; --axis:#383835; --border:rgba(255,255,255,.10);
      {dark_css}
    }}
  }}
  h1 {{ font-size: 20px; margin: 0 0 4px; }}
  .meta {{ color: var(--ink-2); margin: 0 0 16px; font-size: 13px; }}
  .legend {{ display: flex; gap: 16px; flex-wrap: wrap; margin-bottom: 16px; }}
  .key {{ display: inline-flex; align-items: center; gap: 6px; color: var(--ink-2); }}
  .chip {{ width: 12px; height: 12px; border-radius: 3px; display: inline-block; }}
  .grid2 {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 16px; }}
  .panel {{ background: var(--surface-1); border: 1px solid var(--border); border-radius: 10px;
           padding: 14px 16px 8px; margin: 0; overflow-x: auto; }}
  .panel figcaption {{ font-weight: 600; margin-bottom: 6px; }}
  .panel svg {{ width: 100%; height: auto; display: block; }}
  .gridline {{ stroke: var(--grid); stroke-width: 1; }}
  .axisline {{ stroke: var(--axis); stroke-width: 1; }}
  .ticklabel {{ fill: var(--muted); font-size: 11px; font-variant-numeric: tabular-nums; }}
  .unitlabel {{ fill: var(--muted); font-size: 11px; }}
  .whisker line {{ stroke: var(--ink-2); stroke-width: 1.5; }}
  .hit {{ fill: transparent; }}
  .hit:hover {{ fill: rgba(128,128,128,.08); }}
  #tip {{ position: fixed; pointer-events: none; background: var(--surface-1); color: var(--ink);
         border: 1px solid var(--border); border-radius: 8px; padding: 8px 10px; font-size: 12px;
         white-space: pre-line; box-shadow: 0 4px 14px rgba(0,0,0,.18); display: none; z-index: 10;
         font-variant-numeric: tabular-nums; }}
  details {{ margin-top: 20px; }}
  summary {{ cursor: pointer; color: var(--ink-2); }}
  table {{ border-collapse: collapse; margin-top: 10px; background: var(--surface-1);
          border: 1px solid var(--border); border-radius: 10px; }}
  th, td {{ padding: 6px 14px; text-align: left; border-bottom: 1px solid var(--grid); }}
  td.num {{ text-align: right; font-variant-numeric: tabular-nums; }}
  th {{ color: var(--ink-2); font-weight: 600; }}
</style></head>
<body class="viz-root">
<h1>HashMapLockFreeKnVn — JMH results</h1>
<p class="meta">{html.escape(meta)} · runs: {html.escape(", ".join(run_labels))}<br>Lower is better · whiskers are JMH 99.9% confidence intervals · hover a bar for exact numbers</p>
<div class="legend">{legend}</div>
<div class="grid2">{panels}</div>
<details><summary>Table view (exact numbers)</summary>
<table><thead><tr><th>Benchmark</th><th>Run</th><th>Impl</th>
<th>Score ({html.escape(unit)})</th><th>99.9% CI</th><th>ns/operation</th></tr></thead>
<tbody>{build_table(benches, run_labels, impls, data)}</tbody></table></details>
<div id="tip"></div>
<script>
  const tip = document.getElementById('tip');
  document.querySelectorAll('.hit').forEach(el => {{
    el.addEventListener('mousemove', ev => {{
      tip.textContent = el.dataset.tip;
      tip.style.display = 'block';
      const pad = 14, r = tip.getBoundingClientRect();
      tip.style.left = Math.min(ev.clientX + pad, innerWidth - r.width - 8) + 'px';
      tip.style.top = Math.min(ev.clientY + pad, innerHeight - r.height - 8) + 'px';
    }});
    el.addEventListener('mouseleave', () => tip.style.display = 'none');
  }});
</script>
</body></html>"""
    os.makedirs(os.path.dirname(args.output) or ".", exist_ok=True)
    with open(args.output, "w") as f:
        f.write(page)
    print(f"wrote {args.output} ({len(benches)} benchmarks, {len(run_labels)} run(s), {len(impls)} impls)")


if __name__ == "__main__":
    sys.exit(main())
