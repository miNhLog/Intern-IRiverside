"""Cau hinh AI Service."""
import os
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent.parent
CATALOG_PATH = os.getenv("CATALOG_PATH", str(BASE_DIR / "data" / "raw_catalog.json"))
WEIGHTS_PATH = os.getenv("WEIGHTS_PATH", str(BASE_DIR / "app" / "core" / "trained_weights.json"))

SERVICE_TITLE = "Riverside Palace - Intelligent Recommendation & Decision Support Service"
SERVICE_VERSION = "1.0.0"

CORS_ORIGINS = [
    "https://localhost:7101",
    "https://localhost:7102",
    "http://localhost:5102",
]

OVER_BUDGET_TOLERANCE = 1.10
# Dung sai vượt ngân sách tuyệt đối (VNĐ): gợi ý chỉ vượt tối đa 10tr để tránh chênh lệch lớn.
# Min thực tế ~103,3tr cho 150 khách (sảnh Elbe 20tr + menu 4,35tr/bàn × 15 + decor 18tr).
OVER_BUDGET_TOLERANCE_VND = 10_000_000
OVER_BUDGET_SOFT_VND = 5_000_000
TOP_K = 3
# Trong so chuan W* (tong 1.0) — dong bo 4 chieu C# <-> Python (P0).
DEFAULT_WEIGHTS = {"w1_budget": 0.3004, "w2_capacity": 0.3198, "w3_style": 0.1799, "w4_service": 0.1999}
