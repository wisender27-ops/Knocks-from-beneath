"""A-01 build: produces Assets/Audio/Game/** clips (staging) + credits rows.
Level targets (loudest 400 ms RMS, dBFS) keep categories consistent; per-event balance is done in code/mixer."""
import os, sys, glob, json
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from audiolib import *
import synth

SP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DL = os.path.join(SP, 'dl')
OUT = os.path.join(SP, 'build', 'Audio', 'Game')
K_RPG = os.path.join(DL, 'kenney_rpg-audio', 'Audio')
K_IMP = os.path.join(DL, 'kenney_impact-sounds', 'Audio')
K_INT = os.path.join(DL, 'kenney_interface-sounds', 'Audio')
K_UI = os.path.join(DL, 'kenney_ui-audio', 'Audio')
OGA = os.path.join(DL, 'oga')
credits = []
report = []

def credit(out, src, author, license_, url):
    credits.append(dict(file=out, source=src, author=author, license=license_, url=url))

def emit(rel, a, target=None, measure='loud', ceiling=-1.0, to_mono=False, q=6):
    if to_mono: a = mono(a)
    if target is not None: a = normalize(a, target, ceiling, measure)
    path = os.path.join(OUT, rel)
    save(path, a, quality=q)
    report.append(f"{rel:45s} dur={len(a)/SR:6.2f}s pk={peak_db(a):6.1f} loud={loud_db(a):6.1f} rms={rms_db(a):6.1f} ch={a.shape[1]}")
    return a

KENNEY = ('Kenney', 'CC0', 'https://kenney.nl/assets/')

def kenney(rel, path, pack, target, to_mono=True, process=None, trim_db=-55):
    a = load(path)
    a = trim(a, trim_db)
    if process: a = process(a)
    a = fades(a, 0.002, 0.03)
    emit(rel, a, target, to_mono=to_mono)
    credit(rel, f"{pack}/{os.path.basename(path)}", 'Kenney (kenney.nl)', 'CC0', KENNEY[2] + pack.replace('kenney_', ''))

def warm(a, lp=7500, semis=0.0):
    if semis: a = resample_pitch(a, semis)
    return biquad(a, 'lp', lp)

def tape(a, semis, mech, mech_semis):
    g = warm(a, 5500, semis)
    m = mono(load(os.path.join(K_RPG, mech)))
    m = biquad(resample_pitch(trim(m, -50), mech_semis), 'lp', 5000)
    g = mono(g)
    n = max(len(g), len(m))
    out = np.zeros((n, 1), np.float32); out[:len(g)] += g; out[:len(m)] += gain(m, -6)
    return out

# ---------------- UI ----------------
def build_ui():
    ui = [('UI/Hover.ogg', os.path.join(K_INT, 'tick_002.ogg'), -30, lambda a: warm(a, 5000, -3)),
          ('UI/Click.ogg', os.path.join(K_INT, 'click_003.ogg'), -22, lambda a: warm(a, 6500, -2)),
          ('UI/Back.ogg', os.path.join(K_INT, 'back_002.ogg'), -24, lambda a: warm(a, 6000, -3)),
          ('UI/Toggle.ogg', os.path.join(K_INT, 'toggle_002.ogg'), -24, lambda a: warm(a, 6500, -2)),
          ('UI/PauseOpen.ogg', os.path.join(K_INT, 'glitch_002.ogg'), -22, lambda a: tape(a, -5, 'metalClick.ogg', -6)),
          ('UI/PauseClose.ogg', os.path.join(K_INT, 'glitch_004.ogg'), -24, lambda a: tape(a, -3, 'metalLatch.ogg', -4)),
          ('UI/InventoryOpen.ogg', os.path.join(K_RPG, 'handleSmallLeather.ogg'), -22, lambda a: warm(a, 8000)),
          ('UI/InventoryClose.ogg', os.path.join(K_RPG, 'handleSmallLeather2.ogg'), -24, lambda a: warm(a, 8000, -1)),
          ('UI/InventoryMove.ogg', os.path.join(K_INT, 'tick_004.ogg'), -30, lambda a: warm(a, 5000, -4)),
          ('UI/JournalOpen.ogg', os.path.join(K_RPG, 'bookOpen.ogg'), -22, None),
          ('UI/JournalClose.ogg', os.path.join(K_RPG, 'bookClose.ogg'), -22, None),
          ('UI/JournalPage.ogg', os.path.join(K_RPG, 'bookFlip2.ogg'), -24, None),
          ('UI/QuestNew.ogg', os.path.join(K_INT, 'bong_001.ogg'), -26, lambda a: warm(a, 4000, -7)),
          ('UI/QuestDone.ogg', os.path.join(K_INT, 'confirmation_002.ogg'), -26, lambda a: warm(a, 4500, -6)),
          ('UI/ThoughtTick1.ogg', os.path.join(K_INT, 'tick_001.ogg'), -34, lambda a: warm(a, 3500, -6)),
          ('UI/ThoughtTick2.ogg', os.path.join(K_INT, 'tick_002.ogg'), -34, lambda a: warm(a, 3500, -7)),
          ('UI/MenuStart.ogg', os.path.join(K_INT, 'maximize_008.ogg'), -24, lambda a: warm(a, 3000, -9))]
    for rel, path, target, proc in ui:
        pack = 'kenney_rpg-audio' if 'rpg' in path else 'kenney_interface-sounds'
        kenney(rel, path, pack, target, process=proc)

