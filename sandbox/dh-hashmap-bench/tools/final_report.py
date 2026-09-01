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
WSL2_DIR = "results/wsl2"  # same filenames: the original WSL2 measurements, for the environment section
MISS_FILE = "results/final-get.json"
# 7 series for the charts (palette holds 8); the full 11-impl numbers appear in the appendix table.
CHART_IMPLS = ["K1V1", "K4V4", "FASTUTIL", "NOMOD_K1V1", "NOMOD_K4V4", "AMAC_K2V2", "AMAC_K4V4",
               "AMAC_K4V4_MS"]


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
            pattern = e["params"].get("lookupPattern")
            bench = f"getHit — {dist} keys" + (f" ({pattern} sample)" if pattern and pattern != "window" else "")
            if impl not in impls_all:
                impls_all.append(impl)
            data[(bench, label, impl)] = e
    impls_all.sort(key=lambda i: (IMPL_ORDER.index(i) if i in IMPL_ORDER else 99, i))
    return run_labels, impls_all, data


def load_wsl2():
    data = {}
    for lf, path in LF_FILES:
        p2 = os.path.join(WSL2_DIR, os.path.basename(path))
        if not os.path.exists(p2):
            continue
        for e in json.load(open(p2)):
            if e["benchmark"].endswith("getHit"):
                data[(f'getHit — {e["params"].get("keyDist", "random")} keys', f"lf {lf}",
                      e["params"]["impl"])] = e
    return data


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
    benches = sorted({k[0] for k in data}, key=lambda b: (0 if "pulsed" in b else 1, b))

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

    # winner strips: best impl per (workload, lf), with configurable exclusions
    def winners_table(excluded):
        eligible = [i for i in impls_all if i not in excluded]
        rows = []
        for b in benches:
            cells = "".join(
                (lambda best: f'<td>{html.escape(best[0])}<br><span class="sub">{fmt(best[1])} ms</span></td>'
                 if best[1] is not None else "<td>—</td>")(
                    min(((i, score(data, b, r, i)) for i in eligible if score(data, b, r, i) is not None),
                        key=lambda t: t[1], default=("—", None)))
                for r in run_labels)
            rows.append(f'<tr><th>{html.escape(b.split(" — ", 1)[1])}</th>{cells}</tr>')
        return (f'<table class="mini"><thead><tr><th></th>'
                + "".join(f"<th>{html.escape(r)}</th>" for r in run_labels)
                + f'</tr></thead><tbody>{"".join(rows)}</tbody></table>')

    def penalty_table(chosen, excluded):
        eligible = [i for i in impls_all if i not in excluded]
        rows = []
        for b in benches:
            cells = []
            for r in run_labels:
                mine = score(data, b, r, chosen)
                candidates = [(i, score(data, b, r, i)) for i in eligible if score(data, b, r, i) is not None]
                if mine is None or not candidates:
                    cells.append("<td>—</td>")
                    continue
                best_impl, best = min(candidates, key=lambda t: t[1])
                if best_impl == chosen:
                    cells.append('<td>optimal<br><span class="sub">this cell\'s winner</span></td>')
                else:
                    pct = (mine / best - 1) * 100
                    cells.append(f'<td>+{pct:.0f}%<br><span class="sub">{fmt(mine)} vs {fmt(best)} '
                                 f'({html.escape(best_impl)})</span></td>')
            rows.append(f'<tr><th>{html.escape(b.split(" — ", 1)[1])}</th>{"".join(cells)}</tr>')
        return (f'<table class="mini"><thead><tr><th></th>'
                + "".join(f"<th>{html.escape(r)}</th>" for r in run_labels)
                + f'</tr></thead><tbody>{"".join(rows)}</tbody></table>')

    winners_html = f"""{winners_table(set())}
<h3>Fastest implementation per cell, excluding FASTUTIL</h3>
<p class="sub">FASTUTIL cannot honor the lock-free reader contract (see verdict), so it can win cells but not the
job. This table is the real leaderboard.</p>
{winners_table({"FASTUTIL"})}
<h3>Fastest implementation per cell, excluding FASTUTIL and AMAC_K4V4_MS</h3>
<p class="sub">AMAC_K4V4_MS requires java.lang.foreign (JDK 22+). This table is the leaderboard for deployments
that cannot opt in.</p>
{winners_table({"FASTUTIL", "AMAC_K4V4_MS"})}
<h3>Difference from optimal (excluding FASTUTIL), if AMAC_K4V4 is chosen for every scenario</h3>
<p class="sub">The cost of standardizing on one heap implementation (JDK-11-compatible) instead of picking the
per-scenario winner: cells show how much slower AMAC_K4V4 is than the best contract-eligible choice.</p>
{penalty_table("AMAC_K4V4", {"FASTUTIL"})}
<h3>Difference from optimal (excluding FASTUTIL), if AMAC_K4V4_MS is chosen for every scenario</h3>
<p class="sub">Same question for the JDK-22+ MemorySegment implementation.</p>
{penalty_table("AMAC_K4V4_MS", {"FASTUTIL"})}"""

    light_css = "".join(f".s{k}{{fill:{LIGHT_SERIES[k % 8]}}} .chip{k}{{background:{LIGHT_SERIES[k % 8]}}}"
                        for k in range(len(chart_impls)))
    dark_css = "".join(f".s{k}{{fill:{DARK_SERIES[k % 8]}}} .chip{k}{{background:{DARK_SERIES[k % 8]}}}"
                       for k in range(len(chart_impls)))
    legend = "".join(f'<span class="key"><span class="chip chip{k}"></span>{html.escape(i)}'
                     f'{" (baseline)" if i == "FASTUTIL" else ""}</span>'
                     for k, i in enumerate(chart_impls))

    wsl2 = load_wsl2()
    if wsl2:
        def env_table(bench):
            head = "".join(f"<th>{html.escape(r)}</th>" for r in run_labels)
            rows = []
            for i in chart_impls:
                cells = []
                for r in run_labels:
                    w = wsl2.get((bench, r, i))
                    n = data.get((bench, r, i))
                    if w is None or n is None:
                        cells.append("<td class='num'>—</td>")
                        continue
                    ws, ns = w["primaryMetric"]["score"], n["primaryMetric"]["score"]
                    cells.append(f"<td class='num'>{fmt(ws)} → {fmt(ns)}</td>")
                rows.append(f"<tr><td>{html.escape(i)}</td>{''.join(cells)}</tr>")
            return (f"<table><thead><tr><th>{html.escape(bench)} <span class='sub'>WSL2 → Windows, "
                    f"ms per 1M lookups</span></th>{head}</tr></thead><tbody>{''.join(rows)}</tbody></table>")
        env_section = f"""
<p>The entire suite was rerun on native Windows (Eclipse Temurin 21.0.12, otherwise-idle box) via
<code>tools/run-final-suite.ps1</code>. Three findings. <strong>The rankings held</strong> — every conclusion in
this report reproduces across environments, including the pulsed-key dominance and the crossover where the DH
family overtakes fastutil on random keys at high occupancy. <strong>Measurement quality improved
substantially</strong>: median confidence-interval half-width fell from ±10.1% (WSL2) to ±4.3% (Windows) across
the 80 shared cells. <strong>WSL2's lf 0.5 group was contaminated</strong>: it ran uniformly ~2× slower than
Windows across all implementations including the baseline — a signature of environmental interference (shared
memory bandwidth), not code — while the other groups sat at a consistent ~0.85 ratio that plausibly reflects the
hardware difference. The headline charts above use the Windows numbers; the WSL2 originals are preserved under
<code>results/wsl2/</code>. Note the pulsed lf 0.75 slow column reproduces exactly on both environments —
confirming it is genuine workload variance (that window sampled a sparse pulse region), not noise.</p>
{env_table("getHit — pulsed keys")}
{env_table("getHit — random keys")}"""
    else:
        env_section = "<p class='sub'>results/wsl2/ not found — single-environment report.</p>"

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
<p class="meta">Deephaven sandbox study · JMH, 1 fork, 3×500ms warmup + 5×500ms measurement per trial · headline
sections measured on native Windows, JDK 25 (Temurin); development history (the "acts") measured on WSL2/JDK 21 —
see the environment note · all implementations differentially verified against java.util.HashMap (2.1M-op
randomized batches, both key distributions) before any number below was recorded.</p>

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
<p>Lookup selection matters as much as table content, so it is a first-class dimension: the 1M probed keys are a
uniform random subset of the table keys — never a dense contiguous run, which an earlier draft used and which
flatters pulsed tables absurdly (it turns hashing into a linear scan; numbers like 1.5 ns/lookup result). Each
panel pair shows the subset probed in ascending order (<em>sorted</em> — the realistic redirection-index read) and
in random order (<em>shuffled</em> — the pessimistic bound).</p>

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
<h3>Act 3 — HMLFamac: memory-level parallelism (null at 0.5, decisive when tables fill)</h3>
<p>An AMAC-style window of 16 in-flight lookups, each stashing its next bucket's keys a turn ahead. At lf 0.5 it
changes nothing (window 8/16/32 all flat): probe chains are short and independent loop iterations already let
out-of-order hardware extract the available parallelism, and Java has no true prefetch to add more. But the
window's design case is long probe chains, and when tables fill it delivers: at lf 0.9 it produced the first DH
win over fastutil on any random-key benchmark (275 vs 307 ms misses at 1.89M keys), and in the headline study
above the AMAC variants own every high-load-factor cell — the windowed get is a core ingredient of the
implementations that beat fastutil by 25–60% at lf 0.9–0.95. Verdict by regime, not "mostly null": free to carry
at low occupancy, decisive at high.</p>
<h3>Act 4 — alignment: from ByteBuffer to MemorySegment</h3>
<p>A K4V4 bucket is exactly 64 bytes, but heap arrays can't promise alignment — the JVM places array data where it
likes, so most buckets straddle two cache lines and every probe pays double the miss traffic. A ByteBuffer
prototype proved the thesis (~15% faster lookups at lf 0.5 once buckets sat on single lines) and then hit a wall
that got it deleted from the suite: ByteBuffer is int-indexed <em>in bytes</em>, capping a table at 2GB — 1/8 the
reach of a long[] — and the ~2.15GB headline tables simply cannot be allocated (archived numbers in
<code>results/bb-*.json</code>; code in git history). Its finished form is <strong>AMAC_K4V4_MS</strong>
(java.lang.foreign, JDK 22+): 64-byte-aligned native MemorySegments, long-indexed so no size cap, allocated from
<code>Arena.ofAuto()</code> — the GC-managed arena, chosen deliberately because an old segment must outlive the
rehash that replaces it for exactly as long as some reader still holds the snapshot, which is the same lifetime
contract heap arrays get from the garbage collector for free (a deterministic <code>close()</code> would throw
under a concurrent reader mid-probe). In the headline study it sweeps every lf 0.9/0.95 cell, edging the heap
AMAC_K4V4 by a final ~2–4% — so it is the champion, but a JDK-22+ opt-in luxury rather than a necessity.</p>

