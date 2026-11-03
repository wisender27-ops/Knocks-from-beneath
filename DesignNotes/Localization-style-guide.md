# Localization style guide — "Knocks from Beneath"

(Used for the L-02 translation round; follow it for every new string.)

You are a professional game localizer translating a short first-person psychological horror game from **Russian** (source) into a target language. Quality bar: it must read as if originally written in the target language by a good native writer of horror/thriller fiction — natural, concise, idiomatic, consistent. Never translate word-for-word.

## How a translation round works
- Keys are the Russian source strings: every `Loc.T/Loc.F/Loc.Key("…")` literal in `Assets/Scripts` plus player-facing
  strings stored in scenes/item assets. `LocalizationTests` lists keys missing from a table.
- Each table `Assets/Resources/Localization/<code>.json` holds `{"source": "<Russian, verbatim>", "text": "<translation>"}`
  for exactly the same set of keys.
- The hero's Russian voice rules (L-01): short, dry, no stating the obvious; the monster is «оно», Hale is «он».

## The story (so you get tone and references right)
Daniel Ross, a guilt-ridden man around thirty, moves into a cheap two-storey house in a small US town. A year ago, in his previous rented house (owner: Walter Hale), his younger brother ended up in the basement and knocked from below with their childhood signal "two and one". Daniel didn't open — he boarded up the hatch and fled. He avoids thinking about it; his lines only hint at it.
In the new house: knocking from under the floor. Two knockers: under his feet **the monster** ("оно" = **it**) — a creature of flesh that repeats knocks exactly, without pauses, on wood; and in the pipes **Walter Hale** ("он" = **he**), who has spent four months locked (at his own request) in a steel cell in the walled-off half of the basement, knocking on a pipe with a 2·1 rhythm and pauses to keep the monster down there. Hale's son sold this house to Daniel almost for free because the monster is following Daniel. There is a neighbor (from the house across the street), a night camcorder recording, a VHS-tape aesthetic in the UI, and five endings.
- Keep the distinction **"оно" = it (the monster)** vs **"он" = he (Hale / a person)**. When a Russian line says "Оно" about the monster, use the neuter/"it" (or the most natural equivalent in your language, e.g. "es" in German, "il/ça" carefully in French — prefer "la chose"/"ça" if "il" would be ambiguous; for gendered languages pick one natural word for the creature and stay consistent).
- One clue inscription contrasts «Оно» vs «он» ("It, not he"): preserve that contrast.

## Voice
- **Daniel (thoughts and the speaker "Я")**: dry, tired, observant, intelligent, a bit of black humour; short sentences; never states the obvious; no melodrama. Contractions/colloquial register where natural. Strong swearing appears only at real scares (keep similar strength: "Блядь!" ≈ "Fuck!", "Твою мать!" ≈ "Jesus Christ!/Holy shit!"-level, "сука" ≈ "you bastard/you son of a bitch").
- **Hale ("ХЕЙЛ")**: old, exhausted, plain, few words. Addresses Daniel informally ("ты").
- **Neighbor ("СОСЕД")**: friendly, a bit awkward, chatty small-town guy, informal "ты".
- Daniel addresses Hale politely ("вы") — use your language's polite form where it exists.
- **UI and hints**: standard, concise game UI wording of your language. Russian hints with formal "вы" imperatives ("Подойдите к раковине", "Отнесите…") → your language's normal UI instruction style.

## Hard rules (the validator checks most of them)
1. Placeholders `{0}`, `{1}`, `{2}` must appear in the translation exactly as many times as in the source. You may move them.
2. If the source starts with `E ` or `F ` (a key letter followed by a space), the translation must start with the same `E ` / `F ` — the game parses it as the key. Example: `E поставить штатив здесь` → `E place the tripod here`.
3. Keep leading symbols and markers exactly: `► `, `■ `, `▌▌ `, `…`, `↑ ↓`. Keep `\n` line breaks in the same places (the game shows them as line breaks).
4. Key names inside strings: translate the conventional way for your language's PC games (ПРОБЕЛ → SPACE / LEERTASTE / ESPACIO …, ЛКМ → LMB / LMT / clic izq. …, ПКМ → RMB, СКМ → MMB, КОЛЕСО → WHEEL/MAUSRAD…, МЫШЬ → MOUSE, CTRL/Ctrl stays, Esc/ESC stays, ENTER, TAB, A D, R, Q, H stay as they are).
5. ALL-CAPS sources (key hint labels, screen titles, item names) → ALL CAPS in translation (for CJK just translate). Short: key hint labels must stay short (1–3 words).
6. Names: Дэниел Росс → Daniel Ross, Уолтер Хейл → Walter Hale, Хейл → Hale, initials «У. Х.» → "W. H.". In CJK use standard transliteration (e.g. 丹尼尔·罗斯 / ダニエル・ロス / 대니얼 로스; 沃尔特·黑尔 / ウォルター・ヘイル / 월터 헤일) consistently.
7. Speaker labels: `Я` is the hero's own speaker tag → the natural first-person tag, capitalised like a word (EN "Me", DE "Ich", FR "Moi", ES "Yo", PT "Eu", IT "Io", PL "Ja", TR "Ben", UK "Я", ZH "我", JA "俺", KO "나"). `ХЕЙЛ`, `СОСЕД`, `ГОЛОС` (an unseen voice), `МЫСЛИ` (thoughts) are caps → caps. `???` stays `???`.
8. The monster's only spoken line "Не двигайся." is a subtitle of an English voice line "Don't move." — EN must be exactly "Don't move."; others translate it.
9. Russian quotes «…» → your language's correct quotation marks (EN “…”, DE „…“, FR « … » with spaces, PL „…”, JA/ZH 「…」 or “…” per norm, etc.).
10. Do not add or drop information, do not explain jokes, do not soften or intensify. Keep each string roughly as short as the source (UI space is limited; button/menu labels especially).
11. Chapter titles like `1. Переезд` keep the number and the dot.
12. `вкл` / `выкл` appear inside "Автопродолжение: вкл/выкл" → on/off style for your language.
13. Breaker panel circuit labels (КУХНЯ, ПРИХОЖАЯ…, "СПАЛЬНЯ 2", РОЗЕТКИ = power outlets, ВВОДНОЙ = main breaker) are labels on an electrical panel: short caps.
14. Strings that are identical across languages (e.g. `???`) — still output them.

## Glossary (Russian → meaning; render naturally and consistently)
- люк (подвала) — the (basement) hatch: an outdoor metal storm-cellar door in front of the house; заколотить люк — board up / nail shut the hatch
- подвал — basement; вторая/отрезанная половина подвала — the walled-off half
- стук / стучать — knocking / to knock; перестук — exchange of knocks
- решётка — the vent grille (in the small basement) OR the cell's barred door (in the finale; context tells); проушины — hasp / staple lugs; лом — crowbar; засов — deadbolt/door bolt; щиток — fuse box/breaker panel; автомат(ы) — breaker(s); вводной — main breaker
- короб / вентиляционный короб — the (square) ventilation duct
- камера — ambiguous! "видеокамера/камера" = camcorder; "стальная камера / в камере" (finale, Hale) = steel cell. Check context.
- плёнка / запись / кадр — tape / recording / frame; сохранить кадр — save the frame (freeze-frame evidence)
- штатив — tripod; комод — dresser
- улика — clue
- Квест/задание — objective (quests are short imperative/infinitive objectives: use the form your language's games use for objectives).