# ---------------- Footsteps / foley ----------------
def build_foley():
    sets = {'Wood': 'footstep_wood_00{}.ogg', 'Concrete': 'footstep_concrete_00{}.ogg',
            'Grass': 'footstep_grass_00{}.ogg', 'Carpet': 'footstep_carpet_00{}.ogg'}
    for name, pat in sets.items():
        for i in range(5):
            kenney(f'Foley/Footsteps/{name}{i+1}.ogg', os.path.join(K_IMP, pat.format(i)), 'kenney_impact-sounds',
                   -21 if name != 'Grass' else -23, process=lambda a: biquad(a, 'lp', 9000))
    # Stairs: heavier wood (lower, fuller)
    for i in range(5):
        kenney(f'Foley/Footsteps/Stairs{i+1}.ogg', os.path.join(K_IMP, f'footstep_wood_00{i}.ogg'), 'kenney_impact-sounds', -19,
               process=lambda a: biquad(biquad(resample_pitch(a, -2.5), 'lowshelf', 180, db=4), 'lp', 7000))
    # Dirt/gravel paths outside
    for i in range(6):
        kenney(f'Foley/Footsteps/Dirt{i+1}.ogg', os.path.join(K_RPG, f'footstep0{i}.ogg'), 'kenney_rpg-audio', -23)
    # Metal (hatch lid / plates): light metal impact, damped
    for i in range(4):
        kenney(f'Foley/Footsteps/Metal{i+1}.ogg', os.path.join(K_IMP, f'impactMetal_light_00{i}.ogg'), 'kenney_impact-sounds', -22,
               process=lambda a: fades(biquad(resample_pitch(a, -4), 'lp', 5000)[: int(0.35 * SR)], 0.001, 0.12))
    # Cloth (crouch, grab)
    for i in range(4):
        kenney(f'Foley/Cloth{i+1}.ogg', os.path.join(K_RPG, f'cloth{i+1}.ogg'), 'kenney_rpg-audio', -24)
    # Item material sets: grab/drop/impact
    items = {
        'Cardboard': [('impactSoft_medium_00{}.ogg', 'kenney_impact-sounds', -6)],
        'Paper': [('bookPlace{}.ogg', 'kenney_rpg-audio', 0)],
        'Plate': [('impactPlate_light_00{}.ogg', 'kenney_impact-sounds', 0)],
        'Metal': [('impactMetal_medium_00{}.ogg', 'kenney_impact-sounds', -3)],
        'Glass': [('impactGlass_light_00{}.ogg', 'kenney_impact-sounds', 0)],
        'Wood': [('impactWood_light_00{}.ogg', 'kenney_impact-sounds', 0)],
        'Soft': [('impactSoft_heavy_00{}.ogg', 'kenney_impact-sounds', -3)],
    }
    for name, specs in items.items():
        pat, pack, semis = specs[0]
        base = K_RPG if 'rpg' in pack else K_IMP
        idx = [1, 2, 3] if 'bookPlace' in pat else [0, 1, 2]
        for j, i in enumerate(idx):
            kenney(f'Foley/Items/{name}Drop{j+1}.ogg', os.path.join(base, pat.format(i)), pack, -19,
                   process=(lambda a, s=semis: resample_pitch(a, s) if s else a))
    for j, (f, pack) in enumerate([('handleSmallLeather.ogg', 'kenney_rpg-audio'), ('cloth3.ogg', 'kenney_rpg-audio'),
                                   ('beltHandle1.ogg', 'kenney_rpg-audio')]):
        kenney(f'Foley/Items/Grab{j+1}.ogg', os.path.join(K_RPG, f), pack, -23)
    kenney('Foley/Items/PaperGrab1.ogg', os.path.join(K_RPG, 'bookFlip1.ogg'), 'kenney_rpg-audio', -23)
    kenney('Foley/Items/PaperGrab2.ogg', os.path.join(K_RPG, 'bookFlip3.ogg'), 'kenney_rpg-audio', -23)
    kenney('Foley/Items/MetalGrab1.ogg', os.path.join(K_RPG, 'metalPot1.ogg'), 'kenney_rpg-audio', -22)
    kenney('Foley/Items/MetalGrab2.ogg', os.path.join(K_RPG, 'metalPot2.ogg'), 'kenney_rpg-audio', -22)
    kenney('Foley/Items/Equip.ogg', os.path.join(K_RPG, 'beltHandle2.ogg'), 'kenney_rpg-audio', -22)