<h2>Environment note</h2>
<p>Suite v1 (an earlier workload definition) was run on both WSL2 and native Windows, which validated the
methodology: every ranking reproduced across environments; median confidence-interval half-width fell from ±10.1%
(WSL2) to ±4.3% (Windows); and one WSL2 group was caught running uniformly ~2× slow across all implementations
including the baseline — classic shared-memory-bandwidth contamination, and the reason the headline suite runs on
native Windows. The v1 datasets are archived under <code>results/wsl2/</code> and in git history.</p>

<h2>Verdict</h2>
<div class="verdict">
<p><strong>The load factor is the verdict.</strong> At relaxed occupancy (0.5–0.75), fastutil wins most realistic
lookup workloads — with sorted reads of pulsed tables the one hold-out where the DH weak-fold hash still pays. At
0.9–0.95, the customer-critical regime, <strong>AMAC_K4V4_MS sweeps every cell</strong> — both key distributions,
both probe orderings — beating fastutil by 25–60% even on uniform-random shuffled lookups, its strongest ground.
The full stack earns that: batch interface (amortized dispatch, snapshot, and reciprocal), division-free probing,
the AMAC window (which only pays off when probe chains lengthen), and one-cache-line buckets via 64-byte-aligned
MemorySegments.</p>
<p><strong>Why not just use fastutil anyway?</strong> Its speed comes from linear probing and backward-shift
deletion — both of which move or scan entries in ways that are <em>illegal under a lock-free reader</em>: a
concurrent shift can make a present key silently vanish from a reader's probe path. It is a superb map answering
a different question. Where its tricks are legal, use it; the redirection index is not that place — and at the
load factors customers actually run, it now loses on raw speed too.</p>
<p><strong>What to keep:</strong> the batch interface and reciprocal everywhere; wide (K4V4) buckets; the
MemorySegment variant as the high-load-factor engine (requires JDK 22+ — an opt-in for customers who can take
it). The write path was out of scope beyond spot checks; benchmark it before promoting any variant.</p>
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
