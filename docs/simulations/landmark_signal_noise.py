"""
Simulacija granica volumena pod šumom čitanja snage, sa stvarnom strukturom blokova
(docs/features/landmark-signal-noise.md, ispravka posle review-a #94).

Model šuma je isti kao u fatigue_signal_noise.py: nivo snage po nedelji ima sd 2.47%, pa čitanje
(razlika dve uzastopne nedelje) ima sd oko 3.5%, a dva uzastopna čitanja dele nedelju u sredini.

Pravilo Adjust je doslovan prenos VolumeAdaptation.Adjust: korak 1, lutanje +-50% od seed-a, pojas
najmanje 2, cilj najviše 15% iznad svog mesta u pojasu. Vežbač trenira na MAV-u (ili na MEV-u u
tabeli za minimum), RIR na propisu, otkazi 5%.

Struktura: čitanja se računaju unutar bloka (ComparableWeek), pa prva nedelja bloka nema čitanje, a
deload nema ni čitanje ni odluku. Ravan blok: 4 nedelje, deload u 4. Linearan: 6 nedelja, deload
u 6. Godina je 52 nedelje uzastopnih blokova istog oblika. "dostupnost 2/3": svako čitanje postoji
sa verovatnoćom 2/3 (runda 11 je izmerila da čitanje ima oko dve trećine nedelja).

Pokretanje: python3 docs/simulations/landmark_signal_noise.py
"""
import math
import random

LEVEL_SD = 0.0247
SEED = (10, 16, 22)  # grudi: MEV, MAV, MRV
THRESHOLD = 0.01


def clamp_to_seed(value, seed):
    lower = math.ceil(seed * 0.5)
    upper = math.floor(seed * 1.5)
    return min(max(value, max(1, lower)), max(1, upper))


def round_away(value):
    return int(math.floor(abs(value) + 0.5)) * (1 if value >= 0 else -1)


def clamp_target(value, mev, mrv, seed):
    seed_mev, seed_mav, seed_mrv = seed
    band = seed_mrv - seed_mev
    share = (seed_mav - seed_mev) / band if band > 0 else 0.5
    allowed = min(1.0, share + 0.15)
    ceiling = mev + round_away((mrv - mev) * allowed)
    return min(max(value, mev + 1), min(max(ceiling, mev + 1), mrv - 1))


def adjust(current, seed, performed, verdict):
    """verdict: 'progress' | 'decline' | 'flat' | None (ćutanje)."""
    mev, mav, mrv = current
    rir_deviation, failure_share = 0.0, 0.05
    declined = verdict == "decline"
    showed_fatigue = rir_deviation <= -1 or failure_share >= 0.25 or declined
    reps_to_spare = rir_deviation >= 1 and failure_share <= 0.125
    if performed >= current[2] * 0.9 and reps_to_spare:
        mrv += 1
    elif performed >= current[0] and showed_fatigue:
        mrv -= 1
    if performed >= current[1] * 0.9:
        if declined:
            mav -= 1
        elif verdict == "flat":
            mav += 1
    if performed <= current[0]:
        if verdict == "progress":
            mev -= 1
        elif declined:
            mev += 1
    mev, mav, mrv = clamp_to_seed(mev, seed[0]), clamp_to_seed(mav, seed[1]), clamp_to_seed(mrv, seed[2])
    if mrv - mev < 2:
        lifted = clamp_to_seed(mev + 2, seed[2])
        if lifted - mev >= 2:
            mrv = lifted
        else:
            mev = mrv - 2
    safe_mev = max(1, mev)
    safe_mrv = max(safe_mev + 2, mrv)
    return safe_mev, clamp_target(mav, safe_mev, safe_mrv, seed), safe_mrv


def classify(change, t=THRESHOLD):
    return "progress" if change >= t else "decline" if change <= -t else "flat"


# --- pravila: (tekuće čitanje, prethodno čitanje) -> presuda ili None ---

def rule_one(now, before):
    return None if now is None else classify(now)


