import uuid
import json
import os
from DataExtract import extract_structured_cv # Your custom PDF extractor
from service import map_cv_to_schema
from DataHandler import DataHandler

def main():
    print("--- CV.net PRO ENGINE v2.0 ---")
    handler = DataHandler()

    # 1. Extract PDF
    path = input("Enter PDF Path: ")
    if not os.path.exists(path): return
    
    print("⏳ Extracting text...")
    raw_data = extract_structured_cv(path)
    
    # Convert dict of sections to a single string for AI
    cv_text = ""
    for sec, lines in raw_data.get("_raw_sections", {}).items():
        cv_text += f"\n{sec.upper()}\n" + "\n".join(lines)

    # 2. AI Processing
    print("⏳ AI mapping to Schema...")
    final_json = map_cv_to_schema(cv_text)

    # 3. DB Insertion
    user_id = str(uuid.uuid4()) # Placeholder for Firebase UID
    print(f"⏳ Saving to Cloud SQL for User {user_id}...")
    
    if handler.save_cv_data(user_id, final_json):
        print("🎉 SUCCESS: Data is live in PostgreSQL.")
    
    # 4. Cleanup
    handler.cleanup_temp_files(["cv_text.txt", "temp_raw.json"])

if __name__ == "__main__":
    main()