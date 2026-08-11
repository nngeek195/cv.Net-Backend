# CV.Net Backend

Welcome to the backend workspace of **CV.Net** — a platform that helps candidates build stronger profiles and helps companies hire better.

This repository contains:
- A **.NET Web API** for core business features
- A **Python FastAPI service** for CV and LinkedIn data extraction/mapping
- **Firebase Data Connect** schema/config for database modeling

---

## Project Structure

```text
cv.Net-Backend/
├── Backend.sln                     # Visual Studio solution
├── CVNetBackend/                   # Main .NET backend API (net10.0)
│   ├── Program.cs                  # App startup, DI, auth, CORS, rate limit, Swagger
│   ├── Admin/                      # Admin-side endpoints/services
│   ├── Company_End/                # Company-side modules
│   │   ├── JobPost/
│   │   ├── JobManagement/
│   │   ├── ApplicationsView/
│   │   ├── Interviews/
│   │   ├── CandidateSection/
│   │   └── Dashboard/
│   ├── User_End/                   # Candidate/user-side modules
│   │   ├── LoginManagement/
│   │   ├── ProfileHandler/
│   │   ├── JobApply/
│   │   ├── JobRoleManager/
│   │   ├── CVController/
│   │   ├── DashBoard/
│   │   ├── Enhancer/
│   │   └── Services/
│   ├── appsettings.json            # Base app settings
│   └── appsettings.Development.json
├── Python_Backend/                 # AI-assisted CV + LinkedIn processing service
│   ├── main.py                     # Unified FastAPI entry point
│   ├── Cv_handle/                  # CV PDF extraction + schema mapping + DB sync
│   ├── fill_with_Linkedinn/        # LinkedIn scraping + mapping + DB merge
│   └── requirements.txt            # Python dependencies
├── dataconnect/                    # Firebase Data Connect config and schema
│   ├── dataconnect.yaml
│   ├── schema/schema.gql
│   └── example/connector.yaml
├── API_Guide/                      # Internal text guides and API notes
├── firebase.json                   # Firebase + Data Connect local config
└── .firebaserc                     # Firebase project alias
```

---

## Technologies Used

### Main API (.NET)
- **.NET 10 Web API**
- **ASP.NET Core Controllers**
- **JWT token validation** (Firebase)
- **Npgsql + Dapper** for PostgreSQL access
- **Firebase Admin / Firestore SDK**
- **Swagger** for API docs
- **Rate Limiting** middleware

### AI & Data Processing (Python)
- **FastAPI** + **Uvicorn**
- **pdfplumber** for CV PDF parsing
- **OpenAI-compatible clients** for schema mapping
- **psycopg2** for PostgreSQL writes
- **Requests** for file/API calls

### Data/Cloud
- **Firebase Project** integration
- **Firebase Data Connect** schema and connector files
- **PostgreSQL** as primary relational store

---

## How the Backend Workflow Works

1. **User/Company calls .NET API** (`CVNetBackend`)
2. .NET API handles auth, profile, jobs, applications, and business logic
3. For CV/LinkedIn enrichment, system uses **Python service** (`Python_Backend`)
4. Python extracts raw content, maps to structured schema, then syncs to PostgreSQL
5. Data model is aligned with the **Data Connect GraphQL schema** in `dataconnect/schema/schema.gql`

In short: **.NET API manages platform logic, Python handles AI extraction/mapping, and PostgreSQL stores final structured data.**

---

## Getting Started (Clone and Run)

### 1) Clone the repository

```bash
git clone https://github.com/nngeek195/cv.Net-Backend.git
cd cv.Net-Backend
```

### 2) Prerequisites

Install:
- **.NET SDK 10** (or the SDK matching `TargetFramework net10.0`)
- **Python 3.10+**
- **PostgreSQL**
- (Optional) **Firebase CLI** for Data Connect local workflows

### 3) Configure environment

### .NET side (`CVNetBackend`)
Create:
- `CVNetBackend/.env`
- `CVNetBackend/firebase-key.json`

Typical env values used in code:
- `DB_HOST`
- `DB_PORT`
- `DB_USER`
- `DB_PASSWORD`
- `DB_NAME`

### Python side (`Python_Backend`)
Create:
- `Python_Backend/.env`

Typical env values used in code:
- `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`, `DB_NAME`
- `XAIAPI`
- `API_KEY`
- `PILOTERR_API_KEY`

### 4) Run the .NET API

```bash
cd CVNetBackend
dotnet restore
dotnet run
```

Swagger is enabled in development mode.

### 5) Run the Python service

```bash
cd Python_Backend
python -m venv .venv
source .venv/bin/activate   # On Windows: .venv\Scripts\activate
pip install -r requirements.txt
python main.py
```

Default Python service URL:
- `http://localhost:8000`

---

## Development Workflow (Simple)

1. Pull latest changes
2. Create your branch
3. Run .NET API and Python API locally
4. Test the feature flow end-to-end
5. Commit clean changes
6. Open a PR

---

## Notes for New Contributors

- Keep secrets in `.env` files only, never in source files.
- `Program.cs` is the startup center for the .NET API.
- `Python_Backend/main.py` is the startup center for the Python service.
- `dataconnect/schema/schema.gql` is the source of truth for structured domain entities.

---

If you are new here: start from `CVNetBackend/Program.cs`, then read the module folders under `User_End` and `Company_End`, and finally check `Python_Backend/main.py` to understand the AI data pipeline.
