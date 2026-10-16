"""RAG Engine UC10: ChromaDB (optional) + TF-IDF/hash fallback, chat + entity extraction.

Persist: AI_Service/data/chroma_db/ (collection riverside_catalog).
Source : AI_Service/data/knowledge_base/riverside_knowledge.md
"""
import hashlib
import json
import math
import os
import re
import unicodedata
from datetime import date, datetime
from pathlib import Path
from typing import Dict, List, Optional, Tuple

BASE_DIR = Path(__file__).resolve().parent.parent.parent
KB_PATH = BASE_DIR / "data" / "knowledge_base" / "riverside_knowledge.md"
CHROMA_DIR = BASE_DIR / "data" / "chroma_db"
COLLECTION_NAME = "riverside_catalog"
FILE_STORE = CHROMA_DIR / "store.json"
INGEST_META = CHROMA_DIR / "ingest_meta.json"

CHUNK_TOKENS = 400
CHUNK_OVERLAP = 50
OUT_OF_SCOPE_THRESHOLD = 0.70

SAFE_OUT_OF_SCOPE = ("Dạ thông tin này hiện nằm ngoài phạm vi chính sách dịch vụ công bố "
                     "của Riverside Palace. Quý khách vui lòng liên hệ Hotline trực tiếp "
                     "0933 777 888 để được chuyên viên hỗ trợ giải đáp chi tiết ạ!")

def _norm_vi(text: str) -> str:
    """Hạ chữ + bỏ dấu tiếng Việt để TF-IDF/hash khớp 'sảnh'~'sanh', 'giá'~'gia'."""
    t = unicodedata.normalize("NFD", (text or "").lower())
    t = "".join(c for c in t if unicodedata.category(c) != "Mn")
    t = t.replace("đ", "d").replace("Đ", "d")
    return re.sub(r"\s+", " ", t).strip()


def _content_tokens(text: str) -> List[str]:
    return [w for w in re.findall(r"[a-z0-9]+", _norm_vi(text))
            if len(w) > 2 and w not in _VI_STOPS]


_VI_STOPS = frozenset("""
nao khong muon khoang vao voi tam la cac cua va cho cai nay voi den tu ve khi neu
rat hay thi ma de den voi con duoc se co da dang nhung mot cai nay do boi toi ta
chung rieng tren duoi trong ngoai nay kia sao the uoc can phai chuc
viec phan chiec lan thu nhat hai ba bon nam sau bay tam chin muoi
""".split())


def _keyword_overlap(query: str, doc: str) -> float:
    q = _content_tokens(query)
    if not q:
        return 0.0
    d = set(_content_tokens(doc))
    hit = sum(1 for w in q if w in d)
    return hit / len(q)


def _weighted_overlap(query: str, doc: str) -> float:
    """Overlap có trọng số IDF: từ hiếm (elbe, menu, 48h) nặng hơn từ chung (bao, nhieu)."""
    q = _content_tokens(query)
    if not q:
        return 0.0
    d = set(_content_tokens(doc))
    weights = _idf_weights()
    tot = sum(weights.get(w, _DEFAULT_IDF) for w in q) or 1.0
    hit = sum(weights.get(w, _DEFAULT_IDF) for w in q if w in d)
    return hit / tot


_DEFAULT_IDF = 4.0
_idf_cache: Optional[Dict[str, float]] = None


def _idf_weights() -> Dict[str, float]:
    global _idf_cache
    if _idf_cache is not None:
        return _idf_cache
    out: Dict[str, float] = {}
    try:
        vec = _get_tfidf()
        if vec is not None and getattr(vec, "vocabulary_", None):
            inv = {i: w for w, i in vec.vocabulary_.items()}
            for i, v in enumerate(getattr(vec, "idf_", [])):
                if i in inv:
                    out[inv[i]] = float(v)
    except Exception:
        pass
    _idf_cache = out
    return out


def _reset_idf_cache() -> None:
    global _idf_cache
    _idf_cache = None


