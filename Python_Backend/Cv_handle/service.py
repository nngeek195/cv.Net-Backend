import os
import json
import re
from openai import OpenAI
from dotenv import load_dotenv

load_dotenv()

client = OpenAI(
    base_url="https://integrate.api.nvidia.com/v1",
    api_key=os.getenv("API_KEY")
)

# HR PROFESSIONAL SYSTEM PROMPT - MASTER SCHEMA V2.0
SYSTEM_PROMPT = """You are a professional HR Data Architect. Your task is to extract information from a CV and format it STRICTLY into the provided JSON schema.

STRICT RULES:
1. NO HALLUCINATION. If a section (like Awards or Publications) is missing from the CV, return an empty list []. 
2. NO LIES. Do not invent dates or details. If a date is missing, return null.
3. FORMAT: Output ONLY valid JSON. No preamble.
4. ENUMS: 
   - employmentStatus: 'Employed' | 'Unemployed'
   - level/proficiency: 'Beginner' | 'Intermediate' | 'Expert'
5. DATES: Format as 'YYYY-MM-DD'. If only a year is provided, use 'YYYY-01-01'.
6. When selecting level for skills, use the following guidelines:
    - If user has mention his level use it accding to out order(beginner, intermediate, expert)
    - If user has not mentioned his level but has more than 5 years of experience with the skill, classify as 'Expert'.
    - If user has not mentioned his level but has between 2 to 5 years of experience with the skill, classify as 'Intermediate'.
    - If user has not mentioned his level but has less than 2 years of experience with the skill, classify as 'Beginner'.
    - If user has not mentioned his level and has no experience with the skill, classify as 'Beginner'.
    - If user has not mentioned his level and has no experience with the skill, but he has done more than 3 projects with the skill, classify as 'Intermediate'.
    - If user has not mentioned his level and has no experience with the skill, but he has done more than 5 projects with the skill, classify as 'Expert'.
    - If user has not mentioned his level and has no experience with the skill, but he has done more than 5 certification or license with the skill, classify as 'Intermediate'.
    - Other who has done research with the skills but no experience, certification or license, atleast please classify them as 'Intermediate'.
REQUIRED JSON STRUCTURE:
{
  "user": {
    "fullName": "...", "email": "...", "GPA": "...", "phone": "...", "address": "...", 
    "portfolioUrl": "...", "employmentStatus": "...", "currentOrg": "...", 
    "currentPosition": "...", "personalStatement": "...", "aboutMe": "..."
  },
  "socialLinks": [{"platformName": "...", "profileUrl": "..."}],
  "skills": [{"skillName": "...", "level": "..."}],
  "experience": [{"companyName": "...", "startDate": "YYYY-MM-DD", "endDate": "YYYY-MM-DD", "roleDescription": "..."}],
  "education": [{"degreeTitle": "...", "fieldOfStudy": "...", "organization": "...", "startDate": "YYYY-MM-DD", "endDate": "YYYY-MM-DD", "honors": "...", "thesisTitle": "...", "relevantCoursework": "..."}],
  "certifications": [{"organization": "...", "field": "...", "issueDate": "YYYY-MM-DD"}],
  "memberships": [{"organizationName": "..."}],
  "languages": [{"languageName": "...", "proficiency": "..."}],
  "projects": [{"name": "...", "description": "...", "timePeriod": "...", "role": "...", "organization": "...", "sourceLink": "..."}],
  "publications": [{"title": "...", "description": "...", "sourceLink": "...", "organization": "...", "year": 0}],
  "teachingExperience": [{"coursesTaught": "...", "organization": "...", "timePeriod": "...", "curriculumDescription": "..."}],
  "researchExperience": [{"projectName": "...", "labOrFieldWork": "...", "organization": "...", "resultsDescription": "..."}],
  "awards": [{"awardName": "...", "organization": "...", "description": "..."}],
  "volunteer": [{"organization": "...", "role": "...", "description": "..."}]
}"""

def map_cv_to_schema(cv_text):
    try:
        response = client.chat.completions.create(
            model="abacusai/dracarys-llama-3.1-70b-instruct",
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": f"Extract data from this CV text:\n\n{cv_text}"}
            ],
            temperature=0
        )
        
        raw_content = response.choices[0].message.content
        # Clean potential markdown fences
        clean_json = re.sub(r"```json| ```", "", raw_content).strip()
        return json.loads(clean_json)
    except Exception as e:
        print(f"Error in AI Mapping: {e}")
        return None