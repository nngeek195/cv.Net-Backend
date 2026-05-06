from fastapi import FastAPI, UploadFile, File, Form, HTTPException
import shutil
import os
import json
from DataExtract import extract_structured_cv  #
from service import map_cv_to_schema           #
from DataHandler import DataHandler            #

app = FastAPI()
handler = DataHandler()

# Ensure the /pdfs directory exists
PDF_DIR = "pdfs"
if not os.path.exists(PDF_DIR):
    os.makedirs(PDF_DIR)

@app.post("/extract-cv")
async def process_cv(user_id: str = Form(...), file: UploadFile = File(...)):
    # 1. Save uploaded PDF to /pdfs folder
    file_path = os.path.join(PDF_DIR, file.filename)
    with open(file_path, "wb") as buffer:
        shutil.copyfileobj(file.file, buffer)

    try:
        # 2. Extract text using DataExtractor.py[cite: 2]
        # This function returns a dict and does not create a file when called as a function
        raw_data = extract_structured_cv(file_path)
        
        # 3. Format text for the AI Brain
        cv_text = ""
        for sec, lines in raw_data.get("_raw_sections", {}).items():
            cv_text += f"\n{sec.upper()}\n" + "\n".join(lines)

        # 4. Send to AI Brain (service.py)[cite: 1]
        structured_json = map_cv_to_schema(cv_text)
        if not structured_json:
            raise HTTPException(status_code=500, detail="AI Brain failed to process CV")

        # 5. Save everything to PostgreSQL (DataHandler.py)
        db_success = handler.save_to_postgres(user_id, structured_json)

        # 6. Cleanup: Delete the PDF and any temporary JSON markers
        if os.path.exists(file_path):
            os.remove(file_path)
        
        # If DataExtract.py created a json file (e.g., in its CLI mode), delete it here
        json_temp_name = f"{os.path.splitext(file.filename)[0]}_extracted.json"
        if os.path.exists(json_temp_name):
            os.remove(json_temp_name)

        return {
            "status": "success" if db_success else "database_error",
            "message": "CV processed and cleaned up successfully",
            "extracted_data": structured_json
        }

    except Exception as e:
        # Emergency cleanup if something crashes
        if os.path.exists(file_path):
            os.remove(file_path)
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
