"""Endpoints RAG UC10 (Lewis et al. 2020: retriever Top-K context + generator).

- POST /chat: tra loi 1 lan (offline rule-based neu khong co OPENAI/GEMINI_KEY,
  LLM sinh neu co key — xem rag_engine._call_external_llm).
- GET /chat-stream (SSE): stream tu token theo cau/paragraph de FE hien dan;
  van offline-first, khong doi key.
"""
import asyncio
from typing import AsyncIterator

from fastapi import APIRouter
from fastapi.responses import StreamingResponse

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


@router.get("/chat-stream")
async def rag_chat_stream(message: str, history_json: str = "[]"):
    """SSE stream reply theo doan (offline rule-based hoac LLM neu co key).

    Event: text/event-stream, moi chunk 1 `data: {...}` + `[DONE]` cuoi.
    """
    import json as _json

    try:
        history = _json.loads(history_json) if history_json else []
    except Exception:
        history = []
    result = rag_engine.chat(message, history)
    reply = str(result.get("reply", ""))

    async def _gen() -> AsyncIterator[str]:
        # Chia theo doan xuong dong de FE render dan ma khong can token-LLM that.
        for para in [p for p in reply.split("\n") if p.strip()]:
            yield f"data: {_json.dumps({'text': para}, ensure_ascii=False)}\n\n"
            await asyncio.sleep(0)
        yield "data: [DONE]\n\n"

    return StreamingResponse(_gen(), media_type="text/event-stream")
