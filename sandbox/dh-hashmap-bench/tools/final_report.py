#!/usr/bin/env python3
"""Assemble the final narrative report: the HashMapLockFree benchmarking campaign, centered on the
redirection-index workload (hit-only gets, pulsed vs random keys, 2GB table, load factors 0.5-0.95).

Usage: python3 tools/final_report.py  (expects results/final-hit-lf*.json; final-get.json for the miss appendix)
"""
import html
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from render_report import panel_svg, fmt, IMPL_ORDER, LIGHT_SERIES, DARK_SERIES

LF_FILES = [("0.5", "results/final-hit-lf0.5.json"), ("0.75", "results/final-hit-lf0.75.json"),
            ("0.9", "results/final-hit-lf0.9.json"), ("0.95", "results/final-hit-lf0.95.json")]
MISS_FILE = "results/final-get.json"
# 7 series for the charts (palette holds 8); the full 11-impl numbers appear in the appendix table.
CHART_IMPLS = ["K1V1", "K4V4", "FASTUTIL", "NOMOD_K1V1", "NOMOD_K4V4", "AMAC_K2V2", "AMAC_K4V4",
               "AMAC_K4V4_BB"]


def load():
    """-> (run_labels, impls_all, data[(pseudo_bench, run_label, impl)] = entry) for getHit across lf files."""
    data, run_labels, impls_all = {}, [], []
    for lf, path in LF_FILES:
        if not os.path.exists(path):
            continue
        label = f"lf {lf}"
        run_labels.append(label)
        for e in json.load(open(path)):
            if not e["benchmark"].endswith("getHit"):
                continue
            impl = e["params"]["impl"]
            dist = e["params"].get("keyDist", "random")
            if impl not in impls_all:
                impls_all.append(impl)
            data[(f"getHit — {dist} keys", label, impl)] = e
    impls_all.sort(key=lambda i: (IMPL_ORDER.index(i) if i in IMPL_ORDER else 99, i))
    return run_labels, impls_all, data


def load_miss():
    data, impls = {}, []
    if not os.path.exists(MISS_FILE):
        return [], {}
    for e in json.load(open(MISS_FILE)):
        if not e["benchmark"].endswith("getMiss"):
            continue
        impl = e["params"]["impl"]
        dist = e["params"].get("keyDist", "random")
        if impl not in impls:
            impls.append(impl)
        data[(f"getMiss — {dist} keys", "lf 0.5", impl)] = e
    impls.sort(key=lambda i: (IMPL_ORDER.index(i) if i in IMPL_ORDER else 99, i))
    return impls, data


def score(data, bench, run, impl):
    e = data.get((bench, run, impl))
    return e["primaryMetric"]["score"] if e else None


def full_table(run_labels, impls, data, benches):
    rows = []
    for b in benches:
        for r in run_labels:
            for i in impls:
                e = data.get((b, r, i))
                if e is None:
                    continue
                m = e["primaryMetric"]
                err = m.get("scoreError") or 0.0
                if isinstance(err, str) or (isinstance(err, float) and math.isnan(err)):
                    err = 0.0
                nsop = m["score"] * 1e6 / int(e["params"].get("lookups", "0") or 1)
                rows.append(f"<tr><td>{html.escape(b)}</td><td>{html.escape(r)}</td><td>{html.escape(i)}</td>"
                            f'<td class="num">{fmt(m["score"])}</td><td class="num">± {fmt(err)}</td>'
                            f'<td class="num">{fmt(nsop)}</td></tr>')
    return "\n".join(rows)


def values_table(bench, run_labels, impls, data):
    head = "".join(f"<th>{html.escape(r)}</th>" for r in run_labels)
    rows = []
    for i in impls:
        cells = []
        for r in run_labels:
            e = data.get((bench, r, i))
            if e is None:
                cells.append("<td class='num'>—</td>")
                continue
            m = e["primaryMetric"]
            err = m.get("scoreError") or 0.0
            if isinstance(err, str) or (isinstance(err, float) and math.isnan(err)):
                err = 0.0
            cells.append(f"<td class='num'>{fmt(m['score'])} <span class='sub'>± {fmt(err)}</span></td>")
        rows.append(f"<tr><td>{html.escape(i)}</td>{''.join(cells)}</tr>")
    return (f"<table><thead><tr><th>{html.escape(bench)}</th>{head}</tr></thead>"
            f"<tbody>{''.join(rows)}</tbody></table>")