def rule_agree(now, before):
    if now is None or before is None:
        return None
    a, b = classify(now), classify(before)
    return a if a == b else None


def rule_mean(t):
    def rule(now, before):
        if now is None or before is None:
            return None
        return classify((now + before) / 2, t)
    return rule


def rule_agree_decline_mean_rest(t):
    """Pad traži da se oba čitanja slože (plafon se spušta samo na potvrđen pad); napredak i
    'stoji' sude o proseku dva čitanja, pa vežbač koji stoji stvarno dobija 'stoji'."""
    def rule(now, before):
        if now is None or before is None:
            return None
        if classify(now) == "decline" and classify(before) == "decline":
            return "decline"
        mean = (now + before) / 2
        if mean >= t:
            return "progress"
        if abs(mean) < t:
            return "flat"
        return None
    return rule


RULES = {
    "jedno čitanje (pre #94)": rule_one,
    "dva se slažu (#94)": rule_agree,
    "prosek dva, prag 1%": rule_mean(0.01),
    "prosek dva, prag 2.5%": rule_mean(0.025),
    "pad: dva se slažu; ostalo: prosek, 1%": rule_agree_decline_mean_rest(0.01),
}

SHAPES = {"ravan, 4 nedelje": 4, "linearan, 6 nedelja": 6, "bez strukture (model #94)": None}


def run(rule, trend, shape_weeks, availability=1.0, at="mav", years=1, seeds=400):
    sums = [0.0, 0.0, 0.0]
    decisions = 0
    declines = 0
    for s in range(seeds):
        rnd = random.Random(s)
        current = SEED
        week_in_block = 0
        last_level = rnd.gauss(0, LEVEL_SD)
        previous = None
        for _ in range(52 * years):
            week_in_block += 1
            level = rnd.gauss(0, LEVEL_SD)
            change = level - last_level
            last_level = level
            if shape_weeks is not None and week_in_block == shape_weeks:
                # deload: ni čitanja ni odluke, a sledeći blok kreće ispočetka
                week_in_block = 0
                previous = None
                continue
            first_week = shape_weeks is not None and week_in_block == 1
            reading = None if first_week or rnd.random() > availability else trend + change
            verdict = rule(reading, previous)
            performed = current[1] if at == "mav" else current[0]
            current = adjust(current, SEED, performed, verdict)
            decisions += verdict is not None
            declines += verdict == "decline"
            previous = reading
        for i in range(3):
            sums[i] += current[i]
    n = seeds
    return tuple(round(x / n, 1) for x in sums), decisions / n / years, declines / n / years


if __name__ == "__main__":
    for availability in (1.0, 2 / 3):
        print(f"\n## Na MAV-u, godinu dana, dostupnost čitanja {availability:.0%} (seme 10/16/22)")
        print("| Struktura | Pravilo | +1%/ned. MAV / MRV | stoji | -2%/ned. | odluka godišnje (+1%) | padova godišnje (+1%) |")
        print("|---|---|---|---|---|---|---|")
        for shape, weeks in SHAPES.items():
            for name, rule in RULES.items():
                up, d_up, f_up = run(rule, 0.01, weeks, availability)
                flat, _, _ = run(rule, 0.0, weeks, availability)
                down, _, _ = run(rule, -0.02, weeks, availability)
                print(f"| {shape} | {name} | {up[1]} / {up[2]} | {flat[1]} / {flat[2]} | {down[1]} / {down[2]} "
                      f"| {d_up:.1f} | {f_up:.1f} |")
    print("\n## Na MEV-u (minimum), linearan blok, dostupnost 100%: MEV posle godinu dana")
    print("| Pravilo | +1%/ned. | stoji | -2%/ned. |")
    print("|---|---|---|---|")
    for name, rule in RULES.items():
        cells = [run(rule, trend, 6, 1.0, at="mev")[0][0] for trend in (0.01, 0.0, -0.02)]
        print(f"| {name} | " + " | ".join(str(c) for c in cells) + " |")