def _rare_coverage(query: str, doc: str, k: int = 6) -> float:
    """Tỷ lệ phủ K token hiếm nhất của query trong doc (trọng số IDF).
    Truy vấn đa ý (sảnh + ngân sách + ngày + ca) không chunk nào chứa hết từ,
    nhưng chunk đúng phủ gần hết token hiếm -> vượt ngưỡng 0.70 đúng spec.
    Token lạ (ngoài vocab) tính trọng số 1.0 để không lấn át token hiếm thật."""
    q = _content_tokens(query)
    if not q:
        return 0.0
    weights = _idf_weights()
    ranked = sorted(q, key=lambda w: weights.get(w, 1.0), reverse=True)
    top = ranked[:k] if len(ranked) > k else ranked
    tot = sum(weights.get(w, 1.0) for w in top) or 1.0
    d = set(_content_tokens(doc))
    hit = sum(weights.get(w, 1.0) for w in top if w in d)
    return hit / tot


_GREETING_PAT = re.compile(
    r"^(xin chao|chao ban|chao em|chao|hello|hi+|hey|alo|yo)\b|[Cc][ảa][mM] [ơo][nN]|thank|tam biet|bye",
)


def _is_greeting(text: str) -> bool:
    t = _norm_vi(text).strip()
    return bool(re.match(r"^(xin chao|chao|hello|hi|hey|alo|yo|good (morning|afternoon|evening))\b", t)) \
        or t in {"chao ban", "chao em", "hi", "hello", "chao"}


def _greeting_reply() -> str:
    return ("Dạ em chào Quý khách ạ! 💛 Em là Trợ lý ảo Riverside Palace, "
            "chuyên tư vấn sảnh tiệc, thực đơn 7 món Á–Âu 4,35–5,9tr/bàn, decor và chính sách "
            "(giữ chỗ 48h, thanh toán 20%–50%–quyết toán). Quý khách cần tư vấn gì cứ nhắn em nhé, "
            "hoặc gọi Hotline 0933 777 888 để gặp chuyên viên ngay ạ!")


# Từ khóa ngoài miền: ép out-of-scope trừ khi có neo cưới rõ (tên sảnh, menu...).
_OUT_KEYWORDS = [
    "chung khoan", "co phieu", "ngoai te", "bitcoin", "tien ao", "lai suat",
    "ti gia", "gia vang", "vang bac", "thoi tiet", "du bao thoi tiet", "xo so",
    "bong da", "ca do", "chinh tri", "dich benh", "covid", "so de",
]
_ANCHOR_STRONG = [
    "tiec cuoi", "sanh", "thuc don", "menu", "decor", "trang tri", "dat coc",
    "giu cho", "48h", "hotline", "amur", "elbe", "danube", "seine", "thames",
    "volga", "nile", "ballroom", "riverside",
]


def _forced_out_of_scope(query: str) -> bool:
    t = _norm_vi(query)
    if not any(k in t for k in _OUT_KEYWORDS):
        return False
    return not any(a in t for a in _ANCHOR_STRONG)


# Lưới concept theo miền: ngưỡng 0.70 (spec) giữ cho embedding ngữ nghĩa;
# fallback TF-IDF trên chunk dài cho điểm thô thấp nên cộng cổng concept
# (từ khóa chuyên ngành sảnh/menu/decor/chính sách) để không loại nhầm câu hỏi
# đa ý hợp lệ, vẫn chặn out-of-scope (chứng khoán, thời tiết...).
_CONCEPT_STRONG = [
    "thuc don", "bao nhieu", "dat coc", "giu cho", "trang tri", "ca toi", "ca trua",
    "ngan sach", "thanh toan", "quyet toan", "doi lich", "hoang gia", "lang man",
    "san vuon", "co tich", "hien dai", "hai san", "tiec cuoi", "ven song", "menu",
    "decor", "48h", "hotline", "0933", "amur", "elbe", "danube", "seine", "thames",
    "volga", "nile", "ballroom", "gia ",
]
_CONCEPT_NORMAL = [
    "sanh", "ban", "khach", "mon", "tom", "cua", "ca ", "bo ", "ga ", "vit", "lau",
    "sup", "salad", "pudding", "che ", "mi ", "com ", "trieu", "ty ", "dong",
    "ngay", "thang", "nam ", "lich", "trua", "toi", "dich vu", "coc", "giu",
    "huy", "dat tiec", "riverside", "palace", "cuoi", "chinh sach", "uu dai",
    "khuyen mai", "suc chua", "cong hoa", "backdrop", "gallery", "piano", "violin",
    "guitar", "karaoke", "thiep", "gia tien", "mc ",
]


