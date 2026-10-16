"""Schemas RAG UC10."""
from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field


class RagChatRequest(BaseModel):
    message: str = Field(min_length=1)
    history: List[Any] = Field(default_factory=list)


class RagChatResponse(BaseModel):
    reply: str
    sources: List[Dict[str, Any]] = Field(default_factory=list)
    confidence: float = 0.0
    is_out_of_scope: bool = False


class RagExtractRequest(BaseModel):
    history: List[Any] = Field(default_factory=list)


class RagExtractResponse(BaseModel):
    success: bool = True
    entities: Dict[str, Optional[Any]] = Field(default_factory=dict)
