import os
import psycopg2
from psycopg2.extras import execute_values
import logging
from datetime import datetime # ✅ ADDED IMPORT

class LinkedInDataHandler:
    def __init__(self):
        self.conn_params = {
            "dbname": os.getenv("DB_NAME"),
            "user": os.getenv("DB_USER"),
            "password": os.getenv("DB_PASSWORD"),
            "host": os.getenv("DB_HOST"),
            "port": os.getenv("DB_PORT")
        }

    def merge_data(self, user_id, data):
        conn = psycopg2.connect(**self.conn_params)
        cur = conn.cursor()
        try:
            print(f"DB DEBUG: Looking up default_profile_id for user: {user_id}")
            cur.execute('SELECT default_profile_id FROM public."user" WHERE id = %s', (user_id,))
            row = cur.fetchone()
            if not row or not row[0]:
                print(f"DB ❌ FAILURE: No default_profile_id found for user {user_id}")
                return False
                
            profile_id = row[0]
            print(f"DB ✅ Found profile_id: {profile_id}. Starting merge...")

            # --- 1. CORE IDENTITY MERGE ---
            u = data.get('user', {})
            print("DB -> Merging Core User Info...")
            cur.execute("""
                UPDATE public."user" SET 
                phone = COALESCE(NULLIF(phone, ''), %s),
                address = COALESCE(NULLIF(address, ''), %s),
                updated_at = CURRENT_TIMESTAMP
                WHERE id = %s
            """, (u.get('phone'), u.get('address'), user_id))

            cur.execute("""
                UPDATE public.target_role_profiles SET 
                portfolio_url = COALESCE(NULLIF(portfolio_url, ''), %s),
                current_org = COALESCE(NULLIF(current_org, ''), %s),
                current_position = COALESCE(NULLIF(current_position, ''), %s),
                personal_statement = COALESCE(NULLIF(personal_statement, ''), %s),
                about_me = COALESCE(NULLIF(about_me, ''), %s),
                updated_at = CURRENT_TIMESTAMP
                WHERE id = %s
            """, (u.get('portfolioUrl'), u.get('currentOrg'), u.get('currentPosition'), 
                  u.get('personalStatement'), u.get('aboutMe'), profile_id))

            # =========================================================================
            # ✅ DATE SANITIZATION HELPER
            # =========================================================================
            def sanitize_date(val, is_required=False):
                default_date = '1900-01-01' if is_required else None
                if not val or str(val).strip() == "":
                    return default_date
                    
                date_str = str(val).strip()
                if date_str.lower() in ["present", "current", "ongoing", "now", "null"]:
                    return None

                for fmt in ("%b %Y", "%B %Y", "%m/%Y", "%Y-%m", "%Y"):
                    try:
                        dt = datetime.strptime(date_str, fmt)
                        return dt.strftime("%Y-%m-01")
                    except ValueError:
                        continue
                        
                try:
                    dt = datetime.fromisoformat(date_str.replace('Z', '+00:00'))
                    return dt.strftime("%Y-%m-%d")
                except ValueError:
                    pass
                    
                return default_date

            # --- 2. UNIQUE RECORD APPEND CONTROLLER ---
            def merge_unique(table, check_col, data_key, cols, mapping):
                items = data.get(data_key, [])
                if not items: return
                
                print(f"DB -> Merging Table: {table} ({len(items)} items found)")
                cur.execute(f'SELECT "{check_col}" FROM public."{table}" WHERE profile_id = %s', (profile_id,))
                existing = {row[0].lower().strip() for row in cur.fetchall() if row[0]}
                
                to_insert = []
                for i in items:
                    val_check = i.get(list(i.keys())[0]) 
                    if val_check and str(val_check).lower().strip() not in existing:
                        to_insert.append(mapping(i))

                if to_insert:
                    print(f"DB    Inserting {len(to_insert)} NEW records into {table}...")
                    col_str = ", ".join(cols) + ", created_at, updated_at"
                    execute_values(cur, f'INSERT INTO public."{table}" ({col_str}) VALUES %s',
                                   [v + ('now', 'now') for v in to_insert])

            # --- 3. EXECUTE ALL 13 RELATIONAL ARRAYS ---
            merge_unique("skill", "skill_name", "skills", ["profile_id", "skill_name", "level"], lambda x: (profile_id, x.get('skillName'), x.get('level') or "Beginner"))
            merge_unique("experience", "company_name", "experience", ["profile_id", "company_name", "start_date", "end_date", "role_description"], lambda x: (profile_id, x.get('companyName'), sanitize_date(x.get('startDate'), True), sanitize_date(x.get('endDate'), False), x.get('roleDescription') or ""))
            merge_unique("education", "degree_title", "education", ["profile_id", "degree_title", "field_of_study", "organization", "start_date", "end_date"], lambda x: (profile_id, x.get('degreeTitle'), x.get('fieldOfStudy') or "", x.get('organization'), sanitize_date(x.get('startDate'), True), sanitize_date(x.get('endDate'), True)))
            merge_unique("certification", "field", "certifications", ["profile_id", "organization", "field", "issue_date"], lambda x: (profile_id, x.get('organization') or "", x.get('field'), sanitize_date(x.get('issueDate'), True)))
            merge_unique("award", "award_name", "awards", ["profile_id", "award_name", "organization", "description"], lambda x: (profile_id, x.get('awardName'), x.get('organization') or "", x.get('description') or ""))
            merge_unique("project", "name", "projects", ["profile_id", "name", "description", "time_period", "role", "organization", "source_link"], lambda x: (profile_id, x.get('name'), x.get('description') or "", x.get('timePeriod') or "", x.get('role') or "", x.get('organization') or "", x.get('sourceLink') or ""))
            merge_unique("publication", "title", "publications", ["profile_id", "title", "description", "source_link", "organization", "year"], lambda x: (profile_id, x.get('title'), x.get('description') or "", x.get('sourceLink') or "", x.get('organization') or "", x.get('year') or 0))
            merge_unique("language", "language_name", "languages", ["profile_id", "language_name", "proficiency"], lambda x: (profile_id, x.get('languageName'), x.get('proficiency') or "Beginner"))
            merge_unique("teaching_experience", "courses_taught", "teachingExperience", ["profile_id", "courses_taught", "organization", "time_period", "curriculum_description"], lambda x: (profile_id, x.get('coursesTaught'), x.get('organization') or "", x.get('timePeriod') or "", x.get('curriculumDescription') or ""))
            merge_unique("research_experience", "project_name", "researchExperience", ["profile_id", "project_name", "lab_or_field_work", "organization", "results_description"], lambda x: (profile_id, x.get('projectName'), x.get('labOrFieldWork') or "", x.get('organization') or "", x.get('resultsDescription') or ""))
            merge_unique("volunteer", "organization", "volunteer", ["profile_id", "organization", "role", "description"], lambda x: (profile_id, x.get('organization'), x.get('role') or "", x.get('description') or ""))
            merge_unique("membership", "organization_name", "memberships", ["profile_id", "organization_name"], lambda x: (profile_id, x.get('organizationName')))
            merge_unique("social_link", "platform_name", "socialLinks", ["profile_id", "platform_name", "profile_url"], lambda x: (profile_id, x.get('platformName'), x.get('profileUrl') or ""))

            conn.commit()
            print("DB ✅ All tables merged and committed successfully.")
            return True
            
        except Exception as e:
            print(f"\n🔥 DB CRITICAL ERROR: {str(e)}")
            if conn: conn.rollback()
            raise e
        finally:
            if conn: conn.close()