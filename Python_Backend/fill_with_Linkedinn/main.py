import os
import shutil
import sys
import uvicorn
import asyncio
from fastapi import FastAPI, UploadFile, File, Form, HTTPException

from Cv_handle.DataExtract import extract_structured_cv
from Cv_handle.service import map_cv_to_schema as map_pdf_to_schema
from Cv_handle.DataHandler import DataHandler as PDFDataHandler

from fill_with_Linkedinn.linkedin_service import get_linkedin_data, map_linkedin_to_schema
from fill_with_Linkedinn.linkedin_data_handler import LinkedInDataHandler

app = FastAPI()

PDF_DIR = "pdfs"
if not os.path.exists(PDF_DIR): os.makedirs(PDF_DIR)

@app.post("/extract-cv")
async def process_cv(user_id: str = Form(...), file: UploadFile = File(...)):
    file_path = os.path.join(PDF_DIR, file.filename)
    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    try:
        raw_data = extract_structured_cv(file_path)
        cv_text = ""
        for sec, lines in raw_data.get("_raw_sections", {}).items():
            cv_text += f"\n{sec.upper()}\n" + "\n".join(lines)

        structured_json = map_pdf_to_schema(cv_text)
        pdf_handler = PDFDataHandler()
        db_success = pdf_handler.save_to_postgres(user_id, structured_json)

        os.remove(file_path)
        return {"status": "success", "message": "CV Processed", "data": structured_json}
    except Exception as e:
        if os.path.exists(file_path): os.remove(file_path)
        raise HTTPException(status_code=500, detail=str(e))

async def perform_linkedin_sync(user_id: str, profile_url: str):
    try:
        handler = LinkedInDataHandler()
        
        print(f"\n--- STARTING SYNC FOR {user_id} ---")
        print(f"1. Fetching LinkedIn Data for: {profile_url}")
        raw_data = get_linkedin_data(profile_url)
        if not raw_data:
            print("❌ FAILURE: Piloterr returned empty data.")
            return False, "Failed to scrape data from LinkedIn. Check your Scraper API key.", None

        print(f"   -> Successfully fetched {len(str(raw_data))} bytes of raw data.")
        print("2. Mapping Data with AI...")
        
        structured_data = map_linkedin_to_schema(raw_data)
        if not structured_data:
            print("❌ FAILURE: AI Mapping returned None.")
            return False, "AI mapping failed. Check your LLM API key.", None

        print(f"   -> AI Mapping successful. Found {len(structured_data.get('skills', []))} skills.")
        print("3. Merging into Database...")
        
        success = handler.merge_data(user_id, structured_data)
        if not success: 
            print("❌ FAILURE: Database merge returned False.")
            return False, "Database merge failed. User profile might not exist.", None
            
        print("✅ SYNC COMPLETE!")
        return True, "LinkedIn data merged successfully", structured_data

    except Exception as e:
        error_msg = f"CRITICAL ERROR in LinkedIn Sync: {str(e)}"
        print(f"🔥 {error_msg}")
        return False, error_msg, None

@app.post("/sync-linkedin")
async def sync_linkedin(user_id: str = Form(...), profile_url: str = Form(...)):
    success, message, data = await perform_linkedin_sync(user_id, profile_url)
    if not success: 
        raise HTTPException(status_code=500, detail=message)
    return {
        "status": "success", 
        "message": message, 
        "data_preview": {"skills_found": len(data.get("skills", [])) if data else 0}
    }


async def run_as_cli(user_id, profile_url):
    print(f"🚀 Starting CLI Sync for User: {user_id}")
    success, message, _ = await perform_linkedin_sync(user_id, profile_url)
    if success: print(f"✅ {message}")
    else: print(f"❌ Error: {message}")

if __name__ == "__main__":
    if len(sys.argv) >= 3:
        asyncio.run(run_as_cli(sys.argv[1], sys.argv[2]))
    else:
        print("📡 Engine running on http://localhost:8000")
        uvicorn.run(app, host="0.0.0.0", port=8000)