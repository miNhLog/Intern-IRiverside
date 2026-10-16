"""Sinh synthetic dataset 5000 mau cho bai toan goi y Top-3 goi tiec cuoi (WSM Learn-to-Rank).
Phan phoi thuc te: Budget chuan 150tr-600tr, TableCount 15-90 tap trung 25-45,
DesiredStyle theo 7 style, Shift Ca trua 40% / Ca toi 60%.
"""
import json
import os
import random
from pathlib import Path

import numpy as np
import pandas as pd

HERE = Path(__file__).resolve().parent
CATALOG_PATH = HERE / "raw_catalog.json"
OUTPUT_CSV = HERE / "wedding_dataset_5k.csv"

N_SAMPLES = 5000
RANDOM_SEED = 257


def load_catalog():
    with open(CATALOG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


def sample_table_count(rng: np.random.Generator) -> int:
    # Tap trung 25-45: hon hop triangular + uniform de phu bien 15-90
    if rng.random() < 0.70:
        value = int(round(rng.triangular(15, 35, 90)))
        # Day khoi ve cum 25-45
        if value > 45 and rng.random() < 0.55:
            value = int(rng.integers(25, 46))
        if value < 25 and rng.random() < 0.55:
            value = int(rng.integers(25, 46))
    else:
        value = int(rng.integers(15, 91))
    return int(np.clip(value, 15, 90))


def style_score(desired: str, decor_style: str, groups: dict) -> float:
    if desired == decor_style:
        return 1.0
    for _group, members in groups.items():
        if desired in members and decor_style in members:
            return 0.5
    return 0.0


def main() -> None:
    rng = np.random.default_rng(RANDOM_SEED)
    random.seed(RANDOM_SEED)
    catalog = load_catalog()
    halls = catalog["halls"]
    menus = catalog["menus"]
    decors = catalog["decors"]
    services = catalog["services"]
    groups = catalog["style_groups"]
    styles = [d["style"] for d in decors]
    service_ids = [s["service_id"] for s in services]
    service_price = {s["service_id"]: s["price"] for s in services}

    rows = []
    for sample_id in range(1, N_SAMPLES + 1):
        # Budget chuan 150tr-600tr (normal mean 350tr, std 110tr, clip)
        budget = float(np.clip(rng.normal(350_000_000, 110_000_000), 150_000_000, 600_000_000))
        budget = round(budget / 1_000_000) * 1_000_000

        table_count = sample_table_count(rng)
        guest_count = int(table_count * 10 + int(rng.integers(-8, 9)))
        guest_count = max(80, guest_count)
        desired_style = str(rng.choice(styles))
        shift = "Ca toi" if rng.random() < 0.60 else "Ca trua"

        # Dich vu uu tien: 0-3 dich vu
        n_priority = int(rng.choice([0, 1, 2, 3], p=[0.15, 0.35, 0.35, 0.15]))
        priority_services = sorted(random.sample(service_ids, k=n_priority)) if n_priority > 0 else []

        # Chon sanh ground-truth: uu tien sanh vua suc chua table_count, co nhieu nhieu
        feasible = [h for h in halls if h["min_tables"] <= table_count <= h["max_tables"]]
        if feasible and rng.random() < 0.85:
            hall = feasible[int(rng.integers(0, len(feasible)))]
        else:
            hall = halls[int(rng.integers(0, len(halls)))]
        menu = menus[int(rng.integers(0, len(menus)))]

        # Decor ground-truth: 65% khop style, 20% cung nhom, 15% ngau nhien
        roll = rng.random()
        if roll < 0.65:
            decor = next(d for d in decors if d["style"] == desired_style)
        elif roll < 0.85:
            same_group = None
            for _g, members in groups.items():
                if desired_style in members:
                    same_group = [m for m in members if m != desired_style]
                    break
            target_style = str(rng.choice(same_group)) if same_group else desired_style
            decor = next(d for d in decors if d["style"] == target_style)
        else:
            decor = decors[int(rng.integers(0, len(decors)))]

        # Dich vu kem theo combo: giu lai 70-100% priority + them 0-2 dich vu khac
        included = set(priority_services)
        for svc in list(included):
            if rng.random() < 0.15:
                included.discard(svc)
        extras = [s for s in service_ids if s not in included]
        for extra in extras:
            if rng.random() < 0.18:
                included.add(extra)
        included_services = sorted(included)
        services_price = sum(service_price[s] for s in included_services)

        menu_total = menu["price_per_table"] * table_count
        total_cost = menu_total + hall["base_price"] + decor["price"] + services_price

        # 4 tieu chi goc (khong nhieu) -> Satisfaction
        r1 = max(0.0, 1.0 - abs(total_cost - budget) / budget)
        optimal = hall["optimal_tables"]
        max_t = hall["max_tables"]
        r2 = max(0.0, 1.0 - abs(table_count - optimal) / max_t)
        r3 = style_score(desired_style, decor["style"], groups)
        if len(priority_services) == 0:
            r4 = 1.0
        else:
            hit = len(set(priority_services) & set(included_services))
            r4 = hit / len(priority_services)

        latent = 0.40 * r1 + 0.25 * r2 + 0.20 * r3 + 0.15 * r4
        satisfaction = 1.0 + 4.0 * latent + float(rng.normal(0, 0.28))
        satisfaction = float(np.clip(round(satisfaction, 2), 1.0, 5.0))

        if satisfaction >= 4.5:
            grade = 3
        elif satisfaction >= 3.5:
            grade = 2
        elif satisfaction >= 2.5:
            grade = 1
        else:
            grade = 0

        rows.append({
            "SampleId": sample_id,
            "Budget": int(budget),
            "TableCount": int(table_count),
            "GuestCount": int(guest_count),
            "DesiredStyle": desired_style,
            "Shift": shift,
            "SelectedServices": ";".join(priority_services),
            "HallId": hall["hall_id"],
            "MenuId": menu["menu_id"],
            "DecorId": decor["decor_id"],
            "IncludedServices": ";".join(included_services),
            "TotalCost": int(total_cost),
            "Satisfaction": satisfaction,
            "RelevanceGrade": grade,
        })

    df = pd.DataFrame(rows)
    df.to_csv(OUTPUT_CSV, index=False, encoding="utf-8")
    print(f"Da sinh {len(df)} mau -> {OUTPUT_CSV}")
    print(df.head(5).to_string(index=False))
    print(df.describe(include="all").to_string())


if __name__ == "__main__":
    main()
