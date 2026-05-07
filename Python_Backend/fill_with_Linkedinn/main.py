import sys
import uvicorn
import asyncio
from fastapi import FastAPI, Form, HTTPException
from linkedin_service import get_linkedin_data, map_linkedin_to_schema
from linkedin_data_handler import LinkedInDataHandler

app = FastAPI()

# --- REUSABLE SYNC LOGIC ---
async def perform_linkedin_sync(user_id: str, profile_url: str):
    """
    Shared logic used by both the API endpoint and the CLI.
    """
    handler = LinkedInDataHandler()
    # 1. Scrape data from Piloterr
    raw_data = get_linkedin_data(profile_url)
    
    # 2. Map to Master Schema v2.0 using the AI Brain
    structured_data = map_linkedin_to_schema(raw_data)
    
    # 3. Perform Smart Merge (Coalesce and Unique-Append)
    success = handler.merge_data(user_id, structured_data)
    
    if not success:
        return False, "Database merge failed", None
        
    return True, "LinkedIn data merged successfully", structured_data

# --- FASTAPI ENDPOINT ---
@app.post("/sync-linkedin")
async def sync_linkedin(user_id: str = Form(...), profile_url: str = Form(...)):
    success, message, data = await perform_linkedin_sync(user_id, profile_url)
    
    if not success:
        raise HTTPException(status_code=500, detail=message)

    return {
        "status": "success",
        "message": message,
        "data_preview": {
            "skills_found": len(data.get("skills", [])),
            "experience_found": len(data.get("experience", []))
        }
    }

# --- CLI EXECUTION LOGIC ---
async def run_as_cli(user_id, profile_url):
    print(f"🚀 Starting CLI Sync for User: {user_id}")
    success, message, _ = await perform_linkedin_sync(user_id, profile_url)
    if success:
        print(f"✅ {message}")
    else:
        print(f"❌ Error: {message}")

# --- MAIN BLOCK ---
if __name__ == "__main__":
    # If arguments are provided (user_id and url), run as a CLI tool
    if len(sys.argv) >= 3:
        asyncio.run(run_as_cli(sys.argv[1], sys.argv[2]))
    else:
        # DEFAULT: Start the FastAPI server for Postman testing
        print("📡 Starting FastAPI Server on http://localhost:8000")
        uvicorn.run(app, host="0.0.0.0", port=8000)