# ---------------- World ----------------
def build_world():
    for i in range(3):
        kenney(f'World/Doors/Creak{i+1}.ogg', os.path.join(K_RPG, f'creak{i+1}.ogg'), 'kenney_rpg-audio', -24,
               process=lambda a: biquad(a, 'lp', 6000))
    for i in range(4):
        kenney(f'World/Doors/Close{i+1}.ogg', os.path.join(K_RPG, f'doorClose_{i+1}.ogg'), 'kenney_rpg-audio', -17)
    for i in range(2):
        kenney(f'World/Doors/Open{i+1}.ogg', os.path.join(K_RPG, f'doorOpen_{i+1}.ogg'), 'kenney_rpg-audio', -21)
    kenney('World/Doors/Latch.ogg', os.path.join(K_RPG, 'metalLatch.ogg'), 'kenney_rpg-audio', -20)
    kenney('World/Doors/Lock.ogg', os.path.join(K_RPG, 'metalClick.ogg'), 'kenney_rpg-audio', -19)
    # Запертая дверь: ручку дёргают 3 раза (защёлка упирается).
    latch = mono(trim(load(os.path.join(K_RPG, 'metalLatch.ogg')), -50))
    click = mono(trim(load(os.path.join(K_RPG, 'metalClick.ogg')), -50))
    out = np.zeros((int(0.75 * SR), 1), np.float32)
    for t0, src, semis, db in [(0.0, latch, 0.0, 0), (0.17, click, 1.5, -4), (0.33, latch, -1.0, -2), (0.5, click, 0.5, -6)]:
        x = resample_pitch(src, semis); i = int(t0 * SR); n = min(len(x), len(out) - i)
        out[i:i + n] += gain(x[:n], db)
    emit('World/Doors/LockedRattle.ogg', fades(biquad(out, 'lp', 7000), 0.001, 0.05), -19)
    credit('World/Doors/LockedRattle.ogg', 'kenney_rpg-audio/metalLatch.ogg + metalClick.ogg (layered)', 'Kenney (kenney.nl)', 'CC0', 'https://kenney.nl/assets/rpg-audio')
    # Лом срывает навесной замок: скрежет металла, щелчок дужки, удар о доску.
    def L(f, base=K_IMP): return mono(trim(load(os.path.join(base, f)), -50))
    parts = [(0.0, resample_pitch(L('impactMetal_heavy_002.ogg'), -3), -2), (0.06, L('impactMetal_light_003.ogg'), -6),
             (0.11, resample_pitch(L('impactWood_heavy_001.ogg'), -2), -3), (0.2, L('metalClick.ogg', K_RPG), -8)]
    out = np.zeros((int(1.1 * SR), 1), np.float32)
    for t0, x, db in parts:
        i = int(t0 * SR); n = min(len(x), len(out) - i); out[i:i + n] += gain(x[:n], db)
    emit('World/PryBreak.ogg', fades(out, 0.001, 0.2), -14)
    credit('World/PryBreak.ogg', 'kenney_impact-sounds impactMetal_heavy/light, impactWood_heavy + rpg metalClick (layered)', 'Kenney (kenney.nl)', 'CC0', 'https://kenney.nl/assets/impact-sounds')
    kenney('World/SwitchOn.ogg', os.path.join(K_UI, 'switch12.ogg'), 'kenney_ui-audio', -22, process=lambda a: warm(a, 6000, -3))
    kenney('World/SwitchOff.ogg', os.path.join(K_UI, 'switch13.ogg'), 'kenney_ui-audio', -23, process=lambda a: warm(a, 6000, -4))
    kenney('World/FlashlightOn.ogg', os.path.join(K_UI, 'switch3.ogg'), 'kenney_ui-audio', -24, process=lambda a: warm(a, 7000, 2))
    kenney('World/FlashlightOff.ogg', os.path.join(K_UI, 'switch4.ogg'), 'kenney_ui-audio', -25, process=lambda a: warm(a, 7000, 1))

