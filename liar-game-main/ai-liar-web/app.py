"""Local browser prototype: PDF-grounded AI liar game (separate from original Socket.IO rooms)."""
import io
import json
import os
import random
import re
import secrets
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path
import fitz
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.responses import FileResponse
from pydantic import BaseModel, Field
app = FastAPI(title='PDF AI Liar Game Prototype')
ROOT = Path(__file__).parent
MAX_UPLOAD = 40 * 1024 * 1024
SESSIONS = {}
NAMES = ['지민', '서준', '하린', '도윤', '수아', '민재']
PERSONAS = [
    "분석적이고 간결하게 설명함",
    "자신감 있고 구체적으로 설명함",
    "신중하게 핵심 원리를 설명함",
    "비유와 사례를 활용해 설명함",
    "다른 설명의 정확성을 검토하고 정리함",
    "차분하게 개념의 특징을 정리함"
]
STOP = {'그리고','하지만','그래서','이러한','이것은','때문에','있습니다','합니다','경우에','사용하여','통해서','데이터','문제','방법','설명','정의','내용','학습','사용','관련','기능','학생','교수','AI','PDF'}


@dataclass
class Session:
    source: str = '예제 텍스트'
    pages: list = field(default_factory=list)
    concept: str = ''
    evidence: str = ''
    liar: int = 0
    round: int = 0
    max_turns: int = 6
    messages: list = field(default_factory=list)
    mode: str = 'demo'
    liar_hint: str = ''
    npc_topics: list = field(default_factory=list)
    learning_profile: dict = field(default_factory=dict)
    misconception_review: dict = field(default_factory=dict)
    completed: bool = False


class StartRequest(BaseModel):
    session_id: str
    concept: str = Field(min_length=2, max_length=100)


class TurnRequest(BaseModel):
    session_id: str


class VoteRequest(BaseModel):
    session_id: str
    candidate: int = Field(ge=0, le=5)


def get_session(sid):
    item = SESSIONS.get(sid)
    if not item:
        raise HTTPException(404, '세션을 찾을 수 없습니다. 페이지를 새로고침하고 다시 시작하세요.')
    return item


def safe_excerpt(s, maxlen=330):
    s = re.sub(r'\s+', ' ', s).strip()
    return s[:maxlen].strip()


def sentences(text):
    return [x.strip() for x in re.split(r'(?<=[.!?。])\s+|\n+', text) if len(x.strip()) > 12]


def candidate_concepts(pages):
    text = '\n'.join(pages)
    # Academic PDFs often define terms in a heading, parentheses, or before a colon.
    candidates = []
    for pat in [
        r'([가-힣A-Za-z][가-힣A-Za-z0-9 +/\-]{2,28})\s*\(([A-Za-z][A-Za-z0-9 \-]{2,35})\)',
        r'(?:^|\n)\s*([가-힣A-Za-z][가-힣A-Za-z0-9 +/\-]{2,25})\s*(?:\n|:)'
    ]:
        for m in re.finditer(pat, text):
            term = m.group(1).strip(' -:')
            if 2 <= len(term) <= 30 and term not in STOP and term not in candidates:
                candidates.append(term)
    # Fall back to repeated Korean nouns (not semantic extraction).
    counts = {}
    for m in re.finditer(r'(?<![가-힣])([가-힣]{3,12})(?![가-힣])', text):
        w = m.group(1)
        if w not in STOP and not w.endswith(('합니다', '입니다', '됩니다')):
            counts[w] = counts.get(w, 0) + 1
    candidates += [w for w, n in sorted(counts.items(), key=lambda x: -x[1]) if n >= 2 and w not in candidates]
    return candidates[:18]


def evidence_for(pages, term):
    hits=[]
    for pidx,p in enumerate(pages):
        for st in sentences(p):
            if term.lower() in st.lower():
                hits.append((pidx+1,safe_excerpt(st,240)))
    return hits[:6]


def pdf_context_for(pages, term, max_items=6):
    """Return page-labelled source excerpts including context around a term."""
    matches = []
    seen = set()
    for page_number, page_text in enumerate(pages, start=1):
        for match in list(re.finditer(re.escape(term), page_text, re.IGNORECASE))[:3]:
            start = max(0, match.start() - 180)
            end = min(len(page_text), match.end() + 850)
            excerpt = safe_excerpt(page_text[start:end], 1050)
            key = re.sub(r"\s+", "", excerpt).lower()
            if len(key) < 15 or key in seen:
                continue
            seen.add(key)
            matches.append((page_number, excerpt))
            if len(matches) >= max_items:
                return matches
    return matches


def pdf_reference_text(s, max_chars=6500):
    return "\n".join(
        f"PDF {page}페이지 원문: {excerpt}"
        for page, excerpt in pdf_context_for(s.pages, s.concept)
    )[:max_chars]


