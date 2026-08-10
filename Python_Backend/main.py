import os
import uuid
import requests
import uvicorn
from fastapi import FastAPI, Form, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# 1. Imports from your sub-folders (using folder.file syntax)
# Local feature imports.
from Cv_handle.DataExtract import extract_structured_cv
from Cv_handle.service import map_cv_to_schema as map_pdf_to_schema
from Cv_handle.DataHandler import DataHandler as PDFDataHandler
from fill_with_Linkedinn.linkedin_service import get_linkedin_data, map_linkedin_to_schema
from fill_with_Linkedinn.linkedin_data_handler import LinkedInDataHandler

app = FastAPI()

# Allow the frontend to call the API from local and deployed environments.
app.add_middleware(
    CORSMiddleware,
    allow_origin_regex=".*",
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

handler = PDFDataHandler()

PDF_DIR = "pdfs"
if not os.path.exists(PDF_DIR):
    os.makedirs(PDF_DIR)
    print(f"[INIT] Created PDF directory at: {PDF_DIR}")


# Request body for the CV processing endpoint.
class PDFProcessRequest(BaseModel):
    userId: str
    cvUrl: str

@app.post("/process-pdf")
async def process_cv(payload: PDFProcessRequest):
    print("\n" + "="*50)
    print(f"🚀 [START] /process-pdf Triggered")
    print(f"👤 [USER ID]: {payload.userId}")
    print(f"🔗 [CV URL]: {payload.cvUrl}")
    print("="*50)

    # Store the downloaded CV locally before extraction.
    temp_filename = f"{uuid.uuid4()}.pdf"
    file_path = os.path.join(PDF_DIR, temp_filename)
    
    try:
        print(f"⏳ [STEP 1] Downloading PDF from Cloudinary...")
        response = requests.get(payload.cvUrl, stream=True)
        response.raise_for_status()
        with open(file_path, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192):
                f.write(chunk)
        print(f"✅ [STEP 1] PDF saved locally as: {temp_filename}")

        print(f"⏳ [STEP 2] Extracting raw text from PDF using DataExtract.py...")
        raw_data = extract_structured_cv(file_path)
        
        cv_text = ""
        sections_found = 0
        for sec, lines in raw_data.get("_raw_sections", {}).items():
            cv_text += f"\n{sec.upper()}\n" + "\n".join(lines)
            sections_found += 1
            
        print(f"✅ [STEP 2] Extraction complete. Found {sections_found} distinct sections.")
        print(f"📄 [DEBUG] Raw Text Length: {len(cv_text)} characters.")

        print(f"⏳ [STEP 3] Sending raw text to AI Brain (service.py) for schema mapping...")
        structured_json = map_pdf_to_schema(cv_text)
        
        if not structured_json:
            print("❌ [ERROR] AI Brain returned None or failed to parse JSON.")
            raise HTTPException(status_code=500, detail="AI Brain failed to process CV")
            
        print(f"✅ [STEP 3] AI Mapping successful. Keys generated: {list(structured_json.keys())}")

        # Keep the source PDF URL with the extracted record.
        structured_json['cvUrl'] = payload.cvUrl
        print(f"🔗 [STEP 4] Injected Cloudinary URL into structured data.")

        print(f"⏳ [STEP 5] Sending structured data to PostgreSQL via DataHandler.py...")
        db_success = handler.save_to_postgres(payload.userId, structured_json)

        if db_success:
            print(f"✅ [STEP 5] Database synchronization completed successfully!")
        else:
            print(f"⚠️ [WARNING] Database save routine finished, but returned false/None.")

        print("🎉 [SUCCESS] PDF Processing Pipeline Completed!")
        print("="*50 + "\n")
        
        return {
            "status": "success" if db_success else "database_error",
            "message": "CV processed and synchronized to database successfully",
            "extracted_data": structured_json
        }

    except requests.exceptions.RequestException as req_err:
        print(f"❌ [HTTP ERROR] Failed to download PDF from Cloudinary: {req_err}")
        raise HTTPException(status_code=400, detail=f"Failed to download PDF: {str(req_err)}")
    except Exception as e:
        print(f"❌ [CRITICAL ERROR] Pipeline failed: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        # Remove temporary files after processing.
        print(f"🧹 [CLEANUP] Removing temporary files...")
        if os.path.exists(file_path):
            os.remove(file_path)
            print(f"🗑️ Deleted local PDF: {temp_filename}")
        
        json_temp_name = f"{os.path.splitext(temp_filename)[0]}_extracted.json"
        if os.path.exists(json_temp_name):
            os.remove(json_temp_name)
            print(f"🗑️ Deleted temporary JSON file.")

@app.post("/sync-linkedin")
async def sync_linkedin(user_id: str = Form(...), profile_url: str = Form(...)):
    print(f"\n--- STARTING LINKEDIN SYNC ---")
    print(f"User ID: {user_id}")
    
    clean_url = profile_url.split('?')[0].rstrip('/')
    print(f"Target URL: {clean_url}")
    
    try:
        print("1. Fetching LinkedIn Data from Piloterr...")
        try:
            raw_data = get_linkedin_data(clean_url)
        except requests.exceptions.HTTPError as http_err:
            print(f"❌ PILOTERR API CRASHED: {http_err}")
            raise HTTPException(status_code=502, detail="Piloterr is currently blocked by LinkedIn or unavailable. Please try again later.")
        except Exception as api_err:
            print(f"❌ PILOTERR REQUEST FAILED: {api_err}")
            raise HTTPException(status_code=502, detail="Failed to connect to LinkedIn extraction service.")
            
        if not raw_data:
            print("❌ FAILURE: Piloterr returned empty or invalid data.")
            raise HTTPException(status_code=400, detail="Could not extract data. Ensure the LinkedIn profile is public.")
            
        print(f"   -> Successfully fetched data. Payload size: {len(str(raw_data))} bytes")

        print("2. Sending data to AI for Schema Mapping...")
        structured_data = map_linkedin_to_schema(raw_data)
        if not structured_data:
            print("❌ FAILURE: AI Mapping returned None. Check AI logs.")
            raise HTTPException(status_code=500, detail="AI data mapping failed.")
        print(f"   -> AI Mapping successful. Found {len(structured_data.get('experience', []))} experience records.")

        print("3. Executing Smart Merge to PostgreSQL...")
        li_handler = LinkedInDataHandler()
        success = li_handler.merge_data(user_id, structured_data)
        
        if not success:
            print("❌ FAILURE: Database merge aborted (likely missing active profile).")
            raise HTTPException(status_code=500, detail="Database merge failed. User profile might not exist.")

        print("✅ LINKEDIN SYNC COMPLETELY SUCCESSFUL!")
        return {"status": "success", "message": "LinkedIn Merged", "data": structured_data}
        
    except HTTPException as he:
        raise he
    except Exception as e:
        print(f"🔥 CRITICAL EXCEPTION CAUGHT IN MAIN: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    print("📡 CV.net Unified Engine running on http://localhost:8000")
    uvicorn.run(app, host="0.0.0.0", port=8000)