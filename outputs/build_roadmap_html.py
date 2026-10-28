"""Build the offline roadmap view from DESIGN_ROADMAP.md (stdlib only)."""
from collections import Counter
from pathlib import Path
import html
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "DESIGN_ROADMAP.md"
OUTPUT = ROOT / "outputs/roadmap/index.html"
STATUSES = {"x": ("done", "Выполнено"), "~": ("active", "В работе"), " ": ("todo", "Не начато"), "-": ("cancelled", "Отменено")}


def inline(value):
    value = html.escape(value)
    value = re.sub(r"`([^`]+)`", r"<code>\1</code>", value)
    value = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", value)
    value = re.sub(r"_\((.+?)\)_", r"<em>(\1)</em>", value)
    return value


def plain(value):
    return re.sub(r"[*`_]", "", value).strip(" —–:;")


def parse(source):
    section = source.split("## 6. План по фазам", 1)[1].split("### Рекомендуемый порядок", 1)[0]
    tasks, phases, current = [], [], None
    phase = ""
    for line in section.splitlines():
        if line.startswith("### Фаза"):
            phase = line[4:].strip()
            phases.append(phase)
            current = None
        match = re.match(r"^- \[([ x~\-])\] \*\*([A-Z]-\d+[a-z]?)\.\s*(.*?)\*\*(.*)$", line)
        if match:
            status, task_id, title, tail = match.groups()
            current = {"id": task_id, "title": plain(title), "phase": phase, "status": STATUSES[status][0], "label": STATUSES[status][1], "lines": [tail.strip()]}
            tasks.append(current)
        elif current is not None:
            current["lines"].append(line)
    expected = len(re.findall(r"^- \[[ x~\-]\]", section, re.M))
    assert len(tasks) == expected, (len(tasks), expected)
    assert len({t["id"] for t in tasks}) == len(tasks), "Duplicate task ids"
    for task in tasks:
        task["body"] = "\n".join(task.pop("lines")).strip()
    return tasks, phases


def description(value):
    paragraphs, lines = [], []
    for line in value.splitlines():
        if not line.strip():
            if lines:
                paragraphs.append("<p>" + " ".join(lines) + "</p>")
                lines = []
        elif line.lstrip().startswith("- "):
            if lines:
                paragraphs.append("<p>" + " ".join(lines) + "</p>")
                lines = []
            paragraphs.append('<p class="bullet">' + inline(line.strip()[2:]) + "</p>")
        else:
            lines.append(inline(line.strip()))
    if lines:
        paragraphs.append("<p>" + " ".join(lines) + "</p>")
    return "\n".join(paragraphs)