def llm_chat(messages, temperature=0.7):
    key = os.getenv("LIAR_LLM_API_KEY", "").strip()
    url = os.getenv("LIAR_LLM_API_URL", "").strip()
    model = os.getenv("LIAR_LLM_MODEL", "").strip()
    if not all([key, url, model]):
        raise RuntimeError(
            "LLM 환경변수가 설정되지 않았습니다."
        )
    prepared_messages = [
        dict(message)
        for message in messages
    ]
    # Qwen3의 비사고 모드 사용 요청
    for message in prepared_messages:
        if message["role"] == "user":
            message["content"] = (
                "/no_think\n" + message["content"]
            )
    body = {
        "model": model,
        "messages": prepared_messages,
        "temperature": temperature,
        "max_tokens": 2048,
        "stream": False
    }
    request_data = json.dumps(
        body,
        ensure_ascii=False
    ).encode("utf-8")
    request = urllib.request.Request(
        url,
        data=request_data,
        headers={
            "Authorization": f"Bearer {key}",
            "Content-Type": "application/json"
        },
        method="POST"
    )
    with urllib.request.urlopen(
        request,
        timeout=180
    ) as response:
        result = json.load(response)
    choice = result["choices"][0]
    message = choice.get("message", {})
    content = message.get("content") or ""
    reasoning = message.get("reasoning_content") or ""
    finish_reason = choice.get("finish_reason")
    print(
        "[LLM 응답 확인]",
        f"content 길이: {len(content)}",
        f"reasoning 길이: {len(reasoning)}",
        f"종료 사유: {finish_reason}",
        flush=True
    )
    # 응답 본문에 포함된 사고 과정 제거
    output = re.sub(
        r"<think>.*?</think>",
        "",
        content,
        flags=re.DOTALL
    ).strip()
    # 사고 태그가 닫히지 않은 경우
    if "<think>" in output:
        output = output.split("<think>")[0].strip()
    if not output:
        raise RuntimeError(
            "LLM 응답이 비어 있습니다. "
            f"content={len(content)}, "
            f"reasoning={len(reasoning)}, "
            f"finish_reason={finish_reason}"
        )
    return output


def build_learning_profile(s):
    # 짧은 제목만 가져오는 대신 제시어 주변 원문까지 참고한다.
    references = [
        f"PDF {page}페이지 원문: {excerpt}"
        for page, excerpt in pdf_context_for(s.pages, s.concept)
    ]
    pdf_context = "\n".join(references)[:6500]
    prompt = f"""
당신은 대학교 수업용 학습 프로필 작성자입니다.
[제시어]
{s.concept}
[업로드 PDF에서 추출한 내용]
{pdf_context or "관련 원문을 확보하지 못했습니다."}
제시어를 설명하는 학습 프로필을 생성하세요.
이 게임에서 PDF에 명시된 수업 설명은 정답 판정의 최우선 기준입니다.
PDF 내용과 모델의 기존 지식이 다를 경우 수업 PDF의 서술을 따르세요.
PDF에 없는 내용만 사전 학습 지식으로 보완할 수 있습니다.
보완 정보는 PDF에서 직접 확인된 사실인 것처럼 표시하지 마세요.
PDF 원문이 짧더라도 있는 내용을 고치거나 반박하지 마세요.
중요한 원칙:
- PDF의 명시적인 설명을 변경하거나 학문적 오류로 재판정하지 마세요.
- PDF에 직접 나온 사실과 모델이 보완한 지식을 구별하세요.
- PDF에서 확인되지 않은 내용을 PDF의 사실이라고 주장하지 마세요.
- 추측성 정보는 uncertainties에 기록하세요.
- 제시어가 다양한 분야일 수 있으므로
  알고리즘이라는 전제를 두지 마세요.
- 서로 겹치지 않는 NPC 설명 주제를 5개 제안하세요.
- 각 주제는 짧고 구체적인 한국어 명사구여야 합니다.
- 잘못된 개념을 사실처럼 생성하지 마세요.
- 언제나, 반드시, 모든 경우 등 무조건적인 단정을 피하고 적용 조건을 명시하세요.
- 해당 개념과 유사한 다른 개념을 동일시하지 마세요.
- 활용 사례는 대표적이고 널리 확인되는 예시만 사용하세요.
- 비교 문장에서 우월성이나 정답 보장을 일반화하지 마세요.
다음 JSON 객체만 출력하세요.
마크다운 코드 블록은 사용하지 마세요.
{{
    "definition": "제시어의 기본 정의",
    "principles": [
        "핵심 원리 또는 중요한 특징 1",
        "핵심 원리 또는 중요한 특징 2",
        "핵심 원리 또는 중요한 특징 3"
    ],
    "applications": [
        "실제 사용 사례 또는 적용 상황"
    ],
    "comparisons": [
        "비교하기 좋은 관련 개념과 차이"
    ],
    "uncertainties": [
        "PDF나 모델 지식만으로 확실하지 않은 내용"
    ],
    "topics": [
        "NPC 담당 주제 1",
        "NPC 담당 주제 2",
        "NPC 담당 주제 3",
        "NPC 담당 주제 4",
        "NPC 담당 주제 5"
    ]
}}
"""
    response = llm_chat(
        [
            {
                "role": "system",
                "content": (
                    "당신은 교육용 지식 구조화 도우미입니다. "
                    "정확한 JSON 객체만 출력하세요. "
                    "PDF에서 확인한 내용과 사전 학습 지식을 "
                    "혼동하지 마세요."
                )
            },
            {
                "role": "user",
                "content": prompt
            }
        ],
        temperature=0.2
    ).strip()
    print(
        "[학습 프로필 원본 응답]",
        response[:3000],
        flush=True
    )
    # JSON 외의 앞뒤 설명이 나왔을 때도 객체만 추출
    first = response.find("{")
    last = response.rfind("}")
    if first == -1 or last <= first:
        raise ValueError(
            "학습 프로필 응답에서 JSON 객체를 찾지 못했습니다."
        )
    profile = json.loads(response[first:last + 1])
    if not isinstance(profile, dict):
        raise ValueError(
            "학습 프로필은 JSON 객체여야 합니다."
        )
    for key in (
        "principles",
        "applications",
        "comparisons",
        "uncertainties",
        "topics"
    ):
        value = profile.get(key)
        if not isinstance(value, list):
            raise ValueError(
                f"학습 프로필의 {key} 항목이 목록이 아닙니다."
            )
        profile[key] = [
            item.strip()
            for item in value
            if isinstance(item, str) and item.strip()
        ]
    definition = profile.get("definition")
    if not isinstance(definition, str) or not definition.strip():
        raise ValueError(
            "학습 프로필의 핵심 정의가 비어 있습니다."
        )
    profile["definition"] = definition.strip()
    # 모든 NPC에게 서로 다른 주제를 배정해야 하므로 검사
    unique_topics = list(
        dict.fromkeys(profile["topics"])
    )
    if len(unique_topics) != 5:
        raise ValueError(
            "학습 프로필에 서로 다른 담당 주제 5개가 필요합니다."
        )
    profile["topics"] = unique_topics
    # 출처는 실제 검증 상태와 구별하여 기록
    profile["concept"] = s.concept
    profile["pdf_references"] = references
    profile["knowledge_status"] = "pdf_primary_model_supplement_unverified"
    profile["external_search_used"] = False
    print(
        "[학습 프로필 생성 완료]",
        json.dumps(profile, ensure_ascii=False),
        flush=True
    )
    return profile


