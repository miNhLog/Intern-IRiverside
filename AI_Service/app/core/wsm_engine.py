"""WSM Engine: Hard Filtering + Soft Scoring WSM + XAI tieng Viet."""
import json
from pathlib import Path
from typing import Dict, List, Tuple

from .config import DEFAULT_WEIGHTS, OVER_BUDGET_TOLERANCE_VND

_catalog_cache: Dict = {}
_weights_cache: Dict = {}


def _catalog_path() -> Path:
    from . import config as cfg
    return Path(cfg.CATALOG_PATH)


def _weights_path() -> Path:
    from . import config as cfg
    return Path(cfg.WEIGHTS_PATH)


def load_catalog() -> Dict:
    global _catalog_cache
    if _catalog_cache:
        return _catalog_cache
    path = _catalog_path()
    with open(path, "r", encoding="utf-8") as f:
        _catalog_cache = json.load(f)
    return _catalog_cache


def load_weights() -> Dict[str, float]:
    global _weights_cache
    if _weights_cache:
        return _weights_cache
    path = _weights_path()
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        _weights_cache = {
            "w1_budget": float(data.get("w1_budget", 0.25)),
            "w2_capacity": float(data.get("w2_capacity", 0.25)),
            "w3_style": float(data.get("w3_style", 0.25)),
            "w4_service": float(data.get("w4_service", 0.25)),
        }
    except (FileNotFoundError, ValueError, KeyError):
        _weights_cache = dict(DEFAULT_WEIGHTS)
    total = sum(_weights_cache.values())
    if total > 0:
        for key in _weights_cache:
            _weights_cache[key] = _weights_cache[key] / total
    return _weights_cache


def style_score(desired: str, decor_style: str, groups: Dict) -> float:
    desired_norm = (desired or "").strip().lower()
    decor_norm = (decor_style or "").strip().lower()
    if desired_norm == decor_norm or desired_norm in decor_norm or decor_norm in desired_norm:
        return 1.0
    for _group, members in groups.items():
        lowered = [str(m).lower() for m in members]
        if desired_norm in lowered and decor_norm in lowered:
            return 0.5
        # Khop tuong doi theo tu khoa chung
        for member in lowered:
            if desired_norm in member or member in desired_norm:
                if decor_norm in member or member in decor_norm:
                    return 0.5
    return 0.0


def score_candidate(budget: float, table_count: int, desired_style: str,
                    priority: List[str], hall: Dict, menu_price: float,
                    decor: Dict, included: List[str], groups: Dict,
                    weights: Dict[str, float]) -> Tuple[float, Dict[str, float], float]:
    svc_price_map = {s["service_id"]: s["price"] for s in load_catalog()["services"]}
    menu_total = float(menu_price) * int(table_count)
    services_price = float(sum(svc_price_map.get(s, 0) for s in included))
    total = float(menu_total + hall["base_price"] + decor["price"] + services_price)
    # Điểm ngân sách r1: ưu tiên trong ngân sách, vượt nhẹ ≤ 10tr điểm trung bình,
    # vượt nhiều phạt nặng để không lọt Top 3.
    over = total - float(budget)
    if over <= 0:
        r1 = max(0.0, 1.0 - abs(total - budget) / max(1.0, float(budget)))
    elif over <= 5_000_000:
        r1 = 0.70 - (over / 5_000_000) * 0.05
    elif over <= 10_000_000:
        r1 = 0.65 - ((over - 5_000_000) / 5_000_000) * 0.10
    else:
        extra_tens = (over - 10_000_000) / 10_000_000
        r1 = max(0.0, 0.45 - extra_tens * 0.08)
    r2 = max(0.0, 1.0 - abs(int(table_count) - hall["optimal_tables"]) / max(1.0, float(hall["max_tables"])))
    r3 = style_score(desired_style, decor["style"], groups)
    if len(priority) == 0:
        r4 = 1.0
    else:
        r4 = len(set(priority) & set(included)) / len(priority)
    score = (weights["w1_budget"] * r1 + weights["w2_capacity"] * r2
             + weights["w3_style"] * r3 + weights["w4_service"] * r4)
    return score, {"r1": r1, "r2": r2, "r3": r3, "r4": r4}, total


def _fmt_vnd(value: float) -> str:
    try:
        s = f"{value:,.0f}"
    except (ValueError, TypeError):
        s = "0"
    return s.replace(",", ".") + " \u0111"