# ---------------- Ambience / music ----------------
def oga(path): return os.path.join(OGA, path)
OGA_SRC = {
    'amb_morning.wav': ('kresiek-the-furry', 'https://opengameart.org/content/amb-morning-sounds-perfect-loop'),
    'park_ambience_wind.wav': ('thimras', 'https://opengameart.org/content/park-ambiences'),
    'crickets_1.mp3': ('wolfgang', 'https://opengameart.org/content/crickets-ambient-noise-loopable'),
    'rain-gutter-loop_0.mp3': ('ogrebane', 'https://opengameart.org/content/rain-gutter-loop'),
    'dungeon_ambient_1_0.ogg': ('jaggedstone', 'https://opengameart.org/content/loopable-dungeon-ambience'),
    'freezer_0.ogg': ('congusbongus', 'https://opengameart.org/content/abandoned-passages-horror-ambience-loop'),
    'shop/TheShopCollection_convenience_store_drinks_fridge_drone.wav': ('LEGIT Audio', 'https://opengameart.org/content/the-shop'),
    'dark/dark_ambiences/ambience-{}.wav': ('ogrebane', 'https://opengameart.org/content/dark-ambiences'),
}
def ocredit(rel, key):
    author, url = OGA_SRC[key]
    credit(rel, 'opengameart: ' + os.path.basename(key), author, 'CC0', url)

def fit(a, n):
    if len(a) >= n: return a[:n]
    reps = int(np.ceil(n / len(a)))
    return np.concatenate([a] * reps)[:n]

def build_ambience():
    # Day outside: заменено 2026-10-26 на запись пригорода (freesound 614273 + 797675, см. ThirdPartyCredits) — собрано отдельно.
    birds = load(oga('amb_morning.wav'))
    wind = load(oga('park_ambience_wind.wav'))
    n = len(birds)
    wind = wind[SR * 20: SR * 20 + n]
    day = normalize(birds, -28, measure='rms') + normalize(wind, -38, measure='rms')
    emit('Ambience/DayOutside.ogg', loopify(day, 3.0), -27, measure='rms', q=5)
    ocredit('Ambience/DayOutside.ogg', 'amb_morning.wav'); ocredit('Ambience/DayOutside.ogg', 'park_ambience_wind.wav')

    # Night outside: crickets layered (offsets/pitch) + wind
    cr = load(oga('crickets_1.mp3'))
    cr = loopify(cr, 1.0)
    dur = 60 * SR
    night = np.zeros((dur, 2), np.float32)
    for k, (off, semis, db, pan) in enumerate([(0, 0, 0, (1, .7)), (3.7, -1.2, -4, (.6, 1)), (7.1, .8, -7, (1, .5))]):
        layer = resample_pitch(cr, semis) if semis else cr
        layer = np.roll(fit(layer, dur), int(off * SR), axis=0)
        layer = gain(layer, db) * np.array(pan, np.float32)
        night += layer
    night = biquad(night, 'lp', 7000)
    wind2 = load(oga('park_ambience_wind.wav'))[SR * 100: SR * 100 + dur]
    night = normalize(night, -32, measure='rms') + normalize(biquad(wind2, 'lp', 2500), -36, measure='rms')
    emit('Ambience/NightOutside.ogg', loopify(night, 3.0), -29, measure='rms', q=5)
    ocredit('Ambience/NightOutside.ogg', 'crickets_1.mp3'); ocredit('Ambience/NightOutside.ogg', 'park_ambience_wind.wav')

    rain = load(oga('rain-gutter-loop_0.mp3'))
    emit('Ambience/Rain.ogg', loopify(rain, 2.0), -25, measure='rms', q=5)
    ocredit('Ambience/Rain.ogg', 'rain-gutter-loop_0.mp3')

    dun = load(oga('dungeon_ambient_1_0.ogg')); fr = load(oga('freezer_0.ogg'))
    bas = normalize(dun, -27, measure='rms') + normalize(fit(biquad(fr, 'lp', 1500), len(dun)), -36, measure='rms')
    emit('Ambience/Basement.ogg', loopify(bas, 3.0), -26, measure='rms', q=5)
    ocredit('Ambience/Basement.ogg', 'dungeon_ambient_1_0.ogg'); ocredit('Ambience/Basement.ogg', 'freezer_0.ogg')

    fridge = load(oga('shop/TheShopCollection_convenience_store_drinks_fridge_drone.wav'))
    fridge = biquad(fridge, 'lp', 2500)
    emit('Ambience/FridgeHum.ogg', loopify(fridge, 1.5), -26, measure='rms', to_mono=True, q=5)
    ocredit('Ambience/FridgeHum.ogg', 'shop/TheShopCollection_convenience_store_drinks_fridge_drone.wav')

    emit('Ambience/RoomTone.ogg', synth.room_tone(40), -44, measure='rms', q=4)
    emit('Ambience/LampBuzz.ogg', synth.electric_buzz(6), -30, measure='rms', to_mono=True, q=5)
    for rel in ['Ambience/RoomTone.ogg', 'Ambience/LampBuzz.ogg']:
        credit(rel, 'synthesized (tools/synth.py)', 'Knocks from Beneath', 'project-original', '-')

