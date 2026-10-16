"""Pydantic schemas cho API goi y WSM."""
from datetime import date
from typing import List, Optional

from pydantic import BaseModel, Field, field_validator


class RecommendationRequest(BaseModel):
    guest_count: int = Field(ge=1, le=2000)
    table_count: int = Field(ge=1, le=200)
    # Min 100tr: chi phí tối thiểu ~103,3tr cho 150 khách
    # (sảnh Elbe 20tr + menu 4,35tr/bàn × 15 + decor 18tr). Gợi ý chỉ vượt tối đa 10tr.
    expected_budget: float = Field(ge=100_000_000, le=3_000_000_000)
    event_date: str = Field(pattern=r"^\d{4}-\d{2}-\d{2}$")
    shift: str = Field(pattern=r"^(Ca trua|Ca toi)$")
    desired_style: str
    priority_services: List[str] = Field(default_factory=list)
    occupied_hall_ids: List[int] = Field(default_factory=list)

    @field_validator("event_date")
    @classmethod
    def validate_event_window(cls, v: str) -> str:
        # Riverside Palace: nhận đặt tiệc trước từ 30 ngày đến tối đa 365 ngày (1 năm).
        try:
            d = date.fromisoformat(v)
        except ValueError:
            raise ValueError("Ngày tổ chức không hợp lệ.")
        today = date.today()
        min_d = date.fromordinal(today.toordinal() + 30)
        max_d = date.fromordinal(today.toordinal() + 365)
        if d < min_d:
            raise ValueError("Ngày tổ chức tiệc cưới phải cách ngày hiện tại tối thiểu 30 ngày. Vui lòng chọn ngày xa hơn để Riverside Palace có thời gian chuẩn bị tốt nhất.")
        if d > max_d:
            raise ValueError("Hệ thống chỉ nhận đăng ký giữ chỗ trước tối đa 365 ngày (1 năm). Vui lòng chọn ngày gần hơn.")
        return v


class RecommendationItem(BaseModel):
    hall_id: int
    hall_name: str
    hall_price: float
    # Chuan hoa khach vs ban (1 ban = 10 khach).
    hall_min_guests: int = 0
    hall_max_guests: int = 0
    hall_min_tables: int = 0
    hall_max_tables: int = 0
    capacity_min_guests: int = 0
    capacity_max_guests: int = 0
    capacity_min_tables: int = 0
    capacity_max_tables: int = 0
    menu_id: int
    menu_name: str
    price_per_table: float
    total_menu_price: float
    decor_id: int
    decor_name: str
    decor_price: float
    decor_style: str
    included_services: List[str] = Field(default_factory=list)
    total_estimated_cost: float
    matching_score: float = Field(ge=0, le=100)
    explanation_arguments: List[str] = Field(default_factory=list)
    # Chuẩn hóa tên combo — Frontend không bao giờ render undefined.
    combo_name: str = ""
    combo_type: str = ""
    combo_title: str = ""
    combo_badge: str = ""


class RecommendationResponse(BaseModel):
    success: bool = True
    total_candidates_evaluated: int = 0
    top_3: List[RecommendationItem] = Field(default_factory=list)
    algorithm_notice: Optional[str] = None
