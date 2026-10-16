"""Riverside Palace - Intelligent Recommendation & Decision Support Service (FastAPI, port 8000)."""
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from .core.config import CORS_ORIGINS, SERVICE_TITLE, SERVICE_VERSION
from .core.wsm_engine import rank_top3
from .schemas.recommendation import RecommendationRequest, RecommendationResponse

app = FastAPI(title=SERVICE_TITLE, version=SERVICE_VERSION)

# UC10 RAG router (import lazy để main vẫn chạy khi thiếu dep option).
try:
    from .api.v1 import api_router as _rag_api_router
    app.include_router(_rag_api_router)
except Exception:  # pragma: no cover - fallback khi thiếu package option
    pass

app.add_middleware(
    CORSMiddleware,
    allow_origins=CORS_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
def health():
    return {"status": "ok", "service": SERVICE_TITLE, "version": SERVICE_VERSION}


@app.post("/api/v1/ai/wsm/rank", response_model=RecommendationResponse)
def rank_packages(payload: RecommendationRequest):
    items, total = rank_top3(payload)
    notice = (f"Da danh gia {total} phuong an bang WSM (Hard Filtering + Soft Scoring) "
              f"va tra ve toi da 3 ket qua.")
    if total == 0:
        notice = ("Khong co phuong an nao thoa Hard Filtering "
                  "(sanh trong, suc chua, tran ngan sach toi da 10 trieu VND).")
    return RecommendationResponse(success=True, total_candidates_evaluated=total,
                                  top_3=items, algorithm_notice=notice)
