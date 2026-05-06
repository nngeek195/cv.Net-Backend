import os
import json
import time
import re
from openai import OpenAI
from dotenv import load_dotenv

# ── Load .env ─────────────────────────────────────────────────────────────────
load_dotenv()

NIM_API_KEY  = os.getenv("API_KEY", "")
NIM_MODEL    = "abacusai/dracarys-llama-3.1-70b-instruct"

# ── OpenAI client pointed at NVIDIA NIM ───────────────────────────────────────
client = OpenAI(
    base_url="https://integrate.api.nvidia.com/v1",
    api_key=NIM_API_KEY,
)

# ── System prompt ─────────────────────────────────────────────────────────────
SYSTEM_PROMPT = """You are a senior HR manager and technical recruiter with 15 years of experience evaluating candidates across software engineering, data science, and technology roles.

Your task is to analyze a candidate's CV against a job description and produce a structured evaluation.

STRICT RULES — follow every one, no exceptions:
1. Base your evaluation ONLY on what is explicitly stated in the CV and job description. Do NOT invent, assume, or infer skills, experience, or qualifications that are not clearly written.
2. If information is absent (e.g. no certifications mentioned), score that dimension conservatively — do not assume the candidate has it.
3. Be specific: when listing matched or missing skills, name the exact technology or competency. Never use vague terms like "relevant experience".
4. Scores must reflect reality. A candidate missing 6 of 10 required skills cannot score above 55. A junior candidate cannot score "Senior".
5. Output ONLY a single valid JSON object. No preamble, no explanation, no markdown fences, no trailing text.

OUTPUT SCHEMA (return exactly these keys, exactly these value types):

{
  "matching_analysis": <string — 4 to 6 sentences. Discuss overall fit, strongest alignment points, and most critical gaps. Be direct and professional.>,

  "description": <string — exactly one sentence summarising the match verdict>,

  "score": <integer 0–100. Overall compatibility. Penalise hard for missing must-have skills.>,

  "skill_gap": {
    "matched_skills": [<string>, ...],
    "missing_skills": [<string>, ...],
    "skill_match_percentage": <integer 0–100>
  },

  "readiness": {
    "education_score":      <integer 0–100>,
    "experience_score":     <integer 0–100>,
    "projects_score":       <integer 0–100>,
    "certifications_score": <integer 0–100>,
    "employability_score":  <integer 0–100>
  },

  "match_breakdown": {
    "skill_match_percent":  <integer 0–100>,
    "education_score":      <integer 0–100>,
    "experience_level":     <"Junior" | "Mid" | "Senior">,
    "overall_rank_score":   <integer 0–100>
  },

  "improvements": [
    {
      "action":   <string — a specific, actionable step the candidate should take>,
      "impact":   <string — how this concretely improves their fit for this role>,
      "resource": <string — a real course, certification, or platform name. Leave empty string if unsure.>
    }
  ],

  "recommendation": <string — 2 to 3 sentences. Final verdict: hire, consider with caveats, or decline. State the primary reason.>
}"""


# ── Status ────────────────────────────────────────────────────────────────────
def get_status() -> dict:
    return {
        "model_loaded":      bool(NIM_API_KEY),
        "is_loading":        False,
        "error":             None if NIM_API_KEY else "API_KEY is not set in .env file.",
        "vram_allocated_gb": 0,
        "vram_reserved_gb":  0,
    }


def load_model():
    """No-op — kept so startup code that calls load_model() does not break."""
    if not NIM_API_KEY:
        raise RuntimeError(
            "API_KEY is not set. Add it to your .env file as: API_KEY=your_key_here"
        )
    print(f"✅ NIM model service ready — using {NIM_MODEL}")


# ── JSON extraction helpers ───────────────────────────────────────────────────
def _extract_json_block(raw: str) -> str:
    """Pull the first {...} block out of the streamed response."""
    raw = re.sub(r"^```(?:json)?", "", raw.strip(), flags=re.IGNORECASE)
    raw = re.sub(r"```$", "", raw.strip())
    start = raw.find("{")
    end   = raw.rfind("}")
    if start != -1 and end != -1 and end > start:
        return raw[start:end + 1]
    return raw.strip()


def _build_fallback(raw_text: str, inference_ms: int) -> dict:
    return {
        "matching_analysis": raw_text[:2000],
        "description":       "Could not parse structured output from model.",
        "score":             0,
        "skill_gap":         {"matched_skills": [], "missing_skills": [], "skill_match_percentage": 0},
        "readiness":         {"education_score": 0, "experience_score": 0, "projects_score": 0,
                              "certifications_score": 0, "employability_score": 0},
        "match_breakdown":   {"skill_match_percent": 0, "education_score": 0,
                              "experience_level": "Junior", "overall_rank_score": 0},
        "improvements":      [],
        "recommendation":    "Model output could not be parsed. Please retry.",
        "inference_ms":      inference_ms,
    }


# ── Main inference call ───────────────────────────────────────────────────────
def analyze(cv_text: str, job_description: str) -> dict:
    if not NIM_API_KEY:
        raise RuntimeError("API_KEY is not set in .env file.")

    user_message = (
        f"<CV>\n{cv_text.strip()}\n</CV>\n\n"
        f"<job_description>\n{job_description.strip()}\n</job_description>"
    )

    start = time.time()

    # Collect streamed chunks into a single string
    raw_text = ""
    try:
        completion = client.chat.completions.create(
            model=NIM_MODEL,
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user",   "content": user_message},
            ],
            temperature=0,        # deterministic — minimises hallucination
            top_p=1,
            max_tokens=1024,
            stream=True,
        )

        for chunk in completion:
            delta = chunk.choices[0].delta.content
            if delta is not None:
                raw_text += delta

    except Exception as e:
        raise RuntimeError(f"NIM API call failed: {e}")

    inference_ms = int((time.time() - start) * 1000)

    if not raw_text.strip():
        return _build_fallback("Model returned an empty response.", inference_ms)

    # Parse JSON from the fully assembled streamed output
    clean = _extract_json_block(raw_text)
    try:
        result = json.loads(clean)
    except json.JSONDecodeError:
        try:
            # Fix common model quirks: trailing commas, single quotes
            clean = re.sub(r",\s*([}\]])", r"\1", clean)
            clean = clean.replace("'", '"')
            result = json.loads(clean)
        except json.JSONDecodeError:
            return _build_fallback(raw_text, inference_ms)

    result["inference_ms"] = inference_ms
    return result