"""
Simulacija subjektivnog ulaza za umor (runda 20, stavka 20.2, docs/features/subjective-fatigue.md).

Pitanje iz plana: da li novi signal popravlja pogađanje deload-a, i da li je nezavisan od postojećih
(runda 15 je pokazala da „dva signala" mogu biti jedan uzrok brojan dvaput).

Osnova je model iz docs/simulations/fatigue_signal_noise.py: nivo snage po nedelji ima sd 2.47%, čitanje snage
(razlika dve nedelje) sd oko 3.5%, vežbač drži zadat broj ponavljanja i iskreno prijavljuje rezervu (ponašanje A),
greška procene RIR-a sd 0.22. Ocena je ista kao u FatigueEvaluator.Score (RIR 0.35, otkazi 0.25, snaga 0.25,
volumen 0.15), uz potvrdu pada snage iz runde 15, prag 0.60.

Šta je novo, i šta je PRETPOSTAVKA (nijedan od ovih brojeva nije izmeren u aplikaciji, jer aplikacija
subjektivnu ocenu još ne prikuplja): ocena umora posle treninga, 1-5, uzima se kao prosek nedelje. Od njenog
nivoa se ne zavisi, jer ljudi različito koriste skalu; čita se **porast prosečne ocene nedelje u odnosu na
prosek prethodnih nedelja istog bloka**, kao što se prate subjektivne mere umora u praksi (promena u odnosu na
sopstvenu osnovu). Prva nedelja nema osnovu, pa ne doprinosi.

Model ocene u nedelji w:
    prosek = OSNOVA + TRAG * T_w + MOOD_SD * (-RHO * z_w + sqrt(1 - RHO^2) * e_w)
gde je T_w stvarni umor (0 kad snaga ne pada, inače min(1, -trend / 3%), od 2. nedelje), z_w standardizovan
slab/dobar dan (isti onaj koji pomera nivo snage, pa i RIR), a e_w nezavisan šum raspoloženja. RHO je ono što se
ne zna i zato se menja: 0 znači da ocena ne zna ništa o slabom danu, 0.9 da je gotovo isto što i on. Slab dan
spušta snagu i RIR (već u modelu), a ovde diže i ocenu - to je tačno oblik „jedan uzrok, brojan dvaput".

Pokretanje: python3 docs/simulations/subjective_fatigue.py
"""
import math
import random
import sys

RATE = 0.024
LEVEL_SD = 0.0247
MISJUDGEMENT_SD = 0.22

BASELINE_RATING = 2.0      # uobičajena ocena sveže nedelje (skala 1-5)
MOOD_SD = 0.5              # sd prosečne ocene nedelje oko njenog očekivanja (poeni)
RISE_AT_FULL = 1.0         # porast od jednog poena nad sopstvenom osnovom je pun signal


def norm(value, floor, ceiling):
    return 0.0 if ceiling <= floor else min(1.0, max(0.0, (value - floor) / (ceiling - floor)))


def score(rir_deviation, strength_term, volume_share, subjective_term, subjective_weight):
    return (0.35 * norm(-rir_deviation, 0, 1)
            + 0.25 * norm(strength_term, 0, 0.05)
            + 0.15 * norm(volume_share, 0.8, 1.0)
            + subjective_weight * subjective_term)


def confirmed_strength(reading, previous):
    if reading is None or previous is None or previous > -0.01:
        return 0.0
    return max(0.0, -reading)


