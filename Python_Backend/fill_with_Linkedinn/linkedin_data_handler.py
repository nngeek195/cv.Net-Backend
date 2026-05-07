import psycopg2
from psycopg2.extras import execute_values
import logging

class LinkedInDataHandler:
    def __init__(self):
        self.conn_params = {
            "dbname": "cvnet2026-capstone-2-database",
            "user": "postgres",
            "password": "CV.Net2026@capstone",
            "host": "35.245.28.42",
            "port": "5432"
        }

    def merge_data(self, user_id, data):
        conn = psycopg2.connect(**self.conn_params)
        cur = conn.cursor()
        try:
            # --- 1. CORE USER: Fill If Empty ---
            u = data.get('user', {})
            cur.execute("""
                UPDATE public."user" SET 
                phone = COALESCE(NULLIF(phone, ''), %s),
                address = COALESCE(NULLIF(address, ''), %s),
                portfolio_url = COALESCE(NULLIF(portfolio_url, ''), %s),
                current_org = COALESCE(NULLIF(current_org, ''), %s),
                current_position = COALESCE(NULLIF(current_position, ''), %s),
                personal_statement = COALESCE(NULLIF(personal_statement, ''), %s),
                about_me = COALESCE(NULLIF(about_me, ''), %s),
                updated_at = CURRENT_TIMESTAMP
                WHERE id = %s
            """, (u.get('phone'), u.get('address'), u.get('portfolioUrl'), u.get('currentOrg'),
                  u.get('currentPosition'), u.get('personalStatement'), u.get('aboutMe'), user_id))

            # --- 2. GENERIC SUB-TABLE MERGER ---
            def merge_unique(table, check_col, data_key, cols, mapping):
                items = data.get(data_key, [])
                if not items: return
                
                cur.execute(f'SELECT "{check_col}" FROM public."{table}" WHERE user_id = %s', (user_id,))
                existing = {row[0] for row in cur.fetchall()}
                
                to_insert = [mapping(i) for i in items if i.get(list(i.keys())[0]) not in existing]
                if to_insert:
                    col_str = ", ".join(cols) + ", created_at, updated_at"
                    execute_values(cur, f'INSERT INTO public."{table}" ({col_str}) VALUES %s',
                                   [v + ('now', 'now') for v in to_insert])

            # --- 3. UPDATED MAPPINGS WITH DATE PROTECTION ---
            
            # Skills (No dates here)
            merge_unique("skill", "skill_name", "skills", ["user_id", "skill_name", "level"], 
                        lambda x: (user_id, x.get('skillName') or "Skill", x.get('level') or "Beginner"))

            # Experience (Protected start_date)
            merge_unique("experience", "company_name", "experience", ["user_id", "company_name", "start_date", "end_date", "role_description"], 
                        lambda x: (user_id, x.get('companyName') or "Unknown", x.get('startDate') or '1900-01-01', x.get('endDate'), x.get('roleDescription') or ""))

            # Education (Protected start_date and end_date)
            merge_unique("education", "degree_title", "education", ["user_id", "degree_title", "field_of_study", "organization", "start_date", "end_date"], 
                        lambda x: (
                            user_id, 
                            x.get('degreeTitle') or "Degree", 
                            x.get('fieldOfStudy') or "General", 
                            x.get('organization') or "Unknown", 
                            x.get('startDate') or '1900-01-01', # Placeholder for empty dates
                            x.get('endDate') or '1900-01-01'     # Placeholder for empty dates
                        ))

            # Certifications (Protected issue_date)
            merge_unique("certification", "field", "certifications", ["user_id", "organization", "field", "issue_date"], 
                        lambda x: (user_id, x.get('organization') or "Unknown", x.get('field') or "Certification", x.get('issueDate') or '1900-01-01'))

            # Awards
            merge_unique("award", "award_name", "awards", ["user_id", "award_name", "organization", "description"], 
                        lambda x: (user_id, x.get('awardName') or "Award", x.get('organization') or "", x.get('description') or ""))

            conn.commit()
            return True
        except Exception as e:
            if conn: conn.rollback()
            raise e
        finally:
            if conn: conn.close()