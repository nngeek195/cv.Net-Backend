import os

import psycopg2
from psycopg2.extras import execute_values
import logging

# Configure logging to see errors in your Ubuntu terminal
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class DataHandler:
    def __init__(self):
        # Master Database Configuration
        self.conn_params = {
            "dbname": os.getenv("DB_NAME"),
            "user": os.getenv("DB_USER"),
            "password": os.getenv("DB_PASSWORD"),
            "host": os.getenv("DB_HOST"),
            "port": os.getenv("DB_PORT")
        }

    def save_to_postgres(self, user_id, data):
        """
        Orchestrates the update of all 14 Master Schema tables.
        Uses a single transaction (Commit/Rollback) for data integrity.
        """
        conn = None
        try:
            conn = psycopg2.connect(**self.conn_params)
            cur = conn.cursor()

            # --- 1. CORE USER UPDATE ---
            # We UPDATE because the user record was created during signup
            u = data.get('user', {})
            cur.execute("""
                UPDATE public."user" SET 
                full_name = %s, phone = %s, address = %s, portfolio_url = %s,
                employment_status = %s, current_org = %s, current_position = %s,
                personal_statement = %s, about_me = %s, updated_at = CURRENT_TIMESTAMP
                WHERE id = %s
            """, (
                u.get('fullName') or "", 
                u.get('phone') or "", 
                u.get('address') or "", 
                u.get('portfolioUrl') or "", 
                u.get('employmentStatus') or "Unemployed", 
                u.get('GPA') or "",
                u.get('currentOrg') or "", 
                u.get('currentPosition') or "",
                u.get('personalStatement') or "", 
                u.get('aboutMe') or "", 
                user_id
            ))

            # --- 2. THE SYNC HELPER ---
            # This function wipes old data and inserts new data for a specific sub-table
            def sync_table(table_name, columns, data_key, mapping_func):
                items = data.get(data_key, [])
                # 1. Clear existing records for this user (Fresh Sync)
                cur.execute(f'DELETE FROM public."{table_name}" WHERE user_id = %s', (user_id,))
                
                if items:
                    # 2. Prepare values with empty string fallbacks
                    vals = [mapping_func(item) for item in items]
                    # 3. Add standard timestamps
                    col_str = ", ".join(columns) + ", created_at, updated_at"
                    execute_values(cur, f'INSERT INTO public."{table_name}" ({col_str}) VALUES %s', 
                                   [v + ('now', 'now') for v in vals])

            # --- 3. EXECUTE SYNC FOR ALL TABLES ---

            # Social Links
            sync_table("social_link", ["user_id", "platform_name", "profile_url"], "socialLinks", 
                       lambda x: (user_id, x.get('platformName') or "", x.get('profileUrl') or ""))

            # Skills
            sync_table("skill", ["user_id", "skill_name", "level"], "skills", 
                       lambda x: (user_id, x.get('skillName') or "", x.get('level') or "Beginner"))

            # Experience
            sync_table("experience", ["user_id", "company_name", "start_date", "end_date", "role_description"], "experience", 
                       lambda x: (user_id, x.get('companyName') or "", x.get('startDate') or '1900-01-01', x.get('endDate'), x.get('roleDescription') or ""))

            # Education (8 fields)
            sync_table("education", ["user_id", "degree_title", "field_of_study", "organization", "start_date", "end_date", "honors", "thesis_title", "relevant_coursework"], "education", 
                       lambda x: (user_id, x.get('degreeTitle') or "", x.get('fieldOfStudy') or "", x.get('organization') or "", x.get('startDate') or '1900-01-01', x.get('endDate') or '1900-01-01', x.get('honors') or "", x.get('thesisTitle') or "", x.get('relevantCoursework') or ""))

            # Projects
            sync_table("project", ["user_id", "name", "description", "time_period", "role", "organization", "source_link"], "projects", 
                       lambda x: (user_id, x.get('name') or "", x.get('description') or "", x.get('timePeriod') or "", x.get('role') or "", x.get('organization') or "", x.get('sourceLink') or ""))

            # Certifications
            sync_table("certification", ["user_id", "organization", "field", "issue_date"], "certifications", 
                       lambda x: (user_id, x.get('organization') or "", x.get('field') or "", x.get('issueDate') or '1900-01-01'))

            # Memberships
            sync_table("membership", ["user_id", "organization_name"], "memberships", 
                       lambda x: (user_id, x.get('organizationName') or ""))

            # Languages
            sync_table("language", ["user_id", "language_name", "proficiency"], "languages", 
                       lambda x: (user_id, x.get('languageName') or "", x.get('proficiency') or "Beginner"))

            # Publications
            sync_table("publication", ["user_id", "title", "description", "source_link", "organization", "year"], "publications", 
                       lambda x: (user_id, x.get('title') or "", x.get('description') or "", x.get('sourceLink') or "", x.get('organization') or "", x.get('year') or 0))

            # Teaching
            sync_table("teaching_experience", ["user_id", "courses_taught", "organization", "time_period", "curriculum_description"], "teachingExperience", 
                       lambda x: (user_id, x.get('coursesTaught') or "", x.get('organization') or "", x.get('timePeriod') or "", x.get('curriculumDescription') or ""))

            # Research
            sync_table("research_experience", ["user_id", "project_name", "lab_or_field_work", "organization", "results_description"], "researchExperience", 
                       lambda x: (user_id, x.get('projectName') or "", x.get('labOrFieldWork') or "", x.get('organization') or "", x.get('resultsDescription') or ""))

            # Awards
            sync_table("award", ["user_id", "award_name", "organization", "description"], "awards", 
                       lambda x: (user_id, x.get('awardName') or "", x.get('organization') or "", x.get('description') or ""))

            # Volunteer
            sync_table("volunteer", ["user_id", "organization", "role", "description"], "volunteer", 
                       lambda x: (user_id, x.get('organization') or "", x.get('role') or "", x.get('description') or ""))

            conn.commit()
            logger.info(f"✅ Success: CV for User {user_id} fully synced to PostgreSQL.")
            return True

        except Exception as e:
            if conn:
                conn.rollback()
            logger.error(f"❌ Database Sync Error: {e}")
            raise e
        finally:
            if conn:
                conn.close()