import os
import json
import re
import requests
from openai import OpenAI
from dotenv import load_dotenv

load_dotenv()

client = OpenAI(
    base_url="https://integrate.api.nvidia.com/v1",
    api_key=os.getenv("API_KEY"),
    timeout=60.0 
)

PILOTERR_API_KEY = os.getenv("PILOTERR_API_KEY")

SYSTEM_PROMPT = """You are a professional HR Data Architect and a strict data extraction API. Your task is to map raw LinkedIn JSON data to the provided 14-table Master Schema v2.0.

STRICT RULES:
1. NO OVERWRITING: For any field not found in LinkedIn data, return "".
2. DATES: Format as 'YYYY-MM-DD'. If only a year is available, use 'YYYY-01-01'.
3. SKILL LEVELING LOGIC:
   - Use mentioned level (Beginner, Intermediate, Expert) if present.
   - 5+ years experience = Expert.
   - 2-5 years experience = Intermediate.
   - <2 years or no experience = Beginner.
   - No exp + 3+ projects = Intermediate.
   - No exp + 5+ projects = Expert.
   - No exp + 5+ certs = Intermediate.
   - Research background = Intermediate.
4. ENUMS: Only use 'Employed'/'Unemployed' and 'Beginner'/'Intermediate'/'Expert'.
5. STRICT JSON FORMAT: You must output ONLY valid, raw JSON. Do not include any conversational text like "Here is the data". Do not wrap the output in markdown backticks (```json). Your entire response must start with { and end with }.

JSON SCHEMA:
{
  "user": { "fullName": "", "email": "", "phone": "", "address": "", "portfolioUrl": "", "employmentStatus": "", "currentOrg": "", "currentPosition": "", "personalStatement": "", "aboutMe": "" },
  "socialLinks": [{"platformName": "", "profileUrl": ""}],
  "skills": [{"skillName": "", "level": ""}],
  "experience": [{"companyName": "", "startDate": "", "endDate": "", "roleDescription": ""}],
  "education": [{"degreeTitle": "", "fieldOfStudy": "", "organization": "", "startDate": "", "endDate": "", "honors": "", "thesisTitle": "", "relevantCoursework": ""}],
  "certifications": [{"organization": "", "field": "", "issueDate": ""}],
  "memberships": [{"organizationName": ""}],
  "languages": [{"languageName": "", "proficiency": ""}],
  "projects": [{"name": "", "description": "", "timePeriod": "", "role": "", "organization": "", "sourceLink": ""}],
  "publications": [{"title": "", "description": "", "sourceLink": "", "organization": "", "year": 0}],
  "teachingExperience": [{"coursesTaught": "", "organization": "", "timePeriod": "", "curriculumDescription": ""}],
  "researchExperience": [{"projectName": "", "labOrFieldWork": "", "organization": "", "resultsDescription": ""}],
  "awards": [{"awardName": "", "organization": "", "description": ""}],
  "volunteer": [{"organization": "", "role": "", "description": ""}]
}"""
def get_linkedin_data(profile_url):
    url = "https://api.piloterr.com/v2/linkedin/profile/info"
    headers = {"x-api-key": PILOTERR_API_KEY}
    params = {"query": profile_url}
    response = requests.get(url, headers=headers, params=params, timeout=60)
    response.raise_for_status()
    return response.json()

def map_linkedin_to_schema(raw_data):
    try:
        response = client.chat.completions.create(
            # 2. Swap to the lightning-fast 8B model
            model="meta/llama-3.1-8b-instruct", 
            messages=[
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": f"Map this LinkedIn data:\n\n{json.dumps(raw_data)}"}
            ],
            temperature=0,
            max_tokens=2048 # Ensure the model has enough room to write the full JSON
        )
        
        raw_content = response.choices[0].message.content
        
        print("\n--- DEBUG: RAW AI OUTPUT ---")
        print(raw_content)
        print("----------------------------\n")

        match = re.search(r'(\{.*\})', raw_content, re.DOTALL)
        
        if match:
            clean_json = match.group(1)
            parsed_data = json.loads(clean_json)
            print("✅ JSON successfully parsed from AI output.")
            return parsed_data
        else:
            print("❌ Error: No JSON object brackets '{ }' found in AI response.")
            return None

    except Exception as e:
        print(f"❌ Error in AI Mapping request: {e}")
        return None