import os
import requests
import json
from dotenv import load_dotenv

load_dotenv()

API_KEY = os.getenv("PILOTERR_API_KEY")

if not API_KEY:
    raise Exception(
        "Missing PILOTERR_API_KEY in .env"
    )

linkedin_profile = (
    "https://www.linkedin.com/in/niranga-nayanajith/"
)

url = (
    "https://api.piloterr.com/"
    "v2/linkedin/profile/info"
)

headers = {
    "x-api-key": API_KEY
}

params = {
    "query": linkedin_profile
}

try:

    response = requests.get(
        url,
        headers=headers,
        params=params,
        timeout=60
    )

    print("STATUS:", response.status_code)

    response.raise_for_status()

    data = response.json()

    print(json.dumps(
        data,
        indent=2,
        ensure_ascii=False
    ))

except requests.exceptions.HTTPError as e:

    print("HTTP ERROR")
    print(response.text)

except Exception as e:

    print("ERROR:", str(e))