def create_misconception(s):
    """Create a plausible false claim and check that its error can be explained."""
    profile = s.learning_profile
    pdf = pdf_reference_text(s, 5500)
    guide = json.dumps({
        "definition": profile.get("definition", ""),
        "principles": profile.get("principles", []),
        "comparisons": profile.get("comparisons", []),
        "uncertainties": profile.get("uncertainties", [])
    }, ensure_ascii=False)[:3000]
    last_error = ""
    previous_claims = []

    for attempt in range(5):
        prompt = f"""대학 학습용 라이어 게임에서 쓸 '명확히 틀린' 학생 발언을 생성하세요.
[제시어] {s.concept}
[수업의 우선 정답 기준: PDF 실제 원문] {pdf}
[PDF에 없는 부분의 보충 참고: 모델 생성, 미검증] {guide}
[이미 기각한 발언] {json.dumps(previous_claims[-3:], ensure_ascii=False)}
[직전 실패 이유] {last_error or '없음'}

먼저 머릿속으로 사실 한 가지와 그 적용 조건을 확인하고,
그 사실의 핵심 인과관계, 필요조건 또는 작동 원리를 뒤집어
구체적인 오개념을 한 문장으로 만드세요.

중요:
- 가장 먼저 PDF에 명시된 핵심 설명을 하나 찾고, 그 원리나 조건을
  의도적으로 잘못 이해한 발언을 작성하세요.
- PDF에 관련 설명이 매우 짧다면, 그 설명과 모순되는 오류를 우선 만드세요.
- PDF에 해당 주장을 판단할 내용이 전혀 없을 때만 일반 전공지식을 활용하세요.
- 모델 참고지식이 PDF와 다르면 PDF를 수업 정답으로 따르세요.
- 참고지식의 주장도 틀릴 수 있으므로 그대로 정답으로 가정하지 마세요.
- '항상 더 빠르다', '모든 경우 최적이다' 같은 광범위한 단정 대신,
  반례나 모순을 구체적으로 설명할 수 있는 명확한 오류를 우선하세요.
- 지난 시도의 단순한 표현 변경은 하지 마세요. 오류 유형을 바꾸세요.
- 사실적인 문장, 질문, 설명문, 오류 해설을 출력하지 마세요.
- 정답 제시어를 직접 말하지 말고 한국어 구어체 평서문 딱 한 문장만 작성하세요.

실제 학생이 말할 오개념 한 문장:"""
        claim = llm_chat([
            {
                "role": "system",
                "content": "교육용 오개념 생성기입니다. 틀린 학생 발언 한 문장만 출력하세요."
            },
            {"role": "user", "content": prompt}
        ], temperature=0.6).strip(' "\'“”')

        if not claim or len(claim) > 350 or '?' in claim or '？' in claim:
            last_error = "문장 형식이 올바르지 않습니다."
            continue
        if claim in previous_claims:
            last_error = "이미 검토한 문장과 동일합니다. 다른 유형의 오류가 필요합니다."
            continue
        previous_claims.append(claim)

        check_prompt = f"""[제시어] {s.concept}
[오개념 후보 발언] {claim}
[게임의 우선 정답 기준: PDF 실제 원문] {pdf}
[PDF에 없는 부분에 대한 보충 참고: 모델 생성, 미검증] {guide}

판정 우선순위 (반드시 지킬 것):
1. PDF가 후보 발언의 주장을 명시적으로 지지하면 VALID로 판정하세요.
   모델의 기존 지식이 다르다는 이유로 PDF의 설명을 반박하지 마세요.
2. PDF가 후보 발언의 주장과 명시적으로 모순되면 INVALID입니다.
   어느 PDF 설명과 어떻게 모순되는지 구체적으로 설명하세요.
3. PDF가 그 주장에 대한 판단 근거를 제공하지 않는 경우에만
   일반적으로 확립된 전공지식으로 판단하세요.
4. 모델이 만든 참고 프로필은 PDF와 동급의 정답 근거가 아닙니다.
   참고 프로필만 근거로 후보를 INVALID로 판정하지 마세요.
5. PDF에 없다는 이유만으로 UNCERTAIN으로 판정하지 마세요.
   하지만 근거를 확보할 수 없는 경우에는 UNCERTAIN으로 판정하세요.
6. PDF에서 명시된 사실과 AI 보충지식은 판단 이유에서 구분하세요.
7. '항상', '모든' 등 단정에는 반례가 있는지 확인하세요.

판정 기준:
INVALID = PDF에 명시된 내용과 모순되거나 PDF에 없는 범위에서 명확히 반박 가능
VALID = PDF 설명과 일치하거나 PDF에 없는 범위에서 올바른 설명
UNCERTAIN = PDF도 해당 주장을 판단하지 못하고 전공지식으로도 불확실함

반드시 세 줄로만 출력하세요.
VERDICT: INVALID 또는 VALID 또는 UNCERTAIN
REASON: 판단 근거. PDF 근거가 있다면 PDF의 구체적인 설명을 적기
CORRECTION: INVALID일 때 PDF 또는 확립된 지식에 맞는 수정 설명 (그 외에는 없음)"""
        check = llm_chat([
            {
                "role": "system",
                "content": (
                    "교육용 사실 판정자입니다. 수업 PDF의 명시적인 설명을 "
                    "게임의 우선 정답으로 사용하세요. PDF에 없는 경우에만 "
                    "확립된 일반 지식을 보충하고 출처를 구별하세요."
                )
            },
            {"role": "user", "content": check_prompt}
        ], temperature=0.1)
        verdict = re.search(
            r'^VERDICT:\s*(INVALID|VALID|UNCERTAIN)\s*$',
            check, re.MULTILINE | re.IGNORECASE
        )
        reason = re.search(r'^REASON:\s*(.+)$', check, re.MULTILINE | re.IGNORECASE)
        correction = re.search(r'^CORRECTION:\s*(.+)$', check, re.MULTILINE | re.IGNORECASE)
        status = verdict.group(1).upper() if verdict else "FORMAT_ERROR"
        rationale = reason.group(1).strip() if reason else "검토 응답 형식이 잘못되었습니다."
        fix = correction.group(1).strip() if correction else ""
        print(
            f"[오개념 검토] 시도={attempt + 1}, 판정={status}, "
            f"발언={claim}, 이유={rationale}, 수정={fix}",
            flush=True
        )

        if status == "INVALID" and rationale and fix and fix != "없음":
            s.misconception_review = {
                "original_claim": claim,
                "reason": rationale,
                "correction": fix,
                "status": "model_reviewed_unverified"
            }
            print("[오개념 검토 통과]", s.misconception_review, flush=True)
            return re.sub(
                re.escape(s.concept.strip()), "이 개념", claim,
                flags=re.IGNORECASE
            )

        last_error = (
            f"검토 판정 {status}: {rationale}. "
            "검증할 수 있는 서로 다른 오개념을 생성하세요."
        )

    raise ValueError(
        "5회 시도에도 오류를 뒷받침할 수 있는 오개념을 만들지 못했습니다. "
        "검증되지 않은 발언을 라이어의 정답으로 사용하지 않습니다."
    )