def build_music():
    # Night 1: low, mostly still; Night 2: darker cluster with a minor second; menu: wide, slow.
    d = 64.0
    n1 = synth.drone(d, 49.0, [(1, .5), (1.5, .22), (2, .18), (3, .05), (4.76, .025)], .35, 900, seed=11)
    emit('Music/DroneNight1.ogg', synth.make_loop(n1, 4.0), -25, measure='rms', q=5)
    n2 = synth.drone(d, 43.65, [(1, .5), (1.0595, .2), (1.5, .2), (2, .12), (2.83, .05), (5.04, .025)], .5, 700, seed=23)
    emit('Music/DroneNight2.ogg', synth.make_loop(n2, 4.0), -23, measure='rms', q=5)
    mn = synth.drone(d, 55.0, [(1, .45), (1.2, .2), (1.5, .2), (2, .1), (3.6, .03)], .45, 1100, seed=37)
    emit('Music/DroneMenu.ogg', synth.make_loop(mn, 4.0), -24, measure='rms', q=5)
    dd = synth.drone(48.0, 36.7, [(1, .55), (1.4142, .15), (2, .12), (2.12, .06)], .6, 500, seed=41)
    emit('Music/DroneBasement.ogg', synth.make_loop(dd, 4.0), -24, measure='rms', q=5)
    for rel in ['Music/DroneNight1.ogg', 'Music/DroneNight2.ogg', 'Music/DroneMenu.ogg', 'Music/DroneBasement.ogg']:
        credit(rel, 'synthesized (tools/synth.py)', 'Knocks from Beneath', 'project-original', '-')

def build_horror():
    emit('Horror/Heartbeat.ogg', synth.heartbeat_single(), -14, to_mono=True)
    emit('Horror/Tinnitus.ogg', synth.tinnitus(), -28, measure='loud')
    emit('Horror/SubHit1.ogg', synth.sub_hit(3.5, 52), -12)
    emit('Horror/SubHit2.ogg', synth.sub_hit(4.5, 41), -12)
    emit('Horror/Riser.ogg', synth.riser(4.0), -16)
    for rel in ['Horror/Heartbeat.ogg', 'Horror/Tinnitus.ogg', 'Horror/SubHit1.ogg', 'Horror/SubHit2.ogg', 'Horror/Riser.ogg']:
        credit(rel, 'synthesized (tools/synth.py)', 'Knocks from Beneath', 'project-original', '-')
    for i in range(1, 6):
        key = 'dark/dark_ambiences/ambience-{}.wav'
        a = load(oga(key.format(i)))
        emit(f'Horror/DarkHit{i}.ogg', fades(a, 0.002, 0.3), -14)
        author, url = OGA_SRC[key]
        credit(f'Horror/DarkHit{i}.ogg', f'opengameart: ambience-{i}.wav', author, 'CC0', url)

def layer(parts, dur):
    out = np.zeros((int(dur * SR), 1), np.float32)
    for t0, x, db in parts:
        i = int(t0 * SR); n = min(len(x), len(out) - i)
        if n > 0: out[i:i + n] += gain(mono(x)[:n], db)
    return out

def K(f, base=None): return mono(trim(load(os.path.join(base or K_IMP, f)), -50))

