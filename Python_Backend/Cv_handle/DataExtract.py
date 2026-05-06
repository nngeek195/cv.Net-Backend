import pdfplumber
import re
import json
from typing import List, Dict, Tuple, Optional


# --------------------------------------------------
# STEP 1: DETECT IF PAGE IS SINGLE OR TWO-COLUMN
# --------------------------------------------------
def detect_columns(words, page_width, gap_threshold=60) -> Optional[float]:
    """
    Returns the X split point if two columns detected, else None.
    Strategy: find a wide horizontal gap in word X-positions (the gutter).
    """
    if not words:
        return None

    # Collect all x0 positions
    x_positions = sorted(set(round(w['x0']) for w in words))

    if len(x_positions) < 4:
        return None

    # Look for the largest gap between consecutive X positions
    # Only consider gaps in the middle 20%-80% of page width to avoid margins
    left_bound = page_width * 0.20
    right_bound = page_width * 0.80

    best_gap = 0
    best_split = None

    for i in range(len(x_positions) - 1):
        x_curr = x_positions[i]
        x_next = x_positions[i + 1]
        gap = x_next - x_curr

        if gap >= gap_threshold and left_bound <= x_curr <= right_bound:
            if gap > best_gap:
                best_gap = gap
                best_split = (x_curr + x_next) / 2  # midpoint of the gap

    return best_split  # None if no significant gap found


# --------------------------------------------------
# STEP 2: SPLIT WORDS INTO LEFT / RIGHT COLUMNS
# --------------------------------------------------
def split_words_by_column(words, split_x) -> Tuple[List, List]:
    left = [w for w in words if w['x0'] < split_x]
    right = [w for w in words if w['x0'] >= split_x]
    return left, right


# --------------------------------------------------
# STEP 3: GROUP WORDS INTO LINES (within a column)
# --------------------------------------------------
def group_words_into_lines(words, y_threshold=4) -> List[Dict]:
    """
    Returns a list of dicts: {text, top, x0}
    Groups words on the same Y-level into a single line.
    """
    if not words:
        return []

    words_sorted = sorted(words, key=lambda w: (w['top'], w['x0']))

    lines = []
    current_group = [words_sorted[0]]
    current_y = words_sorted[0]['top']

    for w in words_sorted[1:]:
        if abs(w['top'] - current_y) <= y_threshold:
            current_group.append(w)
        else:
            lines.append({
                "text": " ".join(wd['text'] for wd in current_group),
                "top": current_y,
                "x0": current_group[0]['x0']
            })
            current_group = [w]
            current_y = w['top']

    if current_group:
        lines.append({
            "text": " ".join(wd['text'] for wd in current_group),
            "top": current_y,
            "x0": current_group[0]['x0']
        })

    return lines


# --------------------------------------------------
# STEP 4: SECTION DETECTION
# --------------------------------------------------
SECTION_HEADERS = {
    "education":        ["education", "academic background", "qualifications", "academic qualification", "degree", "university", "college"],
    "experience":       ["experience", "work history", "employment", "internship", "professional experience", "work experience", "career history"],
    "skills":           ["skills", "technical skills", "competencies", "technologies", "core competencies", "key skills", "expertise"],
    "projects":         ["projects", "personal projects", "key projects", "notable projects", "portfolio", "work samples"],
    "certifications":   ["certifications", "certificates", "courses", "training", "professional certifications", "credentials"],
    "summary":          ["summary", "profile", "objective", "about me", "professional summary", "executive summary", "personal profile"],
    "contact":          ["contact", "contacts", "get in touch", "contact information", "contact details"],
    "achievements":     ["achievements", "awards", "honours", "honors", "accomplishments", "recognition"],
    "languages":        ["languages", "language proficiency", "language skills"],
    "references":       ["references", "referee", "professional references"],
    "publications":     ["publications", "published works", "research papers", "articles"],
    "volunteer":        ["volunteer", "volunteering", "volunteer experience", "community service"],
    "memberships":      ["memberships", "professional memberships", "associations"],
    "interests":        ["interests", "hobbies", "personal interests"],
    "tools":            ["tools", "software", "applications"],
    "frameworks":       ["frameworks", "libraries", "platforms"],
    "methodologies":    ["methodologies", "agile", "scrum", "processes"],
    "licenses":         ["licenses", "license", "professional licenses"],
}


def detect_section_header(line_text: str) -> Optional[str]:
    """Returns the section key if this line looks like a section header."""
    cleaned = line_text.strip().lower().rstrip(":")
    for section, keywords in SECTION_HEADERS.items():
        if cleaned in keywords:
            return section
        # Also match if line IS just the keyword (even with caps/symbols)
        for kw in keywords:
            if re.fullmatch(rf"[^a-z]*{re.escape(kw)}[^a-z]*", cleaned):
                return section
    return None