def review_expert_line(s, output, topic):
    reference = pdf_reference_text(s, 6500)
    review_prompt = f"""
당신은 대학교 교육용 수업의
사실 정확성 및 담당 주제 검토자입니다.
[정답 개념]
{s.concept}
[학생에게 배정된 담당 주제]
{topic}
[검토할 학생의 발언]
{output}
[수업의 우선 정답 기준: 업로드 PDF 실제 원문]
{reference or "관련 문장을 찾지 못했습니다."}
[PDF에 없는 내용의 보완 참고: LLM이 생성한 공통 프로필, 미검증]
{json.dumps(s.learning_profile, ensure_ascii=False)[:3500]}
판정 우선순위:
1. 발언의 구체적인 주장이 PDF에 명시된 설명과 일치하면
   그 설명을 게임의 정답으로 인정하고 정확성을 VALID로 판정하세요.
   PDF에 대해 모델 지식으로 이의를 제기하지 마세요.
2. 발언이 PDF의 명시적 설명과 모순되면 INVALID로 판정하세요.
3. PDF에서 판단할 수 없는 추가 설명만 일반 전공지식으로 평가하세요.
   확실한 오류면 INVALID, 근거 부족이면 UNCERTAIN입니다.
4. 학습 프로필은 검증되지 않은 AI 보완 정보일 수 있으므로,
   PDF와 충돌할 때 PDF를 우선하고 프로필을 정답으로 단정하지 마세요.
5. 사실 정확성과 담당 주제 적합성은 별도로 평가하세요.
6. 정확성 판단 이유에 PDF의 구체적인 문구 또는 모델 보충지식을 구별하세요.
다음 두 항목을 독립적으로 평가하세요.
1. 사실 정확성 (ACCURACY)
VALID:
발언에서 명확한 사실 오류를 발견하지 못함.
INVALID:
잘못된 개념 원리, 관계, 실행 순서,
조건 또는 명확한 오개념이 포함됨.
UNCERTAIN:
자료와 지식만으로 정확성을 판단하기 어려움.
2. 담당 주제 적합성 (RELEVANCE)
HIGH:
학생이 배정받은 담당 주제를
실질적으로 설명함.
LOW:
학생의 발언이 정확하더라도
담당 주제를 설명하지 않고
다른 내용만 설명함.
UNCERTAIN:
담당 주제와의 관련성을 판단하기 어려움.
[중요한 평가 원칙]
- 사실 정확성과 주제 적합성을 구별하세요.
- 사실적으로 정확해도 주제에서 벗어나면 LOW입니다.
- 담당 주제를 다른 표현으로 설명했다면 HIGH입니다.
- 단순히 담당 주제의 단어를 사용했다고 HIGH가 아닙니다.
- 담당 주제의 모든 내용을 설명할 필요는 없습니다.
- 짧은 발언이라는 이유만으로 LOW라고 하지 마세요.
- PDF에 없다는 이유만으로 틀렸다고 판단하지 마세요.
- 확실하지 않은 사실 오류를 만들어 내지 마세요.
반드시 다음 세 줄 형식으로만 답하세요.
ACCURACY: VALID
RELEVANCE: HIGH
REASON: 정확성과 주제 적합성에 대한 판단 이유
위 세 줄은 출력 형식 예시입니다.
실제 평가 결과에 따라 값을 변경하세요.
"""
    response = llm_chat(
        [
            {
                "role": "system",
                "content": (
                    "당신은 대학 교육용 발언 검토자입니다. "
                    "수업 PDF에 명시된 내용을 게임의 우선 정답으로 삼으세요. "
                    "사실 정확성과 담당 주제 적합성을 "
                    "독립적으로 평가하세요. "
                    "지정된 세 줄 형식으로 답하세요."
                )
            },
            {
                "role": "user",
                "content": review_prompt
            }
        ],
        temperature=0.1
    ).strip()
    accuracy_match = re.search(
        r"^ACCURACY:\s*(VALID|INVALID|UNCERTAIN)\s*$",
        response,
        flags=re.MULTILINE | re.IGNORECASE
    )
    relevance_match = re.search(
        r"^RELEVANCE:\s*(HIGH|LOW|UNCERTAIN)\s*$",
        response,
        flags=re.MULTILINE | re.IGNORECASE
    )
    reason_match = re.search(
        r"^REASON:\s*(.+)$",
        response,
        flags=re.MULTILINE | re.IGNORECASE
    )
    accuracy = (
        accuracy_match.group(1).upper()
        if accuracy_match
        else "UNCERTAIN"
    )
    relevance = (
        relevance_match.group(1).upper()
        if relevance_match
        else "UNCERTAIN"
    )
    reason = (
        reason_match.group(1).strip()
        if reason_match
        else "검토 결과 형식을 해석하지 못했습니다."
    )
    return accuracy, relevance, reason


