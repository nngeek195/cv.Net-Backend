import os
import uuid
import psycopg2
from psycopg2.extras import execute_values
import logging
from datetime import datetime

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class DataHandler:
    def __init__(self):
        self.conn_params = {
            "dbname": os.getenv("DB_NAME"),
            "user": os.getenv("DB_USER"),
            "password": os.getenv("DB_PASSWORD"),
            "host": os.getenv("DB_HOST"),
            "port": os.getenv("DB_PORT")
        }

    def save_to_postgres(self, user_id, data):
        conn = None
        try:
            conn = psycopg2.connect(**self.conn_params)
            cur = conn.cursor()

            # Create a target profile if the user does not have one yet.
            cur.execute('SELECT default_profile_id FROM public."user" WHERE id = %s', (user_id,))
            row = cur.fetchone()
            
            if not row:
                raise Exception("User identity not found in database. Cannot process CV.")

            profile_id = row[0]

            if not profile_id:
                profile_id = str(uuid.uuid4())
                logger.info(f"Generating new TargetRoleProfile ({profile_id}) for User {user_id}")
                
                cur.execute("""
                    INSERT INTO public.target_role_profiles 
                    (id, user_id, job_role, created_at, updated_at) 
                    VALUES (%s, %s, 'General Application Profile', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                """, (profile_id, user_id))
                
                cur.execute('UPDATE public."user" SET default_profile_id = %s WHERE id = %s', (profile_id, user_id))

            # Update the core user record.
            u = data.get('user', {})
            gpa_val = None
            try:
                if u.get('GPA'): gpa_val = float(u.get('GPA'))
            except: pass

            cur.execute("""
                UPDATE public."user" SET 
                phone = %s, address = %s, employment_status = %s, gpa = %s, updated_at = CURRENT_TIMESTAMP
                WHERE id = %s
            """, (u.get('phone') or "", u.get('address') or "", u.get('employmentStatus') or "Unemployed", gpa_val, user_id))

            # Update the profile-level CV fields.
            p = data.get('profile', {}) or u
            cv_link = data.get('cvUrl') or p.get('cvUrl') or ""
            
            cur.execute("""
                UPDATE public.target_role_profiles SET 
                portfolio_url = %s, current_org = %s, current_position = %s,
                personal_statement = %s, about_me = %s, cv_url = %s, updated_at = CURRENT_TIMESTAMP
                WHERE id = %s
            """, (p.get('portfolioUrl') or "", p.get('currentOrg') or "", p.get('currentPosition') or "",
                  p.get('personalStatement') or "", p.get('aboutMe') or "", cv_link, profile_id))

            # Normalize dates and integers before writing relational tables.
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

            def safe_int(val):
                if not val or str(val).strip() == "":
                    return 0
                try:
                    return int(val)
                except ValueError:
                    return 0

            # Replace each relational table with the latest extracted data.
            def sync_table(table_name, columns, data_key, mapping_func):
                items = data.get(data_key, [])
                cur.execute(f'DELETE FROM public."{table_name}" WHERE profile_id = %s', (profile_id,))
                
                if items:
                    vals = [mapping_func(item) for item in items]
                    col_str = ", ".join(columns) + ", created_at, updated_at"
                    execute_values(cur, f'INSERT INTO public."{table_name}" ({col_str}) VALUES %s', 
                                   [v + ('now', 'now') for v in vals])

            sync_table("social_link", ["profile_id", "platform_name", "profile_url"], "socialLinks", lambda x: (profile_id, x.get('platformName') or "", x.get('profileUrl') or ""))
            sync_table("skill", ["profile_id", "skill_name", "level"], "skills", lambda x: (profile_id, x.get('skillName') or "", x.get('level') or "Beginner"))
            
            sync_table("experience", ["profile_id", "company_name", "start_date", "end_date", "role_description"], "experience", lambda x: (profile_id, x.get('companyName') or "", sanitize_date(x.get('startDate'), True), sanitize_date(x.get('endDate'), False), x.get('roleDescription') or ""))
            
            sync_table("education", ["profile_id", "degree_title", "field_of_study", "organization", "start_date", "end_date", "honors", "thesis_title", "relevant_coursework"], "education", lambda x: (profile_id, x.get('degreeTitle') or "", x.get('fieldOfStudy') or "", x.get('organization') or "", sanitize_date(x.get('startDate'), True), sanitize_date(x.get('endDate'), True), x.get('honors') or "", x.get('thesisTitle') or "", x.get('relevantCoursework') or ""))
            
            sync_table("project", ["profile_id", "name", "description", "time_period", "role", "organization", "source_link"], "projects", lambda x: (profile_id, x.get('name') or "", x.get('description') or "", x.get('timePeriod') or "", x.get('role') or "", x.get('organization') or "", x.get('sourceLink') or ""))
            
            sync_table("certification", ["profile_id", "organization", "field", "issue_date"], "certifications", lambda x: (profile_id, x.get('organization') or "", x.get('field') or "", sanitize_date(x.get('issueDate'), True)))
            
            sync_table("membership", ["profile_id", "organization_name"], "memberships", lambda x: (profile_id, x.get('organizationName') or ""))
            sync_table("language", ["profile_id", "language_name", "proficiency"], "languages", lambda x: (profile_id, x.get('languageName') or "", x.get('proficiency') or "Beginner"))
            
            sync_table("publication", ["profile_id", "title", "description", "source_link", "organization", "year"], "publications", lambda x: (profile_id, x.get('title') or "", x.get('description') or "", x.get('sourceLink') or "", x.get('organization') or "", safe_int(x.get('year'))))
            
            sync_table("teaching_experience", ["profile_id", "courses_taught", "organization", "time_period", "curriculum_description"], "teachingExperience", lambda x: (profile_id, x.get('coursesTaught') or "", x.get('organization') or "", x.get('timePeriod') or "", x.get('curriculumDescription') or ""))
            sync_table("research_experience", ["profile_id", "project_name", "lab_or_field_work", "organization", "results_description"], "researchExperience", lambda x: (profile_id, x.get('projectName') or "", x.get('labOrFieldWork') or "", x.get('organization') or "", x.get('resultsDescription') or ""))
            sync_table("award", ["profile_id", "award_name", "organization", "description"], "awards", lambda x: (profile_id, x.get('awardName') or "", x.get('organization') or "", x.get('description') or ""))
            sync_table("volunteer", ["profile_id", "organization", "role", "description"], "volunteer", lambda x: (profile_id, x.get('organization') or "", x.get('role') or "", x.get('description') or ""))

            conn.commit()
            logger.info(f"✅ Success: CV fully synced to Profile {profile_id}")
            return True
        except Exception as e:
            if conn: conn.rollback()
            logger.error(f"❌ Database Sync Error: {e}")
            raise e
        finally:
            if conn: conn.close()