def build_events():
    # Молоток по гвоздю: металлический «тинк» + удар по доске; 4 варианта.
    for i in range(4):
        tink = resample_pitch(K(f'impactMetal_light_00{i}.ogg'), 3 + i * .7)
        thud = resample_pitch(K(f'impactWood_medium_00{i}.ogg'), -1.5)
        out = layer([(0, tink, -3), (0.004, thud, 0)], 0.6)
        emit(f'World/HammerNail{i+1}.ogg', fades(biquad(out, 'hp', 90), 0.001, 0.15), -15)
        credit(f'World/HammerNail{i+1}.ogg', 'kenney impactMetal_light + impactWood_medium (layered)', 'Kenney (kenney.nl)', 'CC0', 'https://kenney.nl/assets/impact-sounds')
    # E2: героя столкнули в подвал, сверху заколачивают — глухие неровные удары через пол (петля 9 с).
    rng = np.random.default_rng(5)
    parts, t = [], 0.4
    while t < 8.4:
        k = rng.integers(0, 4)
        x = resample_pitch(K(f'impactWood_heavy_00{k}.ogg'), rng.uniform(-3, -1))
        parts.append((t, x, rng.uniform(-4, 0)))
        t += rng.choice([0.42, 0.45, 0.5, 1.6, 0.48, 2.2])
    above = biquad(biquad(layer(parts, 9.0), 'lp', 650), 'lowshelf', 120, db=4)
    emit('Horror/NailingAbove.ogg', loopify(above, 0.3), -18)
    credit('Horror/NailingAbove.ogg', 'kenney impactWood_heavy (sequenced, filtered)', 'Kenney (kenney.nl)', 'CC0', 'https://kenney.nl/assets/impact-sounds')
    # Стук соседа костяшками во входную дверь: три удара.
    # Один удар: NeighborVisitSequence сам стучит три раза (по взмаху руки).
    parts = [(0.0, resample_pitch(K('impactWood_light_001.ogg'), -2), 0)]
    emit('World/DoorKnock.ogg', fades(biquad(layer(parts, 0.4), 'lowshelf', 200, db=3), 0.001, 0.12), -16)
    credit('World/DoorKnock.ogg', 'kenney impactWood_light (sequenced)', 'Kenney (kenney.nl)', 'CC0', 'https://kenney.nl/assets/impact-sounds')
    # Сорванная дверь падает на пол — отличный от удара-срыва звук.
    land = layer([(0, resample_pitch(K('impactWood_heavy_003.ogg'), -4), 0), (0.05, resample_pitch(K('impactPlank_medium_002.ogg'), -3), -3),
                  (0.16, resample_pitch(K('impactWood_heavy_001.ogg'), -5), -8)], 1.2)
    emit('World/DoorLanding.ogg', fades(land, 0.001, 0.3), -14)
    credit('World/DoorLanding.ogg', 'kenney impactWood_heavy + impactPlank_medium (layered)', 'Kenney (kenney.nl)', 'CC0', 'https://kenney.nl/assets/impact-sounds')
    # Мигание лампы: короткая электрическая вспышка с треском.
    import synth
    n = int(0.55 * SR); t = np.arange(n) / SR
    buzz = synth.electric_buzz(1.0)[:n] * (np.exp(-t / 0.25) * (0.6 + 0.4 * (np.sin(2 * np.pi * 23 * t) > 0)))[:, None]
    crack = (rng.random(n) < 0.004) * rng.standard_normal(n) * np.exp(-t / 0.2)
    flick = buzz + biquad(crack[:, None].astype(np.float32), 'bp', 2500, 0.8) * 0.8
    emit('World/LampFlicker.ogg', fades(flick, 0.001, 0.08), -20, to_mono=True)
    credit('World/LampFlicker.ogg', 'synthesized (tools/synth.py)', 'Knocks from Beneath', 'project-original', '-')

AG = os.path.join(DL, 'agent')
FS = 'https://freesound.org/people/{}/sounds/{}/'
def fscredit(rel, user, sid, title):
    credit(rel, f'freesound #{sid} "{title}" (HQ preview)', user, 'CC0', FS.format(user, sid))
def ogacredit(rel, slug, author, title):
    credit(rel, f'opengameart "{title}"', author, 'CC0', 'https://opengameart.org/content/' + slug)

def env(a, win=0.02):
    x = np.abs(mono(a)[:, 0]); n = int(win * SR)
    return np.convolve(x, np.ones(n) / n, mode='same')