def generate_line(s, idx, turn):
    from difflib import SequenceMatcher
    name = NAMES[idx]
    is_liar = idx == s.liar
    # 게임 시작 시 정해진 발언 분야
    topic = s.npc_topics[idx]
    print(
        f"[NPC 생성] {name}, "
        f"역할={'라이어' if is_liar else '전공자'}, "
        f"주제={topic}, 라운드={turn + 1}",
        flush=True
    )
    # 제시어를 그대로 드러내는 표현만 감춤. 분야별 별칭은 하드코딩하지 않음.
    def hide_answer(text):
        return re.sub(
            re.escape(s.concept.strip()),
            "이 개념",
            text,
            flags=re.IGNORECASE
        ).strip()
    # 공개된 발언만 대화 기록으로 사용
    previous_lines = [
        message["text"]
        for message in s.messages
    ]
    history = "\n".join(
        f'{NAMES[m["speaker"]]}: {m["text"]}'
        for m in s.messages[-11:]
    ) or "아직 발언이 없습니다."
    # 라이어의 오개념은 게임 시작 시 미리 생성 및 검토함.
    # 예외적으로 빈 경우에도 같은 범용 검증 경로 사용.
    if is_liar and not s.liar_hint:
        s.liar_hint = create_misconception(s)
        print("[라이어 오개념 확정]", s.liar_hint, flush=True)
    # 라이어는 확정된 오개념을 그대로 발언
    # LLM으로 재작성하면 오류가 수정될 수 있으므로
    # 두 번째 생성 요청을 하지 않음
    if is_liar:
        liar_line = s.liar_hint.strip()
        print(
            f"[라이어 발언 확정] {name}: {liar_line}",
            flush=True
        )
        return liar_line
    # 전공자에게만 정답 개념과 공통 프로필 제공
    pdf_context = pdf_reference_text(s, 6500)
    other_topics = [
        s.npc_topics[i]
        for i in range(6)
        if i != idx and i != s.liar
    ]
    role_prompt = f"""
당신은 교육용 라이어 게임의 전공자입니다.
정답 개념: {s.concept}
반드시 집중할 담당 분야:
{topic}
다른 전공자들의 담당 분야:
{", ".join(other_topics)}
[게임의 우선 정답: 실제 PDF 원문]
{pdf_context or "관련 문장이 없습니다."}
[PDF 설명이 부족할 때만 보충하는 학습 프로필: 미검증]
{json.dumps(s.learning_profile, ensure_ascii=False)[:3500]}
PDF에 명시된 수업 설명을 우선하여 발언하세요.
모델의 사전 지식과 PDF가 다르면 PDF의 설명을 따르세요.
PDF에 없는 부분은 모델 지식으로 보완할 수 있으나
해당 내용을 PDF에서 직접 읽었다고 주장하지 마세요.
불확실한 지식은 사실로 단정하지 마세요.
담당 주제에 관련된 구체적인 내용을 설명하세요.
행동 규칙:
1. 자신의 담당 분야만 중심으로 설명하세요.
2. 다른 전공자의 담당 분야는 가급적 설명하지 마세요.
3. 개념의 정의를 매번 반복하지 마세요.
4. 정확한 전공지식을 활용하세요.
5. PDF 문장을 그대로 복사하지 마세요.
6. 가능하면 구체적인 사례나 원리를 말하세요.
7. 다른 NPC의 발언과 내용이 겹치지 않게 하세요.
8. 질문하지 말고 평서문으로 설명하세요.
9. 정답 제시어의 이름을 직접 말하지 마세요.
10. 근거가 없는 사실은 만들어 내지 마세요.
"""
    # 라운드에 따라 설명 방식 변경
    if turn == 0:
        round_instruction = """
이번은 첫 번째 발언입니다.
자신의 담당 분야에서 가장 핵심적인
특징 하나를 설명하세요.
다른 참가자의 의견을 평가할 필요는 없습니다.
"""
    else:
        round_instruction = """
이번은 두 번째 발언입니다.
첫 번째 발언을 그대로 반복하지 마세요.
이전에 설명하지 않은 추가적인 특징이나
구체적 사례를 설명하세요.
앞선 발언 중 부정확한 내용이 있다면
질문 없이 자연스럽게 정정할 수 있습니다.
"""
    prompt = f"""
당신은 대학생 AI 라이어 게임의 NPC입니다.
이름: {name}
성격: {PERSONAS[idx]}
[역할 및 전문 분야]
{role_prompt}
[라운드별 지시]
{round_instruction}
[지금까지 공개된 대화]
{history}
[최종 발언 조건]
- 한국어 구어체를 사용하세요.
- 1~2문장으로만 발언하세요.
- 질문하지 마세요.
- 물음표를 사용하지 마세요.
- 제시어 이름을 말하지 마세요.
- 자신의 역할을 공개하지 마세요.
- 이전 NPC의 설명을 반복하지 마세요.
- 추상적인 말보다 구체적인 내용을 우선하세요.
- 자신의 발언 내용만 출력하세요.
"""
    best_output = ""
    last_rejection = ""
    for attempt in range(3):
        output = llm_chat(
            [
                {
                    "role": "system",
                    "content": (
                        "당신은 교육용 추리 게임의 NPC입니다. "
                        "반드시 한국어 평서문 1~2개만 작성하세요. "
                        "질문하거나 정답 이름을 말하지 마세요. "
                        "담당 분야를 우선해서 설명하세요."
                    )
                },
                {
                    "role": "user",
                    "content": prompt
                }
            ],
            temperature=0.7
        ).strip()
        output = hide_answer(
            output.strip(' "\'“”')
        )
        similarity = max(
            (
                SequenceMatcher(
                    None,
                    output,
                    previous
                ).ratio()
                for previous in previous_lines
            ),
            default=0.0
        )
        question_pattern = (
            r"(나요|까요|습니까|인가요|일까요)"
            r"\s*[.!]?$"
        )
        contains_question = (
            "?" in output
            or "？" in output
            or bool(
                re.search(
                    question_pattern,
                    output
                )
            )
        )
        if not output or contains_question:
            last_rejection = "질문이 아닌 평서문으로 작성하세요."
        elif similarity >= 0.68:
            last_rejection = (
                "이전 참가자의 발언과 너무 비슷합니다. "
                "담당 분야의 다른 특징이나 사례를 설명하세요."
            )
        else:
            try:
                accuracy, relevance, reason = review_expert_line(
                    s,
                    output,
                    topic
                )
            except Exception as e:
                print(
                    "[전공자 검토 오류]",
                    str(e),
                    flush=True
                )
                accuracy = "UNCERTAIN"
                relevance = "UNCERTAIN"
                reason = "검토 요청에 실패했습니다."
            print(
                f"[전공자 통합 검토] "
                f"이름={name}, "
                f"정확성={accuracy}, "
                f"주제적합성={relevance}, "
                f"이유={reason}",
                flush=True
            )
            # 정확성과 담당 주제가 모두 통과해야 승인
            if accuracy == "VALID" and relevance == "HIGH":
                print(
                    f"[전공자 발언 완료] {name}: {output}",
                    flush=True
                )
                return output
            if accuracy == "INVALID":
                last_rejection = (
                    "이전 발언에 사실 오류가 있습니다. "
                    f"검토 이유: {reason} "
                    "잘못된 원리를 수정하고 "
                    f"담당 주제인 '{topic}'을 "
                    "정확하게 설명하세요."
                )
            elif relevance == "LOW":
                last_rejection = (
                    "발언이 담당 주제에서 벗어났습니다. "
                    f"담당 주제는 '{topic}'입니다. "
                    f"검토 이유: {reason} "
                    "담당 주제를 직접 설명하는 "
                    "새로운 발언을 작성하세요."
                )
            else:
                last_rejection = (
                    "발언 검토 결과가 불확실합니다. "
                    f"검토 이유: {reason} "
                    f"담당 주제인 '{topic}'에 집중하여 "
                    "확실한 사실만 설명하세요."
                )
        print(
            f"[발언 재작성] {name}, "
            f"시도={attempt + 1}, "
            f"사유={last_rejection}",
            flush=True
        )
        prompt += (
            "\n\n[재작성 요청]\n"
            + last_rejection
            + "\n질문하지 마세요."
            + "\n제시어를 직접 말하지 마세요."
            + "\n한국어 평서문 1~2개만 작성하세요."
        )
    # 검토에 반복 실패해도 게임 전체를 중단하지 않음
    print(
        f"[NPC 검토 실패] {name}: 안전한 대체 발언 사용",
        flush=True
    )
    return (
        "제시된 자료에서 적용 조건과 실행 과정을 "
        "구분해서 살펴보는 것이 중요하다고 생각해요."
    )


