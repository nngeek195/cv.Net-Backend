import os
import requests
import json
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

API_KEY = os.getenv("PILOTERR_API_KEY")

if not API_KEY:
    raise Exception(
        "Missing PILOTERR_API_KEY in .env"
    )

# LinkedIn profile URL
linkedin_profile = (
    "https://www.linkedin.com/in/niranga-nayanajith/"
)

# Piloterr endpoint
url = (
    "https://api.piloterr.com/"
    "v2/linkedin/profile/info"
)

# Headers
headers = {
    "x-api-key": API_KEY
}

# Query parameters
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

    # Raise error if request failed
    response.raise_for_status()

    data = response.json()

    # Pretty JSON output
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