def build():
    tasks, phases = parse(SOURCE.read_text(encoding="utf-8"))
    counts = Counter(t["status"] for t in tasks)
    actionable = len(tasks) - counts["cancelled"]
    progress = round(100 * counts["done"] / actionable) if actionable else 0
    sections = []
    for index, phase in enumerate(phases):
        group = [t for t in tasks if t["phase"] == phase]
        cards = []
        for task in group:
            search = html.escape(" ".join([task["id"], task["title"], task["body"], phase]).lower(), quote=True)
            excerpt = plain(task["body"].splitlines()[0])
            excerpt = excerpt[:158].rsplit(" ", 1)[0] + "…" if len(excerpt) > 158 else excerpt
            cards.append(f'''<details class="task" id="task-{task['id']}" data-status="{task['status']}" data-phase="{index}" data-search="{search}">
<summary><span class="task-id">{task['id']}</span><span class="task-title">{html.escape(task['title'])}<span class="excerpt">{html.escape(excerpt)}</span></span><span class="badge {task['status']}">{task['label']}</span><span class="chevron" aria-hidden="true">＋</span></summary>
<div class="task-body">{description(task['body'])}<a class="anchor" href="#task-{task['id']}">Ссылка на задачу {task['id']} ↗</a></div></details>''')
        short = re.sub(r"\s*\(.*", "", phase).split(" — ", 1)[-1]
        group_done = sum(t["status"] == "done" for t in group)
        sections.append(f'''<section class="phase" data-phase="{index}" aria-labelledby="phase-{index}"><div class="phase-heading"><span class="phase-number">0{index}</span><div><p class="eyebrow">Фаза {index}</p><h2 id="phase-{index}">{html.escape(short)}</h2></div><span class="phase-count">{group_done} / {len(group)} выполнено</span></div><div class="tasks">{''.join(cards)}</div></section>''')
    options = ''.join('<option value="{}">{}</option>'.format(i, html.escape(re.sub(r"\s*\(.*", "", p))) for i, p in enumerate(phases))
    stats = ''.join(f'<button class="stat" type="button" data-filter="{key}" aria-pressed="false"><span class="stat-value {key}">{counts[key]:02}</span><span>{label}</span></button>' for key, label in [("done", "Выполнено"), ("active", "В работе"), ("todo", "Не начато"), ("cancelled", "Отменено")])
    page = TEMPLATE.replace("__TOTAL__", str(len(tasks))).replace("__PROGRESS__", str(progress)).replace("__DONE__", str(counts["done"])).replace("__ACTIONABLE__", str(actionable)).replace("__STATS__", stats).replace("__OPTIONS__", options).replace("__SECTIONS__", "\n".join(sections))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(page, encoding="utf-8")
    print(json.dumps({"output": str(OUTPUT), "tasks": len(tasks), "phases": len(phases), "statuses": dict(counts), "progress": progress}, ensure_ascii=False))


