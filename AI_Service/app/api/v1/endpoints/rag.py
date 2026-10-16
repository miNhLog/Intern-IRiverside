"""Endpoints RAG UC10."""
from fastapi import APIRouter

from ....core import rag_engine
from ....schemas.rag import RagChatRequest, RagChatResponse, RagExtractRequest, RagExtractResponse

router = APIRouter(prefix="/api/v1/ai/rag", tags=["rag"])


@router.post("/chat", response_model=RagChatResponse)
def rag_chat(payload: RagChatRequest):
    result = rag_engine.chat(payload.message, payload.history)
    return RagChatResponse(
        reply=result.get("reply", ""),
        sources=result.get("sources", []),
        confidence=float(result.get("confidence", 0.0)),
        is_out_of_scope=bool(result.get("is_out_of_scope", False)),
    )


@router.post("/extract-entities", response_model=RagExtractResponse)
def rag_extract(payload: RagExtractRequest):
    entities = rag_engine.extract_entities(payload.history or [])
    return RagExtractResponse(success=True, entities=entities)