@app.get('/')
def home():
    return FileResponse(ROOT/'static'/'index.html')


@app.post('/api/upload')
async def upload(pdf: UploadFile = File(...)):
    if not pdf.filename or not pdf.filename.lower().endswith('.pdf'):
        raise HTTPException(400,'PDF 파일만 업로드할 수 있습니다.')
    raw = await pdf.read(MAX_UPLOAD+1)
    if len(raw)>MAX_UPLOAD:
        raise HTTPException(413,'파일 크기 제한은 40MB입니다.')
    try:
        doc=fitz.open(stream=raw,filetype='pdf')
        if len(doc)>100:
            raise HTTPException(400,'100페이지 이하 PDF를 사용하세요.')
        pages=[p.get_text('text')[:15000] for p in doc]
        doc.close()
    except HTTPException:
        raise
    except Exception:
        raise HTTPException(400,'PDF 텍스트를 읽지 못했습니다. 텍스트 PDF를 사용하세요.')
    if len(''.join(pages).strip())<30:
        raise HTTPException(400,'추출 가능한 텍스트가 부족합니다. 스캔 PDF는 OCR이 필요합니다.')
    sid=secrets.token_urlsafe(18)
    SESSIONS[sid]=Session(source=pdf.filename[:100],pages=pages)
    return {'session_id':sid,'filename':pdf.filename,'pages':len(pages),'concepts':candidate_concepts(pages),'excerpt':safe_excerpt(' '.join(pages),260)}