def build_explanation(hall: Dict, decor: Dict, desired_style: str, budget: float,
                      total: float, table_count: int, r_parts: Dict[str, float],
                      priority: List[str], included: List[str]) -> List[str]:
    args: List[str] = []
    diff = float(budget) - float(total)
    pct = (abs(diff) / float(budget) * 100) if float(budget) > 0 else 0.0
    saved_tr = abs(diff) / 1_000_000
    if diff >= 0:
        args.append(f"Chi ph\u00ed: Ti\u1ebft ki\u1ec7m {_fmt_vnd(diff)} ({saved_tr:.1f} tri\u1ec7u, {pct:.1f}%) so v\u1edbi ng\u00e2n s\u00e1ch.")
    else:
        args.append(f"Chi ph\u00ed: V\u01b0\u1ee3t {_fmt_vnd(abs(diff))} ({saved_tr:.1f} tri\u1ec7u, {pct:.1f}%) so v\u1edbi ng\u00e2n s\u00e1ch, c\u00e2n nh\u1eafc \u0111i\u1ec1u ch\u1ec9nh.")
    span = hall["max_tables"] - hall["min_tables"]
    use_ratio = (table_count - hall["min_tables"]) / span if span > 0 else 1.0
    use_ratio = max(0.0, min(1.0, use_ratio))
    args.append(f"S\u1ee9c ch\u1ee9a: {table_count} b\u00e0n \u0111\u1ea1t l\u1ea5p \u0111\u1ea7y {use_ratio*100:.1f}% s\u1ea3nh {hall['hall_name']} ({hall['min_tables']}-{hall['max_tables']} b\u00e0n, t\u1ed1i \u01b0u {hall['optimal_tables']} b\u00e0n).")
    if r_parts["r3"] >= 1.0:
        args.append(f"Phong c\u00e1ch: '{decor['style']}' kh\u1edbp 100% v\u1edbi mong mu\u1ed1n '{desired_style}'.")
    elif r_parts["r3"] >= 0.5:
        args.append(f"Phong c\u00e1ch: '{decor['decor_name']}' ({decor['style']}) c\u00f9ng nh\u00f3m t\u01b0\u01a1ng \u0111\u1ed3ng v\u1edbi '{desired_style}' (kh\u1edbp 50%).")
    else:
        args.append(f"Phong c\u00e1ch: '{decor['decor_name']}' ({decor['style']}) kh\u00e1c v\u1edbi '{desired_style}' (kh\u1edbp 0%), b\u00f9 l\u1ea1i b\u1eb1ng chi ph\u00ed v\u00e0 s\u1ee9c ch\u1ee9a.")
    if len(priority) == 0:
        args.append("D\u1ecbch v\u1ee5: kh\u00e1ch kh\u00f4ng ch\u1ecdn DV \u01b0u ti\u00ean, g\u00f3i \u0111\u00e1p \u1ee9ng linh ho\u1ea1t 100% d\u1ecbch v\u1ee5 \u0111i k\u00e8m.")
    else:
        hit = len(set(priority) & set(included))
        args.append(f"D\u1ecbch v\u1ee5: \u0111\u00e1p \u1ee9ng {hit}/{len(priority)} DV \u01b0u ti\u00ean ({hit/len(priority)*100:.0f}%).")
    return args