def _concept_points(query: str) -> int:
    t = " " + _norm_vi(query) + " "
    pts = 0
    for ph in _CONCEPT_STRONG:
        k = ph if (" " in ph or ph.endswith(" ")) else f" {ph} "
        if k in t:
            pts += 2
            t = t.replace(k, " ")
    for ph in _CONCEPT_NORMAL:
        k = ph if (" " in ph or ph.endswith(" ")) else f" {ph} "
        if k in t:
            pts += 1
            t = t.replace(k, " ")
    return pts


# ---------------------------------------------------------------- embeddings
_st_model = None
_st_failed = False
_tfidf = None
_tfidf_dim = 0


def _try_load_st_model():
    global _st_model, _st_failed
    if _st_model is not None:
        return _st_model
    if _st_failed:
        return None
    if os.getenv("RAG_EMBEDDINGS", "").strip().lower() in ("tfidf", "hash", "local", "off"):
        return None
    try:
        from sentence_transformers import SentenceTransformer
        _st_model = SentenceTransformer("all-MiniLM-L6-v2")
        return _st_model
    except Exception:
        _st_model = None
        _st_failed = True
        return None


def _hash_embed(text: str, dim: int = 256) -> List[float]:
    vec = [0.0] * dim
    for tok in _content_tokens(text):
        h = int(hashlib.md5(tok.encode("utf-8")).hexdigest(), 16)
        vec[h % dim] += 1.0
    norm = math.sqrt(sum(v * v for v in vec)) or 1.0
    return [v / norm for v in vec]


def _get_tfidf():
    global _tfidf
    if _tfidf is not None:
        return _tfidf
    try:
        from sklearn.feature_extraction.text import TfidfVectorizer
        _tfidf = TfidfVectorizer(max_features=512, preprocessor=_norm_vi,
                                 token_pattern=r"(?u)\b\w\w+\b")
        return _tfidf
    except Exception:
        _tfidf = None
        return None


def _embed_texts(texts: List[str], fit_texts: Optional[List[str]] = None) -> List[List[float]]:
    model = _try_load_st_model()
    if model is not None:
        try:
            arr = model.encode(texts, normalize_embeddings=True)
            return [list(map(float, row)) for row in arr]
        except Exception:
            pass
    vec = _get_tfidf()
    corpus = fit_texts or texts
    if vec is not None and corpus:
        try:
            mat = vec.fit_transform(corpus)
            global _tfidf_dim
            _tfidf_dim = mat.shape[1]
            q = vec.transform(texts).toarray()
            out = []
            for row in q:
                n = math.sqrt(float((row ** 2).sum())) or 1.0
                out.append([float(v / n) for v in row])
            return out
        except Exception:
            pass
    return [_hash_embed(t) for t in texts]


def _embed_query(text: str, vocab_texts: Optional[List[str]] = None) -> List[float]:
    model = _try_load_st_model()
    if model is not None:
        try:
            arr = model.encode([text], normalize_embeddings=True)
            return list(map(float, arr[0]))
        except Exception:
            pass
    if _tfidf is not None and getattr(_tfidf, "vocabulary_", None):
        try:
            row = _tfidf.transform([text]).toarray()[0]
            n = math.sqrt(float((row ** 2).sum())) or 1.0
            return [float(v / n) for v in row]
        except Exception:
            pass
    return _hash_embed(text, dim=_tfidf_dim or 256)


def _cosine(a: List[float], b: List[float]) -> float:
    n = min(len(a), len(b))
    if n == 0:
        return 0.0
    dot = sum(a[i] * b[i] for i in range(n))
    na = math.sqrt(sum(v * v for v in a)) or 1.0
    nb = math.sqrt(sum(v * v for v in b)) or 1.0
    return dot / (na * nb)