@app.post('/api/example')
def example():
    sid=secrets.token_urlsafe(18)
    sample='버블 정렬(Bubble Sort)은 인접한 두 원소를 비교하고 순서가 잘못되었으면 교환하는 정렬 알고리즘입니다. 한 번의 순회가 끝나면 정렬되지 않은 부분에서 가장 큰 원소가 끝으로 이동합니다. 일반적으로 시간 복잡도는 O(n²)입니다.'
    SESSIONS[sid]=Session(source='내장 예제: 버블 정렬',pages=[sample])
    return {'session_id':sid,'filename':'버블 정렬 예제','pages':1,'concepts':['버블 정렬'],'excerpt':sample}


@app.post('/api/start')
def start(r: StartRequest):
    s = get_session(r.session_id)
    concept = r.concept.strip()
    hits = evidence_for(s.pages, concept)
    if not hits:
        raise HTTPException(
            400,
            '선택한 개념이 PDF에 존재하지 않습니다.'
        )
    s.concept = concept
    s.evidence = hits[0][1]
    s.liar = secrets.randbelow(6)
    # 모든 게임에서 사용할 기본 발언 분야
    topic_pool = [
        "핵심 작동 원리",
        "대표적인 활용 사례",
        "구현 방식과 실행 과정",
        "효율성과 성능",
        "유사 개념과의 차이점"
    ]
    # 라이어를 제외한 5명에게 서로 다른 주제 배정
    random.shuffle(topic_pool)
    s.npc_topics = [""] * 6
    topic_index = 0
    for npc_index in range(6):
        if npc_index == s.liar:
            s.npc_topics[npc_index] = "관련 개념의 혼동"
        else:
            s.npc_topics[npc_index] = topic_pool[topic_index]
            topic_index += 1
    # 이전 게임의 라이어 단서 초기화
    s.liar_hint = ""
    print(
        "[NPC 발언 계획]",
        [
            f"{NAMES[i]}: {s.npc_topics[i]}"
            for i in range(6)
        ],
        flush=True
    )
    # 첫 번째 턴: AI 6명만 발언
    s.round = 0
    s.max_turns = 6
    s.messages = []
    s.completed = False
    s.liar_hint = ''
    s.mode = (
        'llm'
        if all(os.getenv(x) for x in [
            'LIAR_LLM_API_KEY',
            'LIAR_LLM_API_URL',
            'LIAR_LLM_MODEL'
        ])
        else 'demo'
    )
    # 모든 PDF 주제에서 동일한 LLM 경로를 사용한다.
    if s.mode != "llm":
        raise HTTPException(
            status_code=503,
            detail="범용 AI 게임에는 로컬 LLM 연결이 필요합니다. 환경변수를 확인하세요."
        )
    # 기존 게임의 학습 프로필 초기화
    s.learning_profile = {}
    s.misconception_review = {}
    if s.mode == "llm":
        try:
            s.learning_profile = build_learning_profile(s)
        except Exception as e:
            print(
                "[학습 프로필 생성 실패]",
                str(e),
                flush=True
            )
            raise HTTPException(
                status_code=502,
                detail=(
                    "학습 프로필을 생성하지 못했습니다. "
                    "LLM 응답 형식을 확인하세요."
                )
            )
        # 게임 시작 전에 오개념도 검토. 실패하면 게임을 시작하지 않음.
        try:
            s.liar_hint = create_misconception(s)
        except Exception as e:
            print("[오개념 생성 실패]", str(e), flush=True)
            raise HTTPException(
                status_code=502,
                detail="검증 가능한 라이어 오개념을 만들지 못했습니다. 제시어나 PDF 내용을 확인하세요."
            )

        # 학습 프로필에서 개념별 담당 주제 가져오기
        topic_pool = s.learning_profile["topics"].copy()
        random.shuffle(topic_pool)
        s.npc_topics = [""] * 6
        topic_index = 0
        for npc_index in range(6):
            if npc_index == s.liar:
                s.npc_topics[npc_index] = "관련 개념의 혼동"
            else:
                s.npc_topics[npc_index] = topic_pool[topic_index]
                topic_index += 1
        print(
            "[PDF 기반 NPC 주제 배정]",
            [
                f"{NAMES[i]}: {s.npc_topics[i]}"
                for i in range(6)
            ],
            flush=True
        )
    print(
        f"[게임 시작] 모드={s.mode}",
        flush=True
    )
    print(
        "[게임 시작] LLM URL:",
        os.getenv("LIAR_LLM_API_URL"),
        flush=True
    )
    print(
        "[게임 시작] LLM 모델:",
        os.getenv("LIAR_LLM_MODEL"),
        flush=True
    )
    return {
        'names': NAMES,
        'mode': s.mode,
        'total_turns': 6,
        'source': s.source
    }


@app.post('/api/next')
def next_turn(r: TurnRequest):
    s = get_session(r.session_id)
    if not s.concept:
        raise HTTPException(
            400,
            '먼저 게임을 시작하세요.'
        )
    if s.completed:
        raise HTTPException(
            400,
            '이미 종료된 게임입니다.'
        )
    if s.round >= s.max_turns:
        return {
            'done': True,
            'progress': s.round,
            'total': s.max_turns
        }
    idx = s.round % 6
    turn = s.round // 6
    try:
        msg = generate_line(s, idx, turn)
    except urllib.error.HTTPError as e:
        error_body = e.read().decode(
            "utf-8",
            errors="replace"
        )
        print(
            f"[LLM HTTP ERROR] {e.code}: {error_body}",
            flush=True
        )
        raise HTTPException(
            status_code=502,
            detail=f"LLM 서버 HTTP 오류: {e.code}"
        )
    except Exception as e:
        import traceback
        traceback.print_exc()
        raise HTTPException(
            status_code=502,
            detail=f"LLM 호출 실패: {type(e).__name__}"
        )
    item = {
        'speaker': idx,
        'round': turn + 1,
        'text': msg
    }
    s.messages.append(item)
    s.round += 1
    return {
        'done': s.round == s.max_turns,
        'message': item,
        'progress': s.round,
        'total': s.max_turns
    }