def simulate(trend, volume_share, weeks_acting, *, design, rho, tracking, threshold=0.60,
             blocks=30000, seed=11):
    """design: ('none',) | ('add', weight) | ('gated', weight) - gated: only when the previous week also rose.

    Returns (share of blocks with a deload, mean correlation inputs) for the independence check.
    """
    rnd = random.Random(seed)
    last_week = max(weeks_acting)
    hits = 0
    # za proveru nezavisnosti: parovi (član RIR-a, član ocene) i (čitanje snage, član ocene) po nedelji
    rir_terms, sub_terms, strength_reads = [], [], []
    for _ in range(blocks):
        last_level = rnd.gauss(0, LEVEL_SD)
        previous_reading = None
        ratings = []
        previous_rise = None
        hit = False
        for week in range(1, last_week + 1):
            level = rnd.gauss(0, LEVEL_SD)
            z = level / LEVEL_SD
            change = level - last_level
            last_level = level
            reading = None if week == 1 else trend + change
            deviation = change / RATE + rnd.gauss(0, MISJUDGEMENT_SD) + (trend / RATE if trend < 0 else 0)

            true_fatigue = 0.0 if (trend >= 0 or week == 1) else min(1.0, -trend / 0.03)
            mean_rating = (BASELINE_RATING + tracking * true_fatigue
                           + MOOD_SD * (-rho * z + math.sqrt(1 - rho * rho) * rnd.gauss(0, 1)))
            if ratings:
                rise = mean_rating - sum(ratings) / len(ratings)
            else:
                rise = None
            ratings.append(mean_rating)

            sub_term = 0.0
            weight = 0.0
            if design[0] != 'none' and rise is not None:
                weight = design[1]
                sub_term = norm(rise, 0.0, RISE_AT_FULL)
                if design[0] == 'gated':
                    sub_term = sub_term if (previous_rise is not None and previous_rise >= 0.5 * RISE_AT_FULL) else 0.0
            previous_rise = rise

            term = confirmed_strength(reading, previous_reading)
            previous_reading = reading
            if week > 1:
                rir_terms.append(norm(-deviation, 0, 1))
                sub_terms.append(norm(rise, 0.0, RISE_AT_FULL) if rise is not None else 0.0)
                strength_reads.append(max(0.0, -reading))
            if week in weeks_acting and score(deviation, term, volume_share, sub_term, weight) >= threshold:
                hit = True
        hits += hit
    return hits / blocks, rir_terms, sub_terms, strength_reads


def pearson(xs, ys):
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    cov = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    vx = sum((x - mx) ** 2 for x in xs)
    vy = sum((y - my) ** 2 for y in ys)
    return 0.0 if vx == 0 or vy == 0 else cov / math.sqrt(vx * vy)


STRUCTURES = {"periodizovan, 6 nedelja": {2, 3, 4}, "ravan, 4 nedelje": {2}}
DESIGNS = [
    ("bez ocene (danas)", ('none',)),
    ("dodaje se, težina 0.20", ('add', 0.20)),
    ("dodaje se, težina 0.25", ('add', 0.25)),
    ("sa potvrdom (prethodna nedelja je takođe porasla), 0.25", ('gated', 0.25)),
    ("sa potvrdom, težina 0.20", ('gated', 0.20)),
]
SCENARIOS = (("+1%, MAV (0.75)", 0.01, 0.75), ("+1%, MRV (1.0)", 0.01, 1.0),
             ("-2%, MAV", -0.02, 0.75), ("-3%, MRV", -0.03, 1.0))


def table(rho, tracking, threshold):
    for structure, weeks in STRUCTURES.items():
        print(f"\n### {structure}, prag {threshold:.2f}, RHO {rho}, trag {tracking}")
        print("| Dizajn | " + " | ".join(name for name, *_ in SCENARIOS) + " |")
        print("|---|" + "---|" * len(SCENARIOS))
        for label, design in DESIGNS:
            cells = [simulate(trend, volume, weeks, design=design, rho=rho, tracking=tracking,
                              threshold=threshold)[0] for _, trend, volume in SCENARIOS]
            print(f"| {label} | " + " | ".join(f"{cell:.1%}" for cell in cells) + " |")


def independence(rho, tracking):
    _, rir_terms, sub_terms, strength_reads = simulate(
        0.01, 0.75, STRUCTURES["periodizovan, 6 nedelja"], design=('add', 0.25), rho=rho,
        tracking=tracking, blocks=20000)
    print(f"RHO {rho}: korelacija člana ocene sa članom RIR-a {pearson(rir_terms, sub_terms):+.2f}, "
          f"sa padom snage {pearson(strength_reads, sub_terms):+.2f} "
          f"(nedelje bez pravog umora, slab dan je jedini zajednički uzrok)")


if __name__ == "__main__":
    tracking = 1.5
    if "--independence" in sys.argv:
        for rho in (0.0, 0.3, 0.6, 0.9):
            independence(rho, tracking)
        raise SystemExit
    for rho in (0.0, 0.5, 0.9):
        table(rho, tracking, 0.60)
    print("\n## slabo praćenje stvarnog umora (trag 0.75), RHO 0.5")
    table(0.5, 0.75, 0.60)
    print("\n## napredni nivo, prag 0.50, RHO 0.5")
    table(0.5, tracking, 0.50)