def rank_top3(request) -> Tuple[List[Dict], int]:
    catalog = load_catalog()
    weights = load_weights()
    groups = catalog["style_groups"]
    halls = catalog["halls"]
    menus = catalog["menus"]
    decors = catalog["decors"]
    occupied = set(request.occupied_hall_ids or [])
    priority = list(request.priority_services or [])
    budget = float(request.expected_budget)
    table_count = int(request.table_count)

    scored: List[Dict] = []
    for hall in halls:
        if hall["hall_id"] in occupied:
            continue
        # Rang buoc cung suc chua theo SO BAN (quy chuan 1 ban = 10 khach):
        # min_tables <= requested_tables <= max_tables.
        if table_count < hall["min_tables"] or table_count > hall["max_tables"]:
            continue
        for menu in menus:
            for decor in decors:
                # Uoc tinh nhanh de loc cung: dung included = priority (day du nhat).
                # Chỉ giữ combo trong ngân sách hoặc vượt tối đa 10tr tuyệt đối
                # (thay vì 10% tương đối gây vượt tới 20–30tr với budget lớn).
                svc_map = {s["service_id"]: s["price"] for s in catalog["services"]}
                est_services = sum(svc_map.get(s, 0) for s in priority)
                est_total = menu["price_per_table"] * table_count + hall["base_price"] + decor["price"] + est_services
                if est_total > budget + OVER_BUDGET_TOLERANCE_VND:
                    continue
                included = list(priority)
                s, parts, total = score_candidate(budget, table_count, request.desired_style,
                                                  priority, hall, float(menu["price_per_table"]),
                                                  decor, included, groups, weights)
                scored.append({
                    "hall": hall, "menu": menu, "decor": decor,
                    "included": included, "score": s, "parts": parts, "total": total,
                })
    total_eval = len(scored)
    if total_eval == 0:
        # P0: hard filter budget+10tr khong con combo -> tra rong de Backend
        # tu tinh suggestedNext (minRequiredBudget + 3 ngay), khong tra cheapest.
        return [], 0
    scored.sort(key=lambda x: x["score"], reverse=True)
    # Diversity Filter: không trả 3 combo giống hệt (chỉ khác tên menu cùng giá).
    # Ưu tiên sảnh khác nhau, sau đó Menu/Decor mức giá khác nhau.
    top_entries: List[Dict] = []
    used_halls = set()
    used_menu_decor = set()

    def _price_tier_key(e: Dict) -> str:
        menu = e["menu"]
        decor = e["decor"]
        return (f"{menu['menu_id']}|{decor['decor_id']}|"
                f"{round(float(menu['price_per_table']) / 1_000_000)}|"
                f"{round(float(decor['price']) / 1_000_000)}")

    for e in scored:
        if len(top_entries) >= 3:
            break
        key = _price_tier_key(e)
        if e["hall"]["hall_id"] not in used_halls and key not in used_menu_decor:
            top_entries.append(e)
            used_halls.add(e["hall"]["hall_id"])
            used_menu_decor.add(key)
    for e in scored:
        if len(top_entries) >= 3:
            break
        if e in top_entries:
            continue
        key = f"{e['menu']['menu_id']}|{e['decor']['decor_id']}"
        if e["hall"]["hall_id"] not in used_halls or key not in used_menu_decor:
            top_entries.append(e)
            used_halls.add(e["hall"]["hall_id"])
            used_menu_decor.add(key)
    for e in scored:
        if len(top_entries) >= 3:
            break
        if e not in top_entries:
            top_entries.append(e)
    top = top_entries[:3]
    combo_meta = [
        ("BALANCED", "Combo Hoàn Hảo (Top 1)", "LỰA CHỌN TỐI ƯU"),
        ("STANDARD", "Combo Tiêu Chuẩn (Top 2)", "CÂN BẰNG CHI PHÍ"),
        ("LUXURY", "Combo Sang Trọng (Top 3)", "TRẢI NGHIỆM ĐẲNG CẤP"),
    ]
    items: List[Dict] = []
    for idx, entry in enumerate(top):
        hall, menu, decor = entry["hall"], entry["menu"], entry["decor"]
        total = entry["total"]
        menu_total = float(menu["price_per_table"]) * table_count
        ctype, ctitle, cbadge = combo_meta[idx] if idx < len(combo_meta) else combo_meta[-1]
        items.append({
            "hall_id": hall["hall_id"],
            "hall_name": hall["hall_name"],
            "hall_price": float(hall["base_price"]),
            # Chuan hoa khach vs ban (1 ban = 10 khach). Tuyet doi khong render
            # so khach kem chu "ban" o Frontend.
            "hall_min_guests": int(hall.get("min_guests", hall["min_tables"] * 10)),
            "hall_max_guests": int(hall.get("max_guests", hall["max_tables"] * 10)),
            "hall_min_tables": int(hall["min_tables"]),
            "hall_max_tables": int(hall["max_tables"]),
            "capacity_min_guests": int(hall.get("min_guests", hall["min_tables"] * 10)),
            "capacity_max_guests": int(hall.get("max_guests", hall["max_tables"] * 10)),
            "capacity_min_tables": int(hall["min_tables"]),
            "capacity_max_tables": int(hall["max_tables"]),
            "menu_id": menu["menu_id"],
            "menu_name": menu["menu_name"],
            "price_per_table": float(menu["price_per_table"]),
            "total_menu_price": float(menu_total),
            "decor_id": decor["decor_id"],
            "decor_name": decor["decor_name"],
            "decor_price": float(decor["price"]),
            "decor_style": decor["style"],
            "included_services": entry["included"],
            "total_estimated_cost": float(total),
            "matching_score": round(float(entry["score"]) * 100, 1),
            "combo_name": ctitle,
            "combo_type": ctype,
            "combo_title": ctitle,
            "combo_badge": cbadge,
            "explanation_arguments": build_explanation(
                hall, decor, request.desired_style, budget, total,
                table_count, entry["parts"], priority, entry["included"]),
        })
    return items, total_eval