def build_liar_explanation(s):
    liar_messages = [
        message["text"]
        for message in s.messages
        if message["speaker"] == s.liar
    ]
    reference = pdf_reference_text(s, 6500) or "관련 PDF 문장을 추출하지 못했습니다."
    profile = s.learning_profile or {}
    profile_context = json.dumps(
        {
            "definition": profile.get("definition", ""),
            "principles": profile.get("principles", []),
            "comparisons": profile.get("comparisons", []),
            "uncertainties": profile.get("uncertainties", []),
            "knowledge_status": profile.get(
                "knowledge_status", "unverified"
            )
        },
        ensure_ascii=False
    )[:4500]
    liar_text = "\n".join(liar_messages)
    review_context = json.dumps(
        s.misconception_review, ensure_ascii=False
    )[:1600]

    # 내장 예제와 업로드한 PDF 모두 동일한 해설 경로를 사용한다.
    # 모델이 생성한 학습 프로필은 검증된 정답으로 간주하지 않는다.
    explanation_prompt = f"""
당신은 대학 교육용 AI 라이어 게임의 학습 해설 검토자입니다.

[학습 제시어]
{s.concept}

[라이어가 게임에서 실제로 말한 문장]
{liar_text or "발언 기록이 없습니다."}

[게임의 최우선 정답 기준: PDF 실제 원문]
{reference}

[게임 시작 시 LLM이 생성한 참고 프로필: 미검증 자료]
{profile_context}

[오개념 생성 당시 모델이 검토한 기록: 독립 검증되지 않음]
{review_context}

다음 원칙에 따라 평가하세요.
1. 실제 라이어 발언에서 검증 가능한 구체적 주장 하나를 먼저 파악하세요.
2. 주장에 명백한 오류가 있으면 그 내용을 정확히 인용하거나 요약하고,
   왜 잘못됐는지 올바른 조건 및 원리와 비교하여 설명하세요.
3. PDF에 명시된 설명은 이 게임의 정답입니다.
   라이어의 발언이 PDF 설명과 모순되는 부분부터 설명하세요.
   모델의 외부 지식으로 PDF의 명시적 설명을 틀렸다고 판정하지 마세요.
4. PDF에 설명이 부족하면 전공지식을 이용해 보완할 수 있지만,
   PDF 원문에서 직접 확인된 정보인 것처럼 표현하지 마세요.
5. 참고 프로필은 LLM이 생성했으므로 틀릴 수 있습니다.
   프로필과 실제 발언이 같다는 이유만으로 정답 또는 오답을 확정하지 마세요.
6. 오류를 확인할 수 없거나 발언 자체가 올바르면 오류를 만들어 내지 마세요.
   이 경우 오류를 확인할 수 없다는 점을 분명히 밝히세요.
7. 제시어가 알고리즘, 통신, 수학 등 어떤 분야이든 같은 기준을 사용하세요.
8. 아래 두 제목을 그대로 사용하고, 각 항목을 한국어 2~4문장으로 작성하세요.

[올바른 개념]
제시어의 핵심 정의와 해당 주장에 관련된 올바른 원리를 설명하세요.

[의심스러운 이유]
실제 발언의 문제점과 그 근거를 설명하세요.
"""
    try:
        explanation = llm_chat(
            [
                {
                    "role": "system",
                    "content": (
                        "당신은 대학 교육용 개념 해설 검토자입니다. "
                        "수업 PDF 명시 내용을 게임 정답으로 우선 적용하세요. "
                        "실제 발언을 바탕으로 오류를 검토하고, "
                        "확인할 수 없는 오류는 만들어 내지 마세요. "
                        "지정된 두 제목으로만 답하세요."
                    )
                },
                {"role": "user", "content": explanation_prompt}
            ],
            temperature=0.1
        ).strip()
        if (
            "[올바른 개념]" not in explanation
            or "[의심스러운 이유]" not in explanation
        ):
            raise ValueError("해설 응답의 항목 구분이 없습니다.")
        _, body = explanation.split("[올바른 개념]", 1)
        correct_explanation, reason = body.split("[의심스러운 이유]", 1)
        correct_explanation = correct_explanation.strip()
        reason = reason.strip()
        if not correct_explanation or not reason:
            raise ValueError("해설의 필수 항목이 비어 있습니다.")
    except Exception as e:
        print("[해설 생성 실패]", str(e), flush=True)
        correct_explanation = (
            "이 개념에 대한 자동 해설을 생성하지 못했습니다. "
            "업로드한 학습자료와 신뢰할 수 있는 참고 문헌으로 "
            "핵심 정의를 확인해야 합니다."
        )
        reason = (
            "실제 라이어 발언의 오류 여부를 자동으로 확인하지 못했습니다. "
            "검증되지 않은 설명을 정답으로 단정하지 않습니다."
        )
    return {
        "liar_messages": liar_messages,
        "reason": reason,
        "correct_explanation": correct_explanation
    }


@app.post('/api/vote')
def vote(r: VoteRequest):
    s = get_session(r.session_id)
    if s.completed:
        raise HTTPException(
            400,
            "이미 투표가 종료되었습니다."
        )
    if s.round != 6 or s.round != s.max_turns:
        raise HTTPException(
            400,
            "현재는 투표할 수 없습니다."
        )
    # 결과 생성
    hits = pdf_context_for(s.pages, s.concept)
    explanation = build_liar_explanation(s)
    s.completed = True
    return {
        "correct": r.candidate == s.liar,
        "liar": s.liar,
        "liar_name": NAMES[s.liar],
        "concept": s.concept,
        "evidence": [
            {
                "page": page,
                "text": text
            }
            for page, text in hits
        ],
        "liar_messages": explanation["liar_messages"],
        "reason": explanation["reason"],
        "correct_explanation": explanation["correct_explanation"]
    }
