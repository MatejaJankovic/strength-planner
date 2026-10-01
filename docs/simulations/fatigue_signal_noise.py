"""
Simulacija lažnog i stvarnog deload-a iz ocene umora (runda 15, docs/features/fatigue-signal-noise.md).

Model šuma je onaj iz granica volumena (runda 14): nivo snage po nedelji ima sd 2.47%, pa čitanje
snage (razlika dve uzastopne nedelje) ima sd oko 3.5%.

Ponašanje (A): vežbač drži zadat broj ponavljanja i iskreno prijavljuje rezervu, pa slab dan spušta
i RIR (jedno ponavljanje na svakih 2.4% snage, Epley za 8-12) i procenu. Greška procene RIR-a nad
nedeljom ima sd 0.22. Stvaran pad snage se oseti i u RIR-u, jer opterećenje dolazi iz prethodne
nedelje; napredak ne, jer ga progresija prati.

Ponašanje (B): vežbač staje kad oseti ciljni RIR, pa se RIR pomera samo greškom procene.

Ocena je ista kao u FatigueEvaluator.Score (hipertrofija, ciljni RIR 1, bez otkaza).

Struktura bloka: čitanje snage postoji od 2. nedelje. Ocena nedelje w deluje na nedelju w + 1, pa
je upotrebljiva samo ako w + 1 nije već deload. Ravan blok (4 nedelje, deload u 4.): ocena se
računa posle 2. i 3. nedelje, a deluje samo ona posle 2. Periodizovan blok (6 nedelja, deload u
6.): deluju ocene posle 2., 3. i 4. nedelje. "Pet nedelja" je model bez strukture, kojim je
izbor pravila prvo napravljen.

Pokretanje: python3 docs/simulations/fatigue_signal_noise.py (runda 15), sa --g6 za odbačeni nalaz G6.
"""
import random
import sys

RATE = 0.024
LEVEL_SD = 0.0247
MISJUDGEMENT_SD = 0.22


def norm(value, floor, ceiling):
    return 0.0 if ceiling <= floor else min(1.0, max(0.0, (value - floor) / (ceiling - floor)))


def score(rir_deviation, strength_term, volume_share):
    return (0.35 * norm(-rir_deviation, 0, 1)
            + 0.25 * norm(strength_term, 0, 0.05)
            + 0.15 * norm(volume_share, 0.8, 1.0))


def strength_term(rule, reading, previous):
    if reading is None:
        return 0.0
    if rule == "one":
        return max(0.0, -reading)
    confirmed = previous is not None and previous <= -0.01
    return max(0.0, -reading) if confirmed else 0.0


def deloaded_share(rule, trend, volume_share, actionable_weeks, threshold=0.60, behaviour="A",
                   blocks=40000, seed=7):
    rnd = random.Random(seed)
    hits = 0
    last_week = max(actionable_weeks)
    for _ in range(blocks):
        last_level = rnd.gauss(0, LEVEL_SD)
        previous_reading = None
        hit = False
        for week in range(1, last_week + 1):
            level = rnd.gauss(0, LEVEL_SD)
            change = level - last_level
            last_level = level
            reading = None if week == 1 else trend + change
            if behaviour == "A":
                deviation = change / RATE + rnd.gauss(0, MISJUDGEMENT_SD) + (trend / RATE if trend < 0 else 0)
            else:
                deviation = rnd.gauss(0, MISJUDGEMENT_SD)
            term = strength_term(rule, reading, previous_reading)
            if week in actionable_weeks and score(deviation, term, volume_share) >= threshold:
                hit = True
            previous_reading = reading
        hits += hit
    return hits / blocks


STRUCTURES = {
    "pet nedelja (model)": set(range(1, 6)),
    "periodizovan, 6 nedelja": {2, 3, 4},
    "ravan, 4 nedelje": {2},
}

def g6_table():
    """Nalaz G6, odbačen: da li signal volumena treba da broji samo serije preko propisa.
    Nedelja odrađena po predlogu bi tada imala udeo 0.8 (dno opsega). Udeli su oni koje predlog
    (posle balansiranja) ugrađenih šablona stvarno dostiže: napredni najviše 0.98, srednji 1.0.
    Svaka nedelja u kojoj ocena deluje stavlja se na taj najveći udeo, pa je ovo gornja granica
    koristi od G6."""
    weeks = STRUCTURES["periodizovan, 6 nedelja"]
    print("\n## G6 (odbačen): periodizovan blok, ponašanje (A), sa potvrdom pada snage")
    print("| Nivo (prag) | Udeo predloga | Signal | +1%/ned. (lažni) | -3%/ned. (uhvaćen) |")
    print("|---|---|---|---|---|")
    for threshold, label, planned in ((0.50, "napredni (0.50)", 0.98), (0.60, "srednji (0.60)", 1.0)):
        for volume, name in ((planned, "udeo prema MRV-u (zadržano)"), (0.8, "preko propisa (odbačeno)")):
            cells = [deloaded_share("confirmed", trend, volume, weeks, threshold) for trend in (0.01, -0.03)]
            print(f"| {label} | {planned} | {name} | " + " | ".join(f"{cell:.1%}" for cell in cells) + " |")


if __name__ == "__main__":
    if "--g6" in sys.argv:
        g6_table()
        raise SystemExit
    for threshold, label in ((0.60, "srednji nivo, prag 0.60"), (0.50, "napredni nivo, prag 0.50")):
        print(f"\n## {label}, ponašanje (A): udeo blokova sa deload-om iz ocene umora")
        print("| Struktura | Pravilo | +1%/ned., MAV | +1%, MRV | -2%/ned., MAV | -3%, MRV |")
        print("|---|---|---|---|---|---|")
        for name, weeks in STRUCTURES.items():
            for rule in ("one", "confirmed"):
                cells = [deloaded_share(rule, trend, volume, weeks, threshold)
                         for trend, volume in ((0.01, 0.75), (0.01, 1.0), (-0.02, 0.75), (-0.03, 1.0))]
                print(f"| {name} | {'jedno čitanje' if rule == 'one' else 'potvrda'} | "
                      + " | ".join(f"{cell:.1%}" for cell in cells) + " |")
    print("\n## ponašanje (B), srednji nivo, pet nedelja, jedno čitanje")
    print(" | ".join(f"{deloaded_share('one', 0.01, v, STRUCTURES['pet nedelja (model)'], behaviour='B'):.1%}"
                     for v in (0.75, 1.0)))