def onsets(a, min_gap=0.25, thresh_db=-24):
    e = env(a); top = e.max(); th = top * 10 ** (thresh_db / 20)
    idx, last = [], -1e9
    for i in range(1, len(e) - 1):
        if e[i] > th and e[i] >= e[i - 1] and e[i] >= e[i + 1] and (i - last) / SR > min_gap:
            idx.append(i); last = i
    return idx

def loudest_window(a, sec):
    n = int(sec * SR); e = env(a, 0.1)
    if len(a) <= n: return a
    c = np.cumsum(np.insert(e, 0, 0)); best = int(np.argmax(c[n:] - c[:-n]))
    return a[best:best + n]

def build_agent():
    # Испуганное дыхание героя после пугалок.
    b1 = load(os.path.join(AG, '01_fs_554906', 'Male_Breath_Scared_Frozen_Loop_Stereo__fs554906_hq-preview.ogg'))
    emit('Horror/Breath1.ogg', fades(trim(b1, -55), 0.05, 0.6), -24, to_mono=True)
    fscredit('Horror/Breath1.ogg', 'Nox_Sound', 554906, 'Male Breath Scared Frozen Loop')
    b2 = load(os.path.join(AG, '01_fs_386573', 'heavy_breathing_1__fs386573_hq-preview.ogg'))
    emit('Horror/Breath2.ogg', fades(trim(b2, -55)[: int(5.5 * SR)], 0.05, 0.8), -23, to_mono=True)
    fscredit('Horror/Breath2.ogg', 'ShaneF91', 386573, 'heavy breathing 1')
    # Скрипы дома.
    i = 1
    for d, f, user, sid, title in [('10_fs_683416', 'House_Creak_2__fs683416_hq-preview.ogg', 'SholeColtis', 683416, 'House Creak 2'),
                                   ('10_fs_590169', 'Creaking_in_Room_6__fs590169_hq-preview.ogg', 'Greg_Surr', 590169, 'Creaking in Room 6')]:
        a = trim(load(os.path.join(AG, d, f)), -55)
        emit(f'World/House/Creak{i}.ogg', fades(a, 0.02, 0.3), -22, to_mono=True); fscredit(f'World/House/Creak{i}.ogg', user, sid, title); i += 1
    bed = load(os.path.join(AG, '07_fs_570599', 'Foley_Natural_Wood_Bed_Creaks_Mono__fs570599_hq-preview.ogg'))
    for k, o in enumerate(onsets(bed, 3.0, -14)[:3]):
        seg = bed[max(0, o - int(0.1 * SR)): o + int(2.2 * SR)]
        rel = f'World/House/Creak{i}.ogg'; emit(rel, fades(biquad(resample_pitch(seg, -3), 'lp', 4000), 0.03, 0.4), -23, to_mono=True)
        fscredit(rel, 'Nox_Sound', 570599, 'Foley Natural Wood Bed Creaks (pitched down)'); i += 1
    # Гром вдали.
    t1 = load(os.path.join(AG, '04_fs_238743', 'Distant_thunder_in_suburban_area__fs238743_hq-preview.ogg'))
    t2 = load(os.path.join(AG, '04_fs_347562', 'Distant_Thunder_Clap_Gentle__fs347562_hq-preview.ogg'))
    emit('Ambience/Thunder1.ogg', fades(loudest_window(t1, 12), 0.3, 2.5), -20, q=5); fscredit('Ambience/Thunder1.ogg', 'csengeri', 238743, 'Distant thunder in suburban area')
    emit('Ambience/Thunder2.ogg', fades(trim(t2, -50)[: int(14 * SR)], 0.1, 3.0), -20, q=5); fscredit('Ambience/Thunder2.ogg', 'mikala_oidua', 347562, 'Distant Thunder Clap Gentle')
    # Собака вдали.
    d1 = load(os.path.join(AG, '12_fs_85574', 'Ext-amb_late-fall-night_Distant-Dog-going-bizerk-fallcrickets__fs85574_hq-preview.ogg'))
    d2 = load(os.path.join(AG, '12_fs_820267', 'Distant_Dogs__fs820267_hq-preview.ogg'))
    emit('Ambience/Dog1.ogg', fades(biquad(loudest_window(d1, 6), 'hp', 150), 0.4, 1.2), -24, q=5); fscredit('Ambience/Dog1.ogg', 'nmscher', 85574, 'Ext-amb late-fall-night Distant Dog')
    emit('Ambience/Dog2.ogg', fades(biquad(loudest_window(d2, 6), 'hp', 150), 0.4, 1.2), -24, q=5); fscredit('Ambience/Dog2.ogg', 'IENBA', 820267, 'Distant Dogs')
    # Молоток по гвоздю — живая запись (заменяет собранный из Kenney вариант).
    hm = load(os.path.join(AG, '06_fs_118341', 'JM_TOOLS_Hammer_01_-_Nail_in_Wood_-_In_a_Room__fs118341_hq-preview.ogg'))
    hits = onsets(hm, 0.3, -12)
    hits = sorted(hits, key=lambda o: -env(hm)[o])[:4]
    for k, o in enumerate(sorted(hits)):
        seg = hm[max(0, o - int(0.01 * SR)): o + int(0.45 * SR)]
        rel = f'World/HammerNail{k+1}.ogg'; emit(rel, fades(seg, 0.002, 0.15), -15, to_mono=True)
        fscredit(rel, 'Julien_Matthey', 118341, 'JM TOOLS Hammer 01 - Nail in Wood - In a Room')
    # Двери — записи qubodup (заменяют Kenney Open/Close).
    dd = os.path.join(AG, '05_oga_door-open-door-close-set', 'extracted', 'qubodup-DoorSet', 'flac')
    for k in range(4):
        rel = f'World/Doors/Close{k+1}.ogg'; emit(rel, fades(trim(load(os.path.join(dd, f'qubodup-DoorClose0{k+1}.flac')), -55), 0.002, 0.1), -17, to_mono=True)
        ogacredit(rel, 'door-open-door-close-set', 'qubodup', f'DoorClose0{k+1}')
    for k in range(4):
        # Открытие: щелчок ручки + начало хода створки, без длинного хвоста (скрип даёт Door при движении).
        a = trim(load(os.path.join(dd, f'qubodup-DoorOpen0{k+1}.flac')), -55)[: int(0.9 * SR)]
        rel = f'World/Doors/Open{k+1}.ogg'; emit(rel, fades(biquad(a, 'hp', 70), 0.002, 0.25), -22, to_mono=True)
        ogacredit(rel, 'door-open-door-close-set', 'qubodup', f'DoorOpen0{k+1}')
    # Плита: ручка + розжиг газа; отключение света: щелчок автомата.
    knob = mono(trim(load(os.path.join(AG, '08_oga_stove-switch', 'turn_stove_on_01_0.ogg')), -50))
    gas = mono(trim(load(os.path.join(AG, '08_fs_751151', 'Gas_Stove_Ignition_3__fs751151_hq-preview.ogg')), -50))[: int(3.2 * SR)]
    emit('World/StoveIgnite.ogg', fades(layer([(0, knob, 0), (0.25, gas, -3)], 3.5), 0.002, 0.6), -19)
    ogacredit('World/StoveIgnite.ogg', 'stove-switch', 'TinyWorlds', 'turn stove on 01'); fscredit('World/StoveIgnite.ogg', 'C-V', 751151, 'Gas Stove Ignition 3')
    brk = load(os.path.join(AG, '09_oga_sfx-circuit-breaker', 'extracted', 'switch off.wav'))
    emit('World/BreakerOff.ogg', fades(trim(brk, -55), 0.002, 0.2), -16, to_mono=True)
    ogacredit('World/BreakerOff.ogg', 'sfx-circuit-breaker', 'CleytonKauffman', 'switch off')
    pl = load(os.path.join(AG, '08_fs_785256', 'Ausr_umen_und_Aufdecken_von_Tassen_und_Tellern_fu_r_Buffet_-__fs785256_hq-preview.ogg'))
    emit('World/PlateSetDown.ogg', fades(loudest_window(pl, 1.4), 0.01, 0.3), -20, to_mono=True)
    fscredit('World/PlateSetDown.ogg', 'Marschal', 785256, 'Aufraeumen und Aufdecken von Tassen und Tellern')

if __name__ == '__main__':
    # agent — последним: живые записи (молоток, двери) заменяют собранные из Kenney варианты.
    parts = sys.argv[1:] or ['ui', 'foley', 'world', 'ambience', 'music', 'horror', 'events', 'agent']
    for p in parts: globals()['build_' + p]()
    print('\n'.join(report))
    cpath = os.path.join(SP, 'build', 'credits.json')
    old = json.load(open(cpath, encoding='utf-8')) if os.path.exists(cpath) else []
    keep = [c for c in old if c['file'] not in {x['file'] for x in credits}]
    json.dump(keep + credits, open(cpath, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
