"""Learn-to-Rank WSM: toi uu trong so bang SLSQP (min MSE giua S va Satisfaction/5.0),
danh gia Precision@3 / Recall@3 / NDCG@3 / HitRate@3 tren test, ve 3 bieu do."""
import json
import os
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import seaborn as sns
from scipy.optimize import minimize
from sklearn.model_selection import train_test_split

HERE = Path(__file__).resolve().parent
DATA_CSV = HERE.parent / "data" / "wedding_dataset_5k.csv"
CATALOG_PATH = HERE.parent / "data" / "raw_catalog.json"
WEIGHTS_PATH = HERE.parent / "app" / "core" / "trained_weights.json"
PLOT_DIR = HERE / "output_plots"
PLOT_DIR.mkdir(parents=True, exist_ok=True)
RANDOM_SEED = 257
N_CANDIDATES = 20  # 1 ground-truth + 19 ung vien ngau nhien moi test sample


def load_catalog():
    with open(CATALOG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


def style_score_value(desired, decor_style, groups) -> float:
    if desired == decor_style:
        return 1.0
    for _g, members in groups.items():
        if desired in members and decor_style in members:
            return 0.5
    return 0.0


def parse_services(text) -> set:
    if text is None or (isinstance(text, float) and np.isnan(text)) or text == "":
        return set()
    return set(str(text).split(";")) if str(text).strip() != "" else set()


def compute_criteria_row(budget, table_count, desired_style, priority, hall, menu_price, decor, included, groups):
    menu_total = menu_price * table_count
    svc_price_map = groups["_svc_price"]
    services_price = sum(svc_price_map.get(s, 0) for s in included)
    total = menu_total + hall["base_price"] + decor["price"] + services_price
    r1 = max(0.0, 1.0 - abs(total - budget) / max(1.0, float(budget)))
    r2 = max(0.0, 1.0 - abs(table_count - hall["optimal_tables"]) / max(1.0, float(hall["max_tables"])))
    r3 = style_score_value(desired_style, decor["style"], groups)
    if len(priority) == 0:
        r4 = 1.0
    else:
        r4 = len(set(priority) & set(included)) / len(priority)
    return r1, r2, r3, r4, total


def dcg_at_k(grades, k):
    value = 0.0
    for i in range(min(k, len(grades))):
        value += (2 ** grades[i] - 1) / np.log2(i + 2)
    return value


def main():
    catalog = load_catalog()
    halls = catalog["halls"]
    menus = catalog["menus"]
    decors = catalog["decors"]
    groups = dict(catalog["style_groups"])
    groups["_svc_price"] = {s["service_id"]: s["price"] for s in catalog["services"]}
    hall_map = {h["hall_id"]: h for h in halls}
    menu_map = {m["menu_id"]: m for m in menus}
    decor_map = {d["decor_id"]: d for d in decors}

    df = pd.read_csv(DATA_CSV, encoding="utf-8")
    print(f"Doc {len(df)} mau tu {DATA_CSV}")

    # Tinh r1..r4 cho tap train (ground-truth cua moi dong)
    crit = []
    for _, row in df.iterrows():
        hall = hall_map[int(row["HallId"])]
        menu = menu_map[int(row["MenuId"])]
        decor = decor_map[int(row["DecorId"])]
        priority = parse_services(row["SelectedServices"])
        included = parse_services(row.get("IncludedServices", row["SelectedServices"]))
        r = compute_criteria_row(
            float(row["Budget"]), int(row["TableCount"]), str(row["DesiredStyle"]),
            priority, hall, float(menu["price_per_table"]), decor, included, groups,
        )
        crit.append(r[:4])
    crit = np.array(crit, dtype=float)
    y = (df["Satisfaction"].to_numpy(dtype=float) / 5.0)

    X_train, X_test, y_train, y_test, df_train, df_test = train_test_split(
        crit, y, df, test_size=0.20, random_state=RANDOM_SEED
    )
    print(f"Train: {len(X_train)} | Test: {len(X_test)}")

    def mse_loss(w):
        preds = X_train @ w
        return float(np.mean((preds - y_train) ** 2))

    constraints = {"type": "eq", "fun": lambda w: float(np.sum(w)) - 1.0}
    bounds = [(0.0, 1.0)] * 4
    result = minimize(mse_loss, x0=np.array([0.25, 0.25, 0.25, 0.25]),
                      method="SLSQP", bounds=bounds, constraints=constraints,
                      options={"maxiter": 500, "ftol": 1e-9})
    w_opt = np.clip(result.x, 0, 1)
    w_opt = w_opt / w_opt.sum()
    w_base = np.array([0.25, 0.25, 0.25, 0.25])
    print(f"Toi uu hoi tu: success={result.success} message={result.message}")
    print(f"Trong so toi uu: w1={w_opt[0]:.4f} w2={w_opt[1]:.4f} w3={w_opt[2]:.4f} w4={w_opt[3]:.4f}")
    print(f"Baseline: w1=w2=w3=w4=0.25 | MSE train baseline={float(np.mean((X_train @ w_base - y_train)**2)):.6f} | MSE train optimized={float(np.mean((X_train @ w_opt - y_train)**2)):.6f}")
    print(f"MSE test baseline={float(np.mean((X_test @ w_base - y_test)**2)):.6f} | MSE test optimized={float(np.mean((X_test @ w_opt - y_test)**2)):.6f}")

    with open(WEIGHTS_PATH, "w", encoding="utf-8") as f:
        json.dump({"w1_budget": float(w_opt[0]), "w2_capacity": float(w_opt[1]),
                   "w3_style": float(w_opt[2]), "w4_service": float(w_opt[3]),
                   "method": "SLSQP min MSE(S vs Satisfaction/5.0)",
                   "baseline": [0.25, 0.25, 0.25, 0.25]}, f, ensure_ascii=False, indent=2)
    print(f"Da luu trong so -> {WEIGHTS_PATH}")

    # ---- Danh gia ranking Top-3 tren test: moi sample sinh 20 ung vien ----
    rng = np.random.default_rng(RANDOM_SEED)
    hall_ids = [h["hall_id"] for h in halls]
    menu_ids = [m["menu_id"] for m in menus]
    decor_ids = [d["decor_id"] for d in decors]
    service_ids = [s["service_id"] for s in catalog["services"]]

    def grade_from_latent(latent: float) -> int:
        sat = 1.0 + 4.0 * latent
        if sat >= 4.5:
            return 3
        if sat >= 3.5:
            return 2
        if sat >= 2.5:
            return 1
        return 0

    metrics_base = {"p3": [], "r3": [], "ndcg3": [], "hit3": []}
    metrics_opt = {"p3": [], "r3": [], "ndcg3": [], "hit3": []}
    all_scores_opt = []

    for _, row in df_test.iterrows():
        budget = float(row["Budget"])
        table_count = int(row["TableCount"])
        desired_style = str(row["DesiredStyle"])
        priority = parse_services(row["SelectedServices"])
        # Ung vien 0 = ground-truth
        candidate_defs = [(int(row["HallId"]), int(row["MenuId"]), int(row["DecorId"]),
                           parse_services(row.get("IncludedServices", row["SelectedServices"])))]
        for _ in range(N_CANDIDATES - 1):
            h_id = int(rng.choice(hall_ids))
            m_id = int(rng.choice(menu_ids))
            d_id = int(rng.choice(decor_ids))
            n_inc = int(rng.integers(0, 4))
            inc = set(rng.choice(service_ids, size=n_inc, replace=False).tolist()) if n_inc > 0 else set()
            candidate_defs.append((h_id, m_id, d_id, inc))

        grades, s_base, s_opt = [], [], []
        for (h_id, m_id, d_id, inc) in candidate_defs:
            r1, r2, r3, r4, _total = compute_criteria_row(
                budget, table_count, desired_style, priority,
                hall_map[h_id], float(menu_map[m_id]["price_per_table"]), decor_map[d_id], inc, groups)
            latent_true = 0.40 * r1 + 0.25 * r2 + 0.20 * r3 + 0.15 * r4
            grades.append(grade_from_latent(latent_true))
            s_base.append(float(np.dot([r1, r2, r3, r4], w_base)))
            s_opt.append(float(np.dot([r1, r2, r3, r4], w_opt)))
        all_scores_opt.extend(s_opt)
        grades = np.array(grades)

        for s_vec, store in ((np.array(s_base), metrics_base), (np.array(s_opt), metrics_opt)):
            order = np.argsort(-s_vec)
            top_grades = grades[order[:3]]
            relevant_total = int(np.sum(grades >= 2))
            hits = int(np.sum(top_grades >= 2))
            p3 = hits / 3.0
            r3 = (hits / relevant_total) if relevant_total > 0 else 0.0
            ideal = np.sort(grades)[::-1][:3]
            ndcg = (dcg_at_k(top_grades.tolist(), 3) / dcg_at_k(ideal.tolist(), 3)) if dcg_at_k(ideal.tolist(), 3) > 0 else 0.0
            hit = 1.0 if hits > 0 else 0.0
            store["p3"].append(p3)
            store["r3"].append(r3)
            store["ndcg3"].append(ndcg)
            store["hit3"].append(hit)

    def avg(d):
        return {k: float(np.mean(v)) for k, v in d.items()}

    avg_base, avg_opt = avg(metrics_base), avg(metrics_opt)
    print("=== KET QUA TEST (Top-3, relevant = grade>=2, 1000 samples x 20 candidates) ===")
    for name, a in (("Baseline (w=0.25)", avg_base), ("Optimized (SLSQP)", avg_opt)):
        print(f"{name}: Precision@3={a['p3']:.4f} Recall@3={a['r3']:.4f} NDCG@3={a['ndcg3']:.4f} HitRate@3={a['hit3']:.4f}")

    # ---- Ve 3 bieu do ----
    sns.set_theme(style="whitegrid")
    labels = ["Precision@3", "Recall@3", "NDCG@3", "HitRate@3"]
    base_vals = [avg_base["p3"], avg_base["r3"], avg_base["ndcg3"], avg_base["hit3"]]
    opt_vals = [avg_opt["p3"], avg_opt["r3"], avg_opt["ndcg3"], avg_opt["hit3"]]

    fig, ax = plt.subplots(figsize=(9, 5.5))
    x = np.arange(len(labels))
    ax.bar(x - 0.2, base_vals, 0.4, label="Baseline (w=0.25)")
    ax.bar(x + 0.2, opt_vals, 0.4, label="Optimized (SLSQP)")
    for i, (b, o) in enumerate(zip(base_vals, opt_vals)):
        ax.text(i - 0.2, b + 0.01, f"{b:.3f}", ha="center", fontsize=9)
        ax.text(i + 0.2, o + 0.01, f"{o:.3f}", ha="center", fontsize=9)
    ax.set_xticks(x)
    ax.set_xticklabels(labels)
    ax.set_ylim(0, 1.05)
    ax.set_ylabel("Gia tri metric")
    ax.set_title("So sanh metrics Baseline vs Optimized (Top-3)")
    ax.legend()
    fig.tight_layout()
    fig.savefig(PLOT_DIR / "metrics_comparison.png", dpi=150)
    plt.close(fig)

    fig2, ax2 = plt.subplots(figsize=(6.5, 6.5))
    ax2.pie(w_opt, labels=["w1 Budget Fit", "w2 Capacity", "w3 Style", "w4 Service"],
            autopct="%1.1f%%", startangle=90)
    ax2.set_title("Phan bo trong so WSM toi uu (SLSQP)")
    fig2.tight_layout()
    fig2.savefig(PLOT_DIR / "weight_distribution.png", dpi=150)
    plt.close(fig2)

    fig3, ax3 = plt.subplots(figsize=(9, 5))
    sns.histplot(np.array(all_scores_opt), bins=30, kde=True, ax=ax3)
    ax3.set_xlabel("Diem WSM (Optimized)")
    ax3.set_ylabel("Tan suat")
    ax3.set_title("Phan phoi diem WSM toi uu tren tap test (20 ung vien / sample)")
    fig3.tight_layout()
    fig3.savefig(PLOT_DIR / "score_distribution.png", dpi=150)
    plt.close(fig3)
    print(f"Da luu plots -> {PLOT_DIR}")


if __name__ == "__main__":
    main()