def group_lines_into_sections(lines: List[Dict]) -> Dict[str, List[str]]:
    """
    Takes lines (list of {text, top, x0}) and groups them into named sections.
    Lines before any detected header go into 'header' (name, contact info, etc.)
    """
    sections = {"header": []}
    current_section = "header"

    for line in lines:
        text = line["text"].strip()
        if not text:
            continue

        detected = detect_section_header(text)
        if detected:
            current_section = detected
            if current_section not in sections:
                sections[current_section] = []
        else:
            if current_section not in sections:
                sections[current_section] = []
            sections[current_section].append(text)

    return sections


# --------------------------------------------------
# STEP 5: EXTRACT STRUCTURED FIELDS FROM SECTIONS
# --------------------------------------------------
KNOWN_SKILLS = [
    "python", "java", "javascript", "typescript", "react", "angular",
    "node", "next.js", "docker", "kubernetes", "mongodb", "sql",
    "linux", "git", "cybersecurity", "nmap", "pytorch", "opencv",
    "flask", "django", "firebase", "c++", "c#", "html", "css",
    "aws", "azure", "gcp", "tensorflow", "pandas", "numpy", "redis",
    "postgresql", "mysql", "graphql", "rest", "api", "tailwind",
    "bootstrap", "php", "ruby", "swift", "kotlin", "r", "matlab", "julia",
    "go", "rust", "scala", "elixir", "haskell", "clojure", "perl", "groovy",
    "dart", "lua", "vb.net", "objective-c", "scala", "groovy", "erlang",
    "m1", "apex", "cobol", "fortran", "pascal", "ada", "lisp", "scheme",
    "prolog", "forth", "assembly", "bash", "powershell", "zsh", "fish",
    "git", "svn", "mercurial", "perforce", "github", "gitlab", "bitbucket",
    "jira", "confluence", "slack", "discord", "teams", "zoom", "asana",
    "trello", "monday.com", "notion", "figma", "sketch", "adobe xd",
    "photoshop", "illustrator", "premiere", "aftereffects", "blender",
    "solidworks", "autocad", "revit", "unity", "unreal", "godot",
    "agile", "scrum", "kanban", "waterfall", "devops", "ci/cd", "jenkins",
    "gitlab-ci", "github-actions", "travis-ci", "circleci", "travisci",
    "npm", "pip", "maven", "gradle", "cargo", "composer", "bundler",
    "webpack", "gulp", "grunt", "parcel", "vite", "rollup", "esbuild",
    "junit", "pytest", "mocha", "jest", "jasmine", "rspec", "testng",
    "selenium", "cypress", "playwright", "puppeteer", "appium", "xcode",
    "android-studio", "visual-studio", "vscode", "intellij", "eclipse",
    "vim", "emacs", "sublime", "atom", "neovim", "spacemacs", "joe",
    "nosql", "cassandra", "dynamodb", "elasticsearch", "solr", "couchdb",
    "firebase-realtime", "supabase", "fauna", "airtable", "smartsheet",
    "microservices", "rest-api", "grpc", "soap", "mqtt", "amqp", "kafka"
]


def extract_name(header_lines: List[str]) -> Optional[str]:
    for line in header_lines[:6]:
        stripped = line.strip()
        # Name: usually all caps, or title case, 2-4 words, no special chars
        if stripped.isupper() and 2 <= len(stripped.split()) <= 5:
            return stripped.title()
        if re.match(r'^[A-Z][a-z]+ [A-Z][a-z]+', stripped) and len(stripped.split()) <= 5:
            return stripped
    return None


def extract_email(all_text: str) -> Optional[str]:
    match = re.search(r'[\w\.\+-]+@[\w\.-]+\.\w+', all_text)
    return match.group(0) if match else None


def extract_phone(all_text: str) -> Optional[str]:
    match = re.search(r'(\+?\d[\d\s\-]{7,14}\d)', all_text)
    return match.group(0).strip() if match else None


def extract_location(all_text: str) -> Optional[str]:
    for line in all_text.splitlines():
        if "sri lanka" in line.lower():
            return line.strip()
    return None


def extract_skills_from_sections(sections: Dict) -> List[str]:
    # Check dedicated skills section first, then scan all text
    skills_text = " ".join(sections.get("skills", [])).lower()
    all_text = " ".join(
        line for lines in sections.values() for line in lines
    ).lower()

    found = set()
    # Prioritize skills section, but fall back to full text
    search_text = skills_text if skills_text else all_text
    for skill in KNOWN_SKILLS:
        pattern = r'\b' + re.escape(skill) + r'\b'
        if re.search(pattern, search_text):
            found.add(skill)
    return sorted(found)