def main():
    run_labels, impls_all, data = load()
    if not run_labels:
        sys.exit("no final-hit-lf*.json files found")
    chart_impls = [i for i in CHART_IMPLS if i in impls_all]
    benches = ["getHit — pulsed keys", "getHit — random keys"]

    def chart_table(bench):
        return values_table(bench, run_labels, chart_impls, data)

    panels = "".join(
        f'<figure class="panel"><figcaption>{html.escape(b)} <span class="sub">1M lookups, ~2GB table, '
        f'occupancy = load factor</span></figcaption>'
        f'{panel_svg(b, run_labels, chart_impls, data, "ms per 1M lookups")}</figure>'
        f'{chart_table(b)}'
        for b in benches)

    miss_impls, miss_data = load_miss()
    miss_panels = ""
    if miss_data:
        miss_chart = [i for i in CHART_IMPLS if i in miss_impls]
        miss_panels = "".join(
            f'<figure class="panel"><figcaption>{html.escape(b)} <span class="sub">appendix: misses cannot occur '
            f'in a redirection index</span></figcaption>'
            f'{panel_svg(b, ["lf 0.5"], miss_chart, miss_data, "ms per 1M lookups")}</figure>'
            f'{values_table(b, ["lf 0.5"], miss_chart, miss_data)}'
            for b in ["getMiss — pulsed keys", "getMiss — random keys"])

    # winner strip: best impl per (dist, lf)
    winners = []
    for b in benches:
        cells = "".join(
            (lambda best: f'<td>{html.escape(best[0])}<br><span class="sub">{fmt(best[1])} ms</span></td>' if best[1] is not None else "<td>—</td>")(
                min(((i, score(data, b, r, i)) for i in impls_all if score(data, b, r, i) is not None),
                    key=lambda t: t[1], default=("—", None)))
            for r in run_labels)
        winners.append(f'<tr><th>{html.escape(b.split(" — ")[1])}</th>{cells}</tr>')
    winners_html = (f'<table class="mini"><thead><tr><th></th>'
                    + "".join(f"<th>{html.escape(r)}</th>" for r in run_labels)
                    + f'</tr></thead><tbody>{"".join(winners)}</tbody></table>')

    light_css = "".join(f".s{k}{{fill:{LIGHT_SERIES[k % 8]}}} .chip{k}{{background:{LIGHT_SERIES[k % 8]}}}"
                        for k in range(len(chart_impls)))
    dark_css = "".join(f".s{k}{{fill:{DARK_SERIES[k % 8]}}} .chip{k}{{background:{DARK_SERIES[k % 8]}}}"
                       for k in range(len(chart_impls)))
    legend = "".join(f'<span class="key"><span class="chip chip{k}"></span>{html.escape(i)}'
                     f'{" (baseline)" if i == "FASTUTIL" else ""}</span>'
                     for k, i in enumerate(chart_impls))

    page = f"""<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>HashMapLockFree: the get() campaign</title>
<style>
  :root {{ color-scheme: light dark; }}
  .viz-root {{
    --surface-1:#fcfcfb; --page:#f9f9f7; --ink:#0b0b0b; --ink-2:#52514e; --muted:#898781;
    --grid:#e1e0d9; --axis:#c3c2b7; --border:rgba(11,11,11,.10);
    font: 15px/1.55 system-ui, -apple-system, "Segoe UI", sans-serif;
    background: var(--page); color: var(--ink); min-height: 100vh; margin: 0;
    padding: 32px 24px 48px; box-sizing: border-box;
  }}
  .viz-root {{ {light_css} }}
  @media (prefers-color-scheme: dark) {{
    .viz-root {{
      --surface-1:#1a1a19; --page:#0d0d0d; --ink:#ffffff; --ink-2:#c3c2b7; --muted:#898781;
      --grid:#2c2c2a; --axis:#383835; --border:rgba(255,255,255,.10);
      {dark_css}
    }}
  }}
  main {{ max-width: 1060px; margin: 0 auto; }}
  h1 {{ font-size: 26px; margin: 0 0 2px; }}
  h2 {{ font-size: 19px; margin: 40px 0 8px; }}
  h3 {{ font-size: 15px; margin: 20px 0 6px; }}
  p, li {{ max-width: 76ch; color: var(--ink); }}
  .meta, .sub {{ color: var(--ink-2); font-size: 13px; font-weight: 400; }}
  .legend {{ display: flex; gap: 16px; flex-wrap: wrap; margin: 12px 0; }}
  .key {{ display: inline-flex; align-items: center; gap: 6px; color: var(--ink-2); font-size: 13px; }}
  .chip {{ width: 12px; height: 12px; border-radius: 3px; display: inline-block; }}
  .grid2 {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(480px, 1fr)); gap: 16px; }}
  .grid1 {{ display: grid; grid-template-columns: 1fr; gap: 8px; }}
  .grid1 table {{ margin: 0 0 20px; }}
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
  table {{ border-collapse: collapse; background: var(--surface-1); border: 1px solid var(--border);
          border-radius: 10px; margin: 10px 0; font-size: 14px; }}
  th, td {{ padding: 6px 14px; text-align: left; border-bottom: 1px solid var(--grid); }}
  td.num {{ text-align: right; font-variant-numeric: tabular-nums; }}
  th {{ color: var(--ink-2); font-weight: 600; }}
  table.mini td {{ text-align: center; }}
  details {{ margin: 16px 0; }}
  summary {{ cursor: pointer; color: var(--ink-2); }}
  .verdict {{ background: var(--surface-1); border: 1px solid var(--border); border-left: 4px solid #2a78d6;
             border-radius: 10px; padding: 14px 18px; margin: 16px 0; }}
</style></head>
<body class="viz-root"><main>
<h1>HashMapLockFree: the get() campaign</h1>
<p class="meta">Deephaven sandbox study · JDK 21 · JMH, 1 fork, 3×500ms warmup + 5×500ms measurement per trial ·
WSL2 (expect modest run-to-run drift) · all implementations differentially verified against java.util.HashMap
(2.1M-op randomized batches, both key distributions) before any number below was recorded.</p>

<h2>The workload</h2>
<p>A <strong>redirection index</strong> maps row keys to storage positions. Three properties define the benchmark:
every lookup <em>hits</em> (the index maps every requested key by construction); keys arrive in
<strong>pulses</strong> — runs of N sequential row keys separated by gaps (here N ~ U[100, 10000],
G ~ U[100, 100000]); and the resident table dwarfs any single read (67–127M keys in the table, 1M per read).
Readers are lock-free against a single writer, which is the design constraint the whole family exists to satisfy —
and the reason the obvious fastutil baseline is answering a different question (see verdict).</p>
<p>Methodology note: every group probes a table of identical geometry (~2<sup>27</sup> entry slots, ~2GB); load
factor is varied by inserting more keys, not by shrinking the array. This isolates fullness from footprint, and
sidesteps fastutil's power-of-two quantization, which would otherwise silently leave it half-full while others ran
at 0.9. Charts show ms per 1M lookups — directly comparable everywhere; hover for exact numbers and ns/op.</p>
<p>One caveat for reading the pulsed columns: each pulsed read is a random contiguous window of pulses, and window
placement re-rolls per table size — so pulsed results include genuine workload variance across load-factor groups,
not just measurement noise. The extreme case is instructive: at lf 0.95 the sampled window was dense enough that
NOMOD_K1V1 served 1M lookups in 1.5 ms (~1.5 ns each) — adjacent-bucket locality turning a hash probe into a
near-linear scan of a few cache-resident megabytes. That is the redirection-index superpower in its purest form.</p>

<h2>Headline: hit-path lookups, random vs pulsed</h2>
<div class="legend">{legend}</div>
<div class="grid1">{panels}</div>
<p class="sub">Table cells are ms per 1M lookups ± JMH's 99.9% confidence interval — numerically identical to
nanoseconds per lookup.</p>
<h3>Fastest implementation per cell</h3>
{winners_html}

<h2>How we got here</h2>
<h3>Act 1 — the batch interface (and the death of megamorphic dispatch)</h3>
<p>The interface moved from per-element calls to 4096-element batches. Type-profile pollution cost a per-element
call site ~33% (42.1 → 55.8 ms per 1M hits when the get site had seen all impls); with batches the same experiment
measures 47.3 vs 47.4 ms — one virtual dispatch per 4096 elements is free. Batching also created the hoisting
point everything later depends on.</p>
<h3>Act 2 — HMLFnomod: the reciprocal trick</h3>
<p>The original probes pay a 64-bit modulo per bucket. The batch boundary lets each call snapshot the array and
precompute <code>ceil(2<sup>64</sup>/numBuckets)</code> once, turning the modulo into a multiply
(Lemire fastmod) while keeping the <em>deliberately weak</em> probe1 fold that sends sequential keys to adjacent
buckets. Result at 1M keys, lf 0.5: random-key hits 43.5 → 30.1 ms, misses 63.8 → 44.0 ms, and the sequential-key
superpower intact (2.7 ms — 10–14× faster than fastutil). A second modulo in the probe-advance was later replaced
by a conditional subtract.</p>
<h3>Act 3 — HMLFamac: memory-level parallelism (a mostly-null result)</h3>
<p>An AMAC-style window of 16 in-flight lookups, each stashing its next bucket's keys a turn ahead. At lf 0.5 it
changed nothing (window 8/16/32 all flat): short probe chains and independent loop iterations mean out-of-order
hardware already extracts the available parallelism, and Java has no true prefetch to add more. At lf 0.9 —
long chains — it earns ~9–15%: AMAC_K4V4 was the first DH impl to beat fastutil on any random-key benchmark
(275 vs 307 ms misses at 1.89M keys).</p>
<h3>Act 4 — alignment: the ByteBuffer experiment</h3>
<p>A K4V4 bucket is exactly 64 bytes, but heap arrays can't promise alignment, so most buckets straddle two cache
lines. Backing the same algorithm with a 64-byte-aligned direct ByteBuffer answered "how much does that cost":
~15% of lookup performance at lf 0.5 (95.5 → 80.5 ms hits), ~9% on misses at 0.9 — at the price of ~33% slower
fills (bounds-checked stores + direct-buffer zeroing) and the direct-memory lifecycle burden. It is absent from
the headline study for a structural reason discovered there: <strong>ByteBuffer is int-indexed, capping one table
at 2GB</strong> — the 67M-key K4V4 table needs 2.19GB and simply cannot be allocated. Scaling the aligned design
past 2GB requires segmented buffers or JDK 22+ MemorySegment (long-indexed, arena-managed), which is where that
experiment should go next.</p>

<h2>Verdict</h2>
<div class="verdict">
<p><strong>Why not just use fastutil?</strong> Its speed comes from linear probing and backward-shift deletion —
both of which move or scan entries in ways that are <em>illegal under a lock-free reader</em>: a concurrent shift
can make a present key silently vanish from a reader's probe path. Its speed is the prize for dropping exactly the
guarantee the redirection index requires. And on the workload that actually matters — pulsed hits — its scrambling
hash discards the locality the DH weak-fold hash exploits, as the headline chart shows.</p>
<p><strong>What to keep:</strong> the batch interface, the reciprocal trick, and the wide-bucket (K4V4-style)
shapes, which barely notice high load factors. AMAC and 64-byte alignment are situational: worth it for
miss-heavy or 0.9+ deployments, not for the common case. The write path was out of scope here beyond spot
checks; benchmark it before promoting any variant.</p>
</div>

<h2>Appendix</h2>
<p class="sub">Miss-path results (lf 0.5, 67M-key table) — irrelevant to redirection indexes, included for
completeness; fastutil's linear probing is strongest exactly here.</p>
<div class="grid2">{miss_panels or "<p class='sub'>final-get.json not found</p>"}</div>
<details><summary>Full numbers — every implementation, every cell (ms per 1M lookups, ns per lookup)</summary>
<table><thead><tr><th>Benchmark</th><th>Group</th><th>Impl</th><th>Score (ms)</th><th>99.9% CI</th>
<th>ns/lookup</th></tr></thead>
<tbody>{full_table(run_labels, impls_all, data, benches)}
{full_table(["lf 0.5"], miss_impls, miss_data, ["getMiss — pulsed keys", "getMiss — random keys"]) if miss_data else ""}</tbody></table>
</details>
<p class="sub">Reproduce: sandbox/dh-hashmap-bench · ./gradlew smokeTest (differential correctness) ·
./gradlew run --args="getHit -p size=&lt;lf·2^27&gt; -p lookups=1000000 -p presize=true -p loadFactor=&lt;lf&gt;
-p keyDist=random,pulsed -f 1 -wi 3 -i 5 -rf json" · python3 tools/final_report.py</p>
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
</main></body></html>"""
    out = "results/final-report.html"
    with open(out, "w") as f:
        f.write(page)
    print(f"wrote {out}: {len(run_labels)} lf groups, {len(impls_all)} impls, charts show {len(chart_impls)}")


if __name__ == "__main__":
    main()