# ---------------------------------------------------------------- chunking
def chunk_markdown(text: str, chunk_tokens: int = CHUNK_TOKENS,
                   overlap: int = CHUNK_OVERLAP) -> List[Dict]:
    """Chunk theo heading ## + cửa sổ trượt ~400 từ, overlap 50 từ."""
    sections: List[Tuple[str, str]] = []
    cur_head = "Tổng quan"
    cur_lines: List[str] = []
    for line in text.splitlines():
        m = re.match(r"^##\s+(.*)", line.strip())
        if m:
            if cur_lines:
                sections.append((cur_head, "\n".join(cur_lines).strip()))
            cur_head = m.group(1).strip()
            cur_lines = []
        else:
            cur_lines.append(line)
    if cur_lines:
        sections.append((cur_head, "\n".join(cur_lines).strip()))
    chunks: List[Dict] = []
    for head, body in sections:
        words = body.split()
        if not words:
            continue
        step = max(1, chunk_tokens - overlap)
        idx = 0
        while idx < len(words):
            part = words[idx:idx + chunk_tokens]
            chunks.append({"heading": head, "text": f"[{head}] " + " ".join(part)})
            if idx + chunk_tokens >= len(words):
                break
            idx += step
    return chunks


# ---------------------------------------------------------------- store
def _load_file_store() -> Dict:
    try:
        with open(FILE_STORE, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {"chunks": []}


def _save_file_store(data: Dict) -> None:
    CHROMA_DIR.mkdir(parents=True, exist_ok=True)
    with open(FILE_STORE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False)


def _try_chroma_collection():
    try:
        import chromadb
        client = chromadb.PersistentClient(path=str(CHROMA_DIR))
        return client.get_or_create_collection(name=COLLECTION_NAME)
    except Exception:
        return None


def ingest() -> Dict:
    """Đọc md, chunk, embed, upsert. Trả {chunks, backend}."""
    text = KB_PATH.read_text(encoding="utf-8")
    chunks = chunk_markdown(text)
    texts = [c["text"] for c in chunks]
    embeddings = _embed_texts(texts, fit_texts=texts)
    store = {"chunks": []}
    for i, c in enumerate(chunks):
        store["chunks"].append({
            "id": f"chunk-{i}",
            "text": c["text"],
            "heading": c["heading"],
            "embedding": embeddings[i] if i < len(embeddings) else _hash_embed(c["text"]),
        })
    _save_file_store(store)
    backend = "file+tfidf"
    col = _try_chroma_collection()
    if col is not None:
        try:
            try:
                col.delete(where={"source": "riverside_knowledge"})
            except Exception:
                pass
            col.upsert(
                ids=[c["id"] for c in store["chunks"]],
                documents=[c["text"] for c in store["chunks"]],
                metadatas=[{"heading": c["heading"], "source": "riverside_knowledge"}
                           for c in store["chunks"]],
                embeddings=[c["embedding"] for c in store["chunks"]],
            )
            backend = "chromadb"
        except Exception:
            pass
    try:
        INGEST_META.write_text(json.dumps(
            {"chunks": len(chunks), "backend": backend, "at": datetime.now().isoformat(),
             "kb_mtime": KB_PATH.stat().st_mtime}, ensure_ascii=False), encoding="utf-8")
    except Exception:
        pass
    return {"chunks": len(chunks), "backend": backend}


def ensure_ingested(force: bool = False) -> Dict:
    if not force:
        store = _load_file_store()
        if store.get("chunks"):
            try:
                meta = json.loads(INGEST_META.read_text(encoding="utf-8"))
                if float(meta.get("kb_mtime", 0)) >= KB_PATH.stat().st_mtime:
                    return {"chunks": len(store["chunks"]), "cached": True,
                            "backend": meta.get("backend", "file+tfidf")}
            except Exception:
                return {"chunks": len(store["chunks"]), "cached": True, "backend": "file+tfidf"}
    return ingest()


def search_context(query: str, top_k: int = 3) -> List[Tuple[str, float, Dict]]:
    """Cosine similarity, trả [(text, score, meta)]."""
    ensure_ingested()
    store = _load_file_store()
    chunks = store.get("chunks", [])
    if not chunks:
        return []
    # Refit TF-IDF trên corpus để query cùng không gian vector (offline-safe).
    corpus = [c["text"] for c in chunks]
    vec = _get_tfidf()
    if _try_load_st_model() is None and vec is not None:
        try:
            mat = vec.fit_transform(corpus)
            global _tfidf_dim
            _tfidf_dim = mat.shape[1]
            _reset_idf_cache()
            for c, row in zip(chunks, mat.toarray()):
                n = math.sqrt(float((row ** 2).sum())) or 1.0
                c["embedding"] = [float(v / n) for v in row]
        except Exception:
            pass
    q = _embed_query(query, vocab_texts=corpus)
    use_overlap = _try_load_st_model() is None
    scored = []
    for c in chunks:
        emb = c.get("embedding") or []
        if len(emb) != len(q):
            emb = _hash_embed(c["text"], dim=len(q))
        cos = _cosine(q, emb)
        if use_overlap:
            # Fallback TF-IDF/hash: cosine thô thấp do chunk dài -> kết hợp
            # overlap có trọng số IDF + phủ rare-token (không dấu) để ngưỡng 0.70
            # phân biệt in-scope (sảnh/giá/menu/chính sách) vs out-of-scope.
            ov = _weighted_overlap(query, c["text"])
            rc = _rare_coverage(query, c["text"])
            score = max(cos, 0.4 * (0.35 * cos + 0.65 * ov) + 0.6 * rc)
        else:
            score = cos
        scored.append((c["text"], score,
                       {"heading": c.get("heading", ""), "id": c.get("id", "")}))
    scored.sort(key=lambda x: x[1], reverse=True)
    return scored[:max(1, top_k)]


# ---------------------------------------------------------------- chat
def _call_external_llm(prompt: str) -> Optional[str]:
    # OpenAI (ưu tiên nếu có key)
    openai_key = os.getenv("OPENAI_API_KEY", "").strip()
    if openai_key:
        try:
            import urllib.request
            payload = json.dumps({
                "model": os.getenv("OPENAI_MODEL", "gpt-4o-mini"),
                "messages": [{"role": "user", "content": prompt}],
                "max_tokens": 600, "temperature": 0.3,
            }).encode("utf-8")
            req = urllib.request.Request(
                "https://api.openai.com/v1/chat/completions", data=payload,
                headers={"Authorization": f"Bearer {openai_key}",
                         "Content-Type": "application/json"}, method="POST")
            with urllib.request.urlopen(req, timeout=12) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                txt = data["choices"][0]["message"]["content"].strip()
                if txt:
                    return txt
        except Exception:
            pass
    gemini_key = os.getenv("GEMINI_API_KEY", "").strip()
    if gemini_key:
        try:
            import urllib.request
            model = os.getenv("GEMINI_MODEL", "gemini-1.5-flash")
            payload = json.dumps({
                "contents": [{"parts": [{"text": prompt}]}],
                "generationConfig": {"maxOutputTokens": 600, "temperature": 0.3},
            }).encode("utf-8")
            url = (f"https://generativelanguage.googleapis.com/v1beta/models/{model}"
                   f":generateContent?key={gemini_key}")
            req = urllib.request.Request(url, data=payload,
                                         headers={"Content-Type": "application/json"},
                                         method="POST")
            with urllib.request.urlopen(req, timeout=12) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                txt = data["candidates"][0]["content"]["parts"][0]["text"].strip()
                if txt:
                    return txt
        except Exception:
            pass
    return None


def _rule_based_reply(query: str, hits: List[Tuple[str, float, Dict]]) -> str:
    qwords = _content_tokens(query)
    # Gom câu liên quan từ TẤT CẢ top chunks (ưu tiên chunk Sảnh khi hỏi sảnh/bàn).
    ql = _norm_vi(query)
    wants_hall = any(k in ql for k in ["sanh", "ban", "khach", "suc chua", "sanh nao"])
    ordered = sorted(hits, key=lambda h: (
        0 if (wants_hall and "sảnh" in (h[2].get("heading", "").lower())) else 1, -h[1]))
    picked_all: List[str] = []
    for text, _, _ in ordered:
        sentences = re.split(r"(?<=[.!?])\s+", text)
        rel = [re.sub(r"^\s*\[[^\]]+\]\s*", "", s).strip()
               for s in sentences if any(w in _norm_vi(s) for w in qwords[:10])]
        rel = [s for s in rel if s]
        picked_all.extend(rel[:2] if rel else
                          [re.sub(r"^\s*\[[^\]]+\]\s*", "", sentences[0]).strip()]
                          if sentences else [])
        if len(picked_all) >= 4:
            break
    lines = ["Dạ em là Trợ lý ảo Riverside Palace ạ! 💛"]
    if picked_all:
        lines.append(" ".join(picked_all[:4]).strip())
    # Bổ sung báo giá chuẩn nếu query hỏi giá.
    qln = _norm_vi(query)
    if any(k in qln for k in ["gia", "bao nhieu", "ban", "sanh", "menu", "thuc don", "ngan sach"]):
        lines.append("Báo giá chuẩn công bố: sảnh 20–80tr/ca (Elbe 20tr rẻ nhất, Grand Ballroom 80tr); "
                     "menu 4,35–5,9tr/bàn 7 món Á–Âu; decor 18–28tr; "
                     "dự toán = sảnh + menu×số bàn + decor + dịch vụ.")
    if any(k in qln for k in ["coc", "dat coc", "giu", "48h", "thanh toan", "huy", "doi", "ca ", "gio"]):
        lines.append("Chính sách: đặt trước 30–365 ngày; 2 ca Trưa 10:00–14:00 / Tối 17:30–21:30; "
                     "giữ chỗ 48h; thanh toán 3 đợt 20%–50%–quyết toán.")
    return "\n".join(lines)


def _slot_aware_closing(query: str, history: Optional[List]) -> str:
    """Câu kết chỉ hỏi slot còn thiếu (bàn/khách, ngày, ca, ngân sách)."""
    try:
        hist = list(history or []) + [{"role": "user", "content": query}]
        ents = extract_entities(hist)
    except Exception:
        ents = {}
    missing = []
    if not ents.get("so_ban_du_kien") and not ents.get("so_khach_du_kien"):
        missing.append("số bàn/khách dự kiến")
    if not ents.get("ngay_to_chuc"):
        missing.append("ngày tổ chức")
    if not ents.get("ca_to_chuc"):
        missing.append("ca (trưa/tối)")
    if not ents.get("ngan_sach_tran"):
        missing.append("ngân sách dự trù")
    known = []
    if ents.get("so_ban_du_kien"):
        g = ents.get("so_khach_du_kien") or ""
        known.append(f"{ents['so_ban_du_kien']} bàn" + (f" ({g} khách)" if g else ""))
    if ents.get("ngay_to_chuc"):
        known.append(f"ngày {ents['ngay_to_chuc']}")
    if ents.get("ca_to_chuc"):
        known.append(str(ents["ca_to_chuc"]).lower())
    if ents.get("ngan_sach_tran"):
        try:
            known.append(f"ngân sách {int(ents['ngan_sach_tran']):,} đ".replace(",", "."))
        except (ValueError, TypeError):
            pass
    if known and not missing:
        return ("Em đã ghi nhận " + ", ".join(known) + " ạ. Quý khách bấm nút "
                "“Áp dụng thông tin vào khảo sát” bên dưới để xem Top 3 combo, "
                "hoặc gọi Hotline 0933 777 888 để chuyên viên hỗ trợ ngay ạ!")
    if known:
        return ("Em đã ghi nhận " + ", ".join(known) + " ạ. Quý khách cho em xin thêm "
                + ", ".join(missing) + " để em gợi ý sảnh phù hợp nhất, hoặc gọi Hotline "
                "0933 777 888 để chuyên viên hỗ trợ ngay ạ!")
    return ("Quý khách cho em xin thêm số bàn/khách, ngày và ca tổ chức để em gợi ý sảnh "
            "phù hợp nhất, hoặc gọi Hotline 0933 777 888 để chuyên viên hỗ trợ ngay ạ!")


def chat(query: str, history: Optional[List] = None) -> Dict:
    ensure_ingested()
    if _is_greeting((query or "").strip()):
        return {"reply": _greeting_reply(), "sources": [],
                "confidence": 1.0, "is_out_of_scope": False}
    hits = search_context(query, top_k=3)
    max_score = max((s for _, s, _ in hits), default=0.0)
    # Ngưỡng spec 0.70 cho embedding ngữ nghĩa; fallback TF-IDF cộng cổng concept
    # (>=2 điểm và điểm thô >=0.30) để câu hỏi đa ý hợp lệ không bị loại nhầm.
    # Từ khóa ngoài miền (chứng khoán, thời tiết...) ép out-of-scope.
    out_of_scope = True
    if hits and max_score >= OUT_OF_SCOPE_THRESHOLD and not _forced_out_of_scope(query):
        out_of_scope = False
    elif (hits and max_score >= 0.30 and _concept_points(query) >= 2
            and not _forced_out_of_scope(query)):
        out_of_scope = False
    if out_of_scope:
        return {"reply": SAFE_OUT_OF_SCOPE, "sources": [],
                "confidence": round(float(max_score), 3), "is_out_of_scope": True}
    sources = [{"text": t[:500], "score": round(float(s), 3), **m} for t, s, m in hits]
    context = "\n---\n".join(t for t, _, _ in hits)
    hist_txt = ""
    try:
        if history:
            parts = []
            for m in history[-6:]:
                if isinstance(m, dict):
                    r = m.get("role", "user")
                    c = m.get("content", m.get("message", m.get("text", "")))
                    parts.append(f"{r}: {c}")
                else:
                    parts.append(str(m))
            hist_txt = "\n".join(parts)
    except Exception:
        hist_txt = ""
    prompt = ("Bạn là trợ lý ảo Riverside Palace, trả lời tiếng Việt thân thiện, xưng 'Dạ/em', "
              "chỉ dùng thông tin trong Context, kèm báo giá chuẩn và Hotline 0933 777 888.\n"
              f"Context:\n{context}\nLịch sử:\n{hist_txt}\nCâu hỏi: {query}\nTrả lời:")
    llm = _call_external_llm(prompt)
    if llm:
        return {"reply": llm, "sources": sources,
                "confidence": round(float(max_score), 3), "is_out_of_scope": False}
    body = _rule_based_reply(query, hits)
    closing = _slot_aware_closing(query, history)
    # _rule_based_reply luôn kết bằng câu hỏi slot mặc định -> thay bằng câu theo slot.
    if "\n" in body:
        head, _ = body.rsplit("\n", 1)
        body = head + "\n" + closing
    return {"reply": body, "sources": sources,
            "confidence": round(float(max_score), 3), "is_out_of_scope": False}


# ---------------------------------------------------------------- entities
_BUDGET_UNIT = {"ty": 1_000_000_000,
                "trieu": 1_000_000, "tr": 1_000_000,
                "m": 1_000_000,
                "nghin": 1_000, "k": 1_000, "d": 1,
                "vnd": 1, "dong": 1}


def _parse_budget(text: str) -> Optional[int]:
    t = _norm_vi(text).replace(",", "").replace(".", "").replace(" ", "")
    # Dạng số dài: 250000000 / 250000000vnd
    m = re.search(r"(\d{7,10})(vnd|d|dong)?", t)
    if m:
        try:
            v = int(m.group(1))
            if 1_000_000 <= v <= 3_000_000_000:
                return v
        except ValueError:
            pass
    # Dạng số + đơn vị (đã bỏ dấu): 250trieu, 250tr, 2.5ty, 250m
    t2 = _norm_vi(text).replace(",", ".")
    for mm in re.finditer(r"(\d+(?:\.\d+)?)\s*(ty|trieu|tr|m|nghin|k)\b", t2):
        try:
            num = float(mm.group(1))
        except ValueError:
            continue
        mult = _BUDGET_UNIT.get(mm.group(2), 0)
        if mult:
            v = int(num * mult)
            if 1_000_000 <= v <= 3_000_000_000:
                return v
    return None


def _parse_tables(text: str) -> Optional[int]:
    m = re.search(r"(\d{1,3})\s*ban", _norm_vi(text))
    if m:
        try:
            v = int(m.group(1))
            if 1 <= v <= 200:
                return v
        except ValueError:
            pass
    return None


def _parse_guests(text: str) -> Optional[int]:
    m = re.search(r"(\d{2,4})\s*khach", _norm_vi(text))
    if m:
        try:
            v = int(m.group(1))
            if 10 <= v <= 2000:
                return v
        except ValueError:
            pass
    return None


def _parse_date(text: str) -> Optional[str]:
    m = re.search(r"(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})", text)
    if m:
        try:
            d = date(int(m.group(3)), int(m.group(2)), int(m.group(1)))
            return d.isoformat()
        except ValueError:
            pass
    m2 = re.search(r"ngay\s*(\d{1,2})\s*thang\s*(\d{1,2})(?:\s*nam\s*(\d{4}))?", _norm_vi(text))
    if m2:
        try:
            y = int(m2.group(3)) if m2.group(3) else date.today().year
            return date(y, int(m2.group(2)), int(m2.group(1))).isoformat()
        except ValueError:
            pass
    return None


def _parse_shift(text: str) -> Optional[str]:
    t = _norm_vi(text)
    if re.search(r"ca\s*toi|buoi\s*toi|\btoi\b|evening|dinner|17:?30|21:?30", t):
        return "Ca tối"
    if re.search(r"ca\s*trua|buoi\s*trua|\btrua\b|noon|lunch|10:?00|14:?00", t):
        return "Ca trưa"
    return None


_STYLE_KEYS = [
    ("Hoàng gia", ["hoang gia", "royal", "versailles", "gold"]),
    ("Hiện đại / lãng mạn", ["hien dai", "modern", "celestial", "blue", "midnight"]),
    ("Sân vườn / lãng mạn", ["san vuon", "vuon", "garden", "rustic", "botanical", "paris", "hong"]),
    ("Cổ tích / lãng mạn", ["co tich", "cong chua", "princess", "fairy", "mong", "diep"]),
    ("Nhẹ nhàng", ["nhe nhang", "minimal", "tinh gian", "trang", "ngoc"]),
    ("Thiên nhiên / ngoài trời", ["thien nhien", "ngoai troi", "ven song", "nature", "outdoor"]),
    ("Lãng mạn", ["lang man", "romantic", "love"]),
]


def _parse_style(text: str) -> Optional[str]:
    t = _norm_vi(text)
    for label, keys in _STYLE_KEYS:
        for k in keys:
            # Key ngắn (<=4 ký tự) phải khớp nguyên từ để "khong"/"phong" không
            # nhận nhầm thành "hong" (Vườn Hồng), "mong" (Mộng)...
            if len(k) <= 4:
                if re.search(rf"(?<![a-z]){re.escape(k)}(?![a-z])", t):
                    return label
            elif k in t:
                return label
    return None


def _iter_texts_newest_first(history: List) -> List[str]:
    texts = []
    for m in reversed(history or []):
        if isinstance(m, dict):
            c = m.get("content", m.get("message", m.get("text", "")))
            if c:
                texts.append(str(c))
        elif isinstance(m, str):
            texts.append(m)
    return texts


def extract_entities(history: List) -> Dict:
    """Bóc entities, ưu tiên message mới nhất."""
    out: Dict = {"ngan_sach_tran": None, "so_ban_du_kien": None,
                 "so_khach_du_kien": None, "ngay_to_chuc": None,
                 "ca_to_chuc": None, "phong_cach": None}
    texts = _iter_texts_newest_first(history)
    for t in texts:
        if out["ngan_sach_tran"] is None:
            v = _parse_budget(t)
            if v:
                out["ngan_sach_tran"] = v
        if out["so_ban_du_kien"] is None:
            v = _parse_tables(t)
            if v:
                out["so_ban_du_kien"] = v
        if out["so_khach_du_kien"] is None:
            v = _parse_guests(t)
            if v:
                out["so_khach_du_kien"] = v
        if out["ngay_to_chuc"] is None:
            v = _parse_date(t)
            if v:
                out["ngay_to_chuc"] = v
        if out["ca_to_chuc"] is None:
            v = _parse_shift(t)
            if v:
                out["ca_to_chuc"] = v
        if out["phong_cach"] is None:
            v = _parse_style(t)
            if v:
                out["phong_cach"] = v
        if all(v is not None for v in out.values()):
            break
    # Suy luận lẫn nhau 1 bàn = 10 khách.
    if out["so_ban_du_kien"] and not out["so_khach_du_kien"]:
        out["so_khach_du_kien"] = out["so_ban_du_kien"] * 10
    elif out["so_khach_du_kien"] and not out["so_ban_du_kien"]:
        out["so_ban_du_kien"] = math.ceil(out["so_khach_du_kien"] / 10)
    return out
