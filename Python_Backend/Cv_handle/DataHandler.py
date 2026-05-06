import psycopg2
from psycopg2.extras import execute_values
import os

class DataHandler:
    def __init__(self):
        self.conn_params = {
            "dbname": "postgres",
            "user": "postgres",
            "password": "YOUR_PASSWORD", # Replace with actual
            "host": "YOUR_IP",           # Replace with actual
            "port": "5432"
        }

    def save_cv_data(self, user_uuid, data):
        conn = None
        try:
            conn = psycopg2.connect(**self.conn_params)
            cur = conn.cursor()

            # 1. USERS TABLE
            u = data.get('user', {})
            cur.execute("""
                INSERT INTO Users (id, full_name, email, phone, address, portfolio_url, employment_status, current_org, current_position, personal_statement, about_me)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (email) DO UPDATE SET full_name = EXCLUDED.full_name;
            """, (user_uuid, u.get('full_name'), u.get('email'), u.get('phone'), u.get('address'), u.get('portfolio_url'), u.get('employment_status'), u.get('current_org'), u.get('current_position'), u.get('personal_statement'), u.get('about_me')))

            # HELPER FOR BULK INSERTS
            def bulk_insert(table, columns, data_list, mapping_func):
                if data_list:
                    vals = [mapping_func(item) for item in data_list]
                    col_str = ", ".join(columns)
                    placeholders = ", ".join(["%s"] * len(columns))
                    query = f"INSERT INTO {table} ({col_str}) VALUES ({placeholders})"
                    execute_values(cur, f"INSERT INTO {table} ({col_str}) VALUES %s", vals)

            # 2. SOCIAL LINKS
            bulk_insert("SocialLinks", ["user_id", "platform_name", "profile_url"], data.get('social_links'), 
                        lambda x: (user_uuid, x.get('platform_name'), x.get('profile_url')))

            # 3. SKILLS
            bulk_insert("Skills", ["user_id", "skill_name", "level"], data.get('skills'), 
                        lambda x: (user_uuid, x.get('skill_name'), x.get('level')))

            # 4. EXPERIENCE
            bulk_insert("Experience", ["user_id", "company_name", "start_date", "end_date", "role_description"], data.get('experience'), 
                        lambda x: (user_uuid, x.get('company_name'), x.get('start_date'), x.get('end_date'), x.get('role_description')))

            # 5. EDUCATION
            bulk_insert("Education", ["user_id", "degree_title", "field_of_study", "organization", "start_date", "end_date", "honors", "thesis_title", "relevant_coursework"], data.get('education'), 
                        lambda x: (user_uuid, x.get('degree_title'), x.get('field_of_study'), x.get('organization'), x.get('start_date'), x.get('end_date'), x.get('honors'), x.get('thesis_title'), x.get('relevant_coursework')))

            # 6. PROJECTS
            bulk_insert("Projects", ["user_id", "name", "description", "time_period", "role", "organization", "source_link"], data.get('projects'), 
                        lambda x: (user_uuid, x.get('name'), x.get('description'), x.get('time_period'), x.get('role'), x.get('organization'), x.get('source_link')))

            # 7. PUBLICATIONS
            bulk_insert("Publications", ["user_id", "title", "description", "source_link", "organization", "year"], data.get('publications'), 
                        lambda x: (user_uuid, x.get('title'), x.get('description'), x.get('source_link'), x.get('organization'), x.get('year')))

            # 8. TEACHING / RESEARCH / AWARDS / VOLUNTEER
            bulk_insert("TeachingExperience", ["user_id", "courses_taught", "organization", "time_period", "curriculum_description"], data.get('teaching_experience'), 
                        lambda x: (user_uuid, x.get('courses_taught'), x.get('organization'), x.get('time_period'), x.get('curriculum_description')))
            
            bulk_insert("ResearchExperience", ["user_id", "project_name", "lab_or_field_work", "organization", "results_description"], data.get('research_experience'), 
                        lambda x: (user_uuid, x.get('project_name'), x.get('lab_or_field_work'), x.get('organization'), x.get('results_description')))

            bulk_insert("Awards", ["user_id", "award_name", "organization", "description"], data.get('awards'), 
                        lambda x: (user_uuid, x.get('award_name'), x.get('organization'), x.get('description')))

            bulk_insert("VolunteerExperience", ["user_id", "organization", "role", "description"], data.get('volunteer_experience'), 
                        lambda x: (user_uuid, x.get('organization'), x.get('role'), x.get('description')))

            conn.commit()
            print("✅ All tables updated successfully.")
            return True
        except Exception as e:
            if conn: conn.rollback()
            print(f"❌ DB Error: {e}")
            return False
        finally:
            if conn: conn.close()

    def cleanup_temp_files(self, filenames):
        for f in filenames:
            if os.path.exists(f):
                os.remove(f)