def extract_experience_years(all_text: str) -> int:
    match = re.search(r'(\d+)\+?\s+years?', all_text.lower())
    return int(match.group(1)) if match else 0


def extract_education(sections: Dict) -> Optional[str]:
    edu_lines = " ".join(sections.get("education", [])).lower()
    if "master" in edu_lines or "msc" in edu_lines or "m.sc" in edu_lines:
        return "master"
    if "bachelor" in edu_lines or "bsc" in edu_lines or "b.sc" in edu_lines or "b.e" in edu_lines:
        return "bachelor"
    if "diploma" in edu_lines:
        return "diploma"
    return None


def extract_projects(sections: Dict) -> List[str]:
    return [line for line in sections.get("projects", []) if line.strip()]


def extract_certifications(sections: Dict) -> List[str]:
    return [line for line in sections.get("certifications", []) if line.strip()]


# --------------------------------------------------
# STEP 6: PROCESS ONE PAGE (column-aware)
# --------------------------------------------------
def process_page(page) -> Dict[str, List[str]]:
    """
    Detects columns, processes each column separately,
    then merges the section dicts.
    """
    words = page.extract_words()
    if not words:
        return {}

    # Add page-level metadata
    enriched_words = [
        {**w, "top": w["top"], "x0": w["x0"]}
        for w in words
    ]

    page_width = page.width
    split_x = detect_columns(enriched_words, page_width)

    if split_x:
        # Two-column layout detected
        left_words, right_words = split_words_by_column(enriched_words, split_x)

        left_lines = group_words_into_lines(left_words)
        right_lines = group_words_into_lines(right_words)

        left_sections = group_lines_into_sections(left_lines)
        right_sections = group_lines_into_sections(right_lines)

        # Merge: combine both columns' sections
        merged = {}
        all_keys = set(left_sections.keys()) | set(right_sections.keys())
        for key in all_keys:
            merged[key] = left_sections.get(key, []) + right_sections.get(key, [])
        return merged

    else:
        # Single-column layout
        lines = group_words_into_lines(enriched_words)
        return group_lines_into_sections(lines)


# --------------------------------------------------
# STEP 7: FULL PDF → STRUCTURED DATA
# --------------------------------------------------
def extract_structured_cv(pdf_path: str) -> Dict:
    merged_sections: Dict[str, List[str]] = {}

    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            page_sections = process_page(page)
            for key, lines in page_sections.items():
                if key not in merged_sections:
                    merged_sections[key] = []
                merged_sections[key].extend(lines)

    # Build all_text for regex-based extraction
    all_text = "\n".join(
        line for lines in merged_sections.values() for line in lines
    )
    header_lines = merged_sections.get("header", [])

    return {
        "name":                 extract_name(header_lines),
        "email":                extract_email(all_text),
        "phone":                extract_phone(all_text),
        "location":             extract_location(all_text),
        "education":            extract_education(merged_sections),
        "experience_years":     extract_experience_years(all_text),
        "skills":               extract_skills_from_sections(merged_sections),
        "projects":             extract_projects(merged_sections),
        "certifications":       extract_certifications(merged_sections),
        "summary":              merged_sections.get("summary", []),
        "achievements":         merged_sections.get("achievements", []),
        "languages":            merged_sections.get("languages", []),
        "_raw_sections":        merged_sections   # useful for debugging
    }


# --------------------------------------------------
# STEP 8: CLI ENTRY POINT
# --------------------------------------------------
if __name__ == "__main__":
    import sys
    import os

    if len(sys.argv) < 2:
        print("Usage: python cv_extractor.py <cv.pdf> [output.json] [--raw]")
        exit(1)

    pdf_file = sys.argv[1]
    show_raw = "--raw" in sys.argv

    # Determine output JSON path
    # If second arg is provided and not a flag, use it as output path
    if len(sys.argv) >= 3 and not sys.argv[2].startswith("--"):
        output_file = sys.argv[2]
    else:
        # Default: same name as PDF but .json extension
        base_name = os.path.splitext(os.path.basename(pdf_file))[0]
        output_file = f"{base_name}_extracted.json"

    print(f"📄 Extracting CV data from: {pdf_file}")
    data = extract_structured_cv(pdf_file)

    if not show_raw:
        data.pop("_raw_sections", None)

    # Save to JSON file
    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=4, ensure_ascii=False)

    print(f"\n✅ Extracted data saved to: {output_file}")
    print("\n📋 Preview:\n")
    print(json.dumps(data, indent=4))