TEMPLATE = r'''<!doctype html>
<html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><meta name="color-scheme" content="dark"><title>Knocks from Beneath · Дорожная карта</title>
<style>
:root{--bg:#101310;--panel:#171c18;--panel2:#1d241e;--line:#303b32;--text:#eef1e9;--muted:#a7b1a6;--accent:#f0b477;--green:#9ec9a7;--blue:#9ec5e5;--dim:#bec1b7;--cancel:#d6a5a5;font-family:Inter,"Segoe UI",Arial,sans-serif;color:var(--text);background:var(--bg)}*{box-sizing:border-box}body{margin:0;background:radial-gradient(ellipse at 85% 0,#26332880,transparent 50%);line-height:1.55}button,input,select{font:inherit}button,a,input,select,summary{-webkit-tap-highlight-color:transparent}a{color:var(--accent);text-underline-offset:4px}button{cursor:pointer}button:focus-visible,a:focus-visible,summary:focus-visible,input:focus-visible,select:focus-visible{outline:2px solid var(--accent);outline-offset:5px}[hidden]{display:none!important}.wrap{max-width:1180px;margin:auto;padding:0 36px}.top{display:flex;align-items:center;justify-content:space-between;gap:20px;border-bottom:1px solid var(--line);padding:24px 0;font-size:12px;letter-spacing:.1em;text-transform:uppercase;color:var(--muted)}.brand{color:var(--text);font-weight:700}.rec{display:inline-block;width:7px;height:7px;background:var(--accent);border-radius:50%;margin-right:10px;box-shadow:0 0 16px #f0b47745}.top a{font-size:11px;text-decoration:none;color:var(--muted)}.hero{display:grid;grid-template-columns:1.45fr 1fr;gap:60px;padding:66px 0 40px;align-items:end}.eyebrow{font-size:11px;letter-spacing:.17em;text-transform:uppercase;color:var(--accent);margin:0 0 12px;font-weight:700}h1{font-size:clamp(36px,5vw,62px);letter-spacing:-.055em;line-height:1.05;font-weight:650;margin:0 0 22px}h1 span{color:var(--muted)}.intro{color:var(--muted);max-width:520px;font-size:15px;margin:0}.progress{border:1px solid var(--line);border-radius:16px;padding:24px;background:linear-gradient(145deg,#222c22,#171c18)}.progress-top{display:flex;align-items:center;justify-content:space-between;color:var(--muted);font-size:13px}.percent{font-size:44px;font-weight:600;color:var(--green);letter-spacing:-.05em;line-height:1}.bar{height:5px;background:#354236;border-radius:5px;margin:22px 0 14px;overflow:hidden}.bar span{display:block;width:__PROGRESS__%;height:100%;background:var(--green)}.progress-note{margin:0;font-size:12px;color:var(--muted)}.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:0 0 38px}.stat{text-align:left;display:flex;align-items:center;gap:15px;padding:21px 22px;border:1px solid var(--line);border-radius:12px;background:#171c18;color:var(--muted);font-size:13px;transition:border-color .15s,background .15s}.stat:hover,.stat[aria-pressed=true]{border-color:var(--accent);background:#242a22}.stat-value{font-size:30px;line-height:1.1;letter-spacing:-.05em;font-weight:650}.done{color:var(--green)}.active{color:var(--accent)}.todo{color:var(--blue)}.cancelled{color:var(--cancel)}.controls{display:grid;grid-template-columns:1.6fr 1fr 1fr;gap:12px;border-top:1px solid var(--line);padding-top:24px}.field label{display:block;font-size:11px;color:var(--muted);margin-bottom:7px;letter-spacing:.04em}.field input,.field select{width:100%;height:46px;border:1px solid var(--line);border-radius:8px;padding:0 13px;background:var(--panel);color:var(--text);min-width:0}.field input::placeholder{color:#879386}.toolbar{display:flex;gap:18px;align-items:center;justify-content:space-between;margin:17px 0 31px;color:var(--muted);font-size:12px}.toolbar-actions{display:flex;gap:16px}.text-button{background:none;border:0;color:var(--muted);padding:4px 0;font-size:12px;text-decoration:underline;text-underline-offset:4px}.text-button:hover{color:var(--text)}.phase{margin-bottom:34px}.phase-heading{display:flex;align-items:center;gap:16px;padding:10px 0 17px}.phase-number{border:1px solid var(--line);border-radius:9px;width:42px;height:42px;display:grid;place-items:center;font-family:Consolas,monospace;color:var(--muted);font-size:13px}.phase-heading .eyebrow{margin:0 0 2px;font-size:9px;color:var(--muted)}h2{font-size:21px;font-weight:550;margin:0;line-height:1.25;letter-spacing:-.02em}.phase-count{margin-left:auto;color:var(--muted);font-size:11px;white-space:nowrap}.tasks{border:1px solid var(--line);border-radius:12px;overflow:hidden;background:#171c18a6}.task+.task{border-top:1px solid var(--line)}summary{display:flex;align-items:center;gap:18px;padding:20px 22px;cursor:pointer;list-style:none}summary::-webkit-details-marker{display:none}summary:hover{background:#202820}.task[open] summary{background:var(--panel2)}.task-id{font:12px Consolas,monospace;color:var(--muted);min-width:53px;align-self:flex-start;padding-top:3px}.task-title{font-size:15px;font-weight:550;flex:1;min-width:0}.excerpt{display:block;font-size:12px;line-height:1.6;color:var(--muted);font-weight:400;margin-top:5px;max-width:670px}.badge{font-size:10px;border:1px solid currentColor;border-radius:5px;padding:4px 8px;white-space:nowrap;opacity:.95;letter-spacing:.02em}.chevron{color:var(--muted);font-size:19px;font-weight:300;line-height:1;width:18px;text-align:center}.task[open] .chevron{transform:rotate(45deg)}.task-body{font-size:13px;color:#c3cec0;padding:4px 94px 23px;border-top:1px solid #303b3260;overflow-wrap:anywhere}.task-body p{margin:13px 0}.task-body .bullet{padding-left:15px;border-left:2px solid #4c5a47}.task-body strong{color:var(--text);font-weight:600}.task-body code{font:11px Consolas,monospace;color:#e3c39e;background:#101610;padding:2px 4px;border-radius:3px}.anchor{display:inline-block;font-size:11px;margin-top:10px}.empty{border:1px dashed var(--line);padding:50px 20px;text-align:center;border-radius:12px;color:var(--muted);margin-bottom:40px}.empty strong{display:block;color:var(--text);font-size:20px;margin-bottom:8px}.source-note{border-top:1px solid var(--line);padding:27px 0 42px;display:flex;gap:30px;justify-content:space-between;font-size:11px;color:var(--muted)}.source-note p{margin:0;max-width:690px}.source-note span{white-space:nowrap;font-family:Consolas,monospace;font-size:10px;letter-spacing:.08em}html{scroll-padding-top:24px}.task:target{outline:1px solid var(--accent);outline-offset:-1px}.skip{position:absolute;top:-60px;left:24px;background:var(--accent);color:#101310;padding:10px;z-index:5}.skip:focus{top:10px}@media(max-width:800px){.wrap{padding:0 22px}.hero{gap:25px;padding-top:42px}.stats{grid-template-columns:repeat(2,1fr)}.controls{grid-template-columns:1fr 1fr}.field:first-child{grid-column:1/-1}summary{gap:12px;padding:18px 15px}.task-body{padding:4px 24px 22px}.excerpt{display:none}.task-id{min-width:48px}.badge{font-size:9px}.phase-count{font-size:10px}.source-note{display:block}.source-note span{display:block;margin-top:18px}}@media(max-width:560px){.top{font-size:10px}.top a{font-size:9px}.hero{grid-template-columns:1fr;gap:26px;padding-bottom:24px}.progress{padding:18px}.progress-top{font-size:12px}.percent{font-size:36px}.stats{gap:8px;margin-bottom:26px}.stat{padding:17px 14px;gap:11px;font-size:12px}.stat-value{font-size:26px}.controls{gap:10px}.toolbar{align-items:flex-start;gap:12px}.toolbar-actions{gap:12px}h2{font-size:18px}.phase-count{max-width:72px;text-align:right;white-space:normal}.phase-heading{gap:12px}.phase-number{width:34px;height:34px;flex-shrink:0}summary{display:grid;grid-template-columns:48px 1fr 18px;gap:6px 9px}.task-title{font-size:14px}.badge{grid-column:2;justify-self:start}.chevron{grid-column:3;grid-row:1/3}.task-id{grid-row:1/3}.source-note{font-size:10px}}@media(prefers-reduced-motion:reduce){*{transition:none!important}}@media print{:root{color:#111;background:#fff}.top,.controls,.toolbar,.chevron,.anchor,.skip{display:none}.wrap{max-width:none;padding:0}.hero{padding-top:0}.hero h1{font-size:30px}.progress,.stat,.tasks,.task[open] summary{background:none;border-color:#aaa}.task-body,.excerpt,.intro,.progress-note,.phase-count,.stat,.task-id{color:#444}.task-title,h1 span,h2{color:#111}.stats{margin-bottom:20px}.task{break-inside:avoid}.source-note{padding-bottom:0}}
</style></head><body><a href="#roadmap" class="skip">Перейти к задачам</a><div class="wrap"><header class="top"><div class="brand"><span class="rec" aria-hidden="true"></span>Knocks from Beneath</div><a href="https://github.com/wisender27-ops/Knocks-from-beneath/blob/main/DESIGN_ROADMAP.md" target="_blank" rel="noopener">Открыть исходный план ↗</a></header>
<main><div class="hero"><div><p class="eyebrow">Разработка игры · Дорожная карта</p><h1>Дом помнит.<br><span>План — тоже.</span></h1><p class="intro">Все задачи в одном месте: от языка стука до финальной сборки. Выберите фазу, найдите задачу и посмотрите, что уже сделано.</p></div><div class="progress"><div class="progress-top"><span>Общий прогресс</span><strong class="percent">__PROGRESS__<small style="font-size:21px">%</small></strong></div><div class="bar" role="progressbar" aria-label="Выполненные задачи без отменённых" aria-valuenow="__PROGRESS__" aria-valuemin="0" aria-valuemax="100"><span></span></div><p class="progress-note">__DONE__ из __ACTIONABLE__ действующих задач выполнены<br>Отменённые задачи не входят в расчёт.</p></div></div>
<nav class="stats" aria-label="Быстрый фильтр по статусу">__STATS__</nav>
<div class="controls"><div class="field"><label for="search">Поиск по всем задачам</label><input id="search" type="search" placeholder="Например, B-03, подвал или фонарик" autocomplete="off"></div><div class="field"><label for="phase-filter">Фаза разработки</label><select id="phase-filter"><option value="all">Все фазы</option>__OPTIONS__</select></div><div class="field"><label for="status-filter">Статус</label><select id="status-filter"><option value="all">Все статусы</option><option value="done">Выполнено</option><option value="active">В работе</option><option value="todo">Не начато</option><option value="cancelled">Отменено</option></select></div></div>
<div class="toolbar"><span id="result-count" role="status" aria-live="polite">Показано __TOTAL__ из __TOTAL__ задач</span><div class="toolbar-actions"><button type="button" class="text-button" id="expand">Развернуть детали</button><button type="button" class="text-button" id="reset">Сбросить</button></div></div>
<div id="roadmap">__SECTIONS__</div><div class="empty" id="empty" hidden><strong>Задачи не найдены</strong>Измените запрос или сбросьте фильтры.</div></main>
<footer class="source-note"><p>Источник статусов — <a href="../../DESIGN_ROADMAP.md">DESIGN_ROADMAP.md</a>. Это снимок плана на 28 октября 2026 года. Статусы доступны только для просмотра; изменения вносятся в исходный план. Страница работает без интернета.</p><span>SNAPSHOT · 28.10.2026 / __TOTAL__ TASKS</span></footer></div>
<script>
(()=>{'use strict';const tasks=[...document.querySelectorAll('.task')],phases=[...document.querySelectorAll('.phase')],search=document.getElementById('search'),phase=document.getElementById('phase-filter'),status=document.getElementById('status-filter'),stats=[...document.querySelectorAll('[data-filter]')],expand=document.getElementById('expand');let expanded=false;const norm=s=>s.toLocaleLowerCase('ru').replaceAll('ё','е');function filter(){const words=norm(search.value.trim()).split(/\s+/).filter(Boolean);let visible=0;tasks.forEach(task=>{const text=norm(task.dataset.search);const match=(phase.value==='all'||task.dataset.phase===phase.value)&&(status.value==='all'||task.dataset.status===status.value)&&words.every(word=>text.includes(word));task.hidden=!match;if(match)visible++;});phases.forEach(group=>{group.hidden=![...group.querySelectorAll('.task')].some(task=>!task.hidden);});stats.forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.filter===status.value)));document.getElementById('result-count').textContent=`Показано ${visible} из ${tasks.length} задач`;document.getElementById('empty').hidden=visible!==0;}search.addEventListener('input',filter);phase.addEventListener('change',filter);status.addEventListener('change',filter);stats.forEach(button=>button.addEventListener('click',()=>{status.value=status.value===button.dataset.filter?'all':button.dataset.filter;filter();}));document.getElementById('reset').addEventListener('click',()=>{search.value='';phase.value='all';status.value='all';filter();});expand.addEventListener('click',()=>{expanded=!expanded;tasks.filter(task=>!task.hidden).forEach(task=>task.open=expanded);expand.textContent=expanded?'Свернуть детали':'Развернуть детали';});function openHash(){const id=decodeURIComponent(location.hash.slice(1));const task=document.getElementById(id);if(task&&task.classList.contains('task')){search.value='';phase.value='all';status.value='all';filter();task.open=true;task.scrollIntoView({block:'start'});}}window.addEventListener('hashchange',openHash);window.addEventListener('beforeprint',()=>tasks.forEach(task=>{task.dataset.beforePrint=String(task.open);task.open=true;}));window.addEventListener('afterprint',()=>tasks.forEach(task=>{task.open=task.dataset.beforePrint==='true';}));filter();openHash();})();
</script></body></html>'''


if __name__ == "__main__":
    build()
