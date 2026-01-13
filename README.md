
# User Management API (CRUD + Custom Middleware)

A minimal **ASP.NET Core Web API** built for **TechHive Solutions** to manage internal user records for HR and IT teams.  
This project includes:

*   ✅ **CRUD endpoints** for users (Create, Read, Update, Delete)
*   ✅ **Custom middleware** for:
    *   Request/response **logging** (audit trail)
    *   Standardized **error handling** (consistent JSON errors)
    *   **Token-based authentication** (blocks unauthorized access)
*   ✅ **Swagger UI** support for interactive testing

***

## 🚀 Features

### ✅ User CRUD API

*   **GET** `/api/users` → list all users
*   **GET** `/api/users/{id}` → get a user by ID
*   **POST** `/api/users` → create a new user
*   **PUT** `/api/users/{id}` → update an existing user
*   **DELETE** `/api/users/{id}` → delete a user

### ✅ Middleware (Corporate Policy Compliance)

This API implements middleware required by TechHive Solutions:

1.  **Error Handling Middleware (first)**
    *   Catches unhandled exceptions
    *   Returns consistent JSON:
        ```json
        { "error": "Internal server error." }
        ```

2.  **Authentication Middleware (second)**
    *   Validates token from request headers
    *   Allows only requests with valid token
    *   Returns:
        ```json
        { "error": "Unauthorized. Invalid or missing token." }
        ```

3.  **Logging Middleware (last)**
    *   Logs request HTTP method, path, and response status code
    *   Example log:
            HTTP GET /api/users => 200

> ⚠️ Note: If authentication fails and the pipeline is short-circuited, logging middleware may not execute. To ensure audit logs, unauthorized attempts are also logged inside the auth middleware.

***

## 🧱 Tech Stack

*   **.NET / ASP.NET Core Minimal API**
*   **Swagger/OpenAPI** for API documentation and testing
*   **ConcurrentDictionary** as an in-memory datastore (thread-safe)

***

## 📦 Getting Started

### 1) Clone the repo

```bash
git clone <your-repo-url>
cd CRUDMiddleware
```

### 2) Restore dependencies

```bash
dotnet restore
```

### 3) Configure Token (`appsettings.json`)

Create/update **`appsettings.json`**:

```json
{
  "Auth": {
    "Token": "techhive-dev-token"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

> ✅ The token used in testing examples is: `techhive-dev-token`

### 4) Run the API

```bash
dotnet run
```

Or with hot reload:

```bash
dotnet watch run
```

***

## 🔍 Swagger UI Testing

Once the API is running, open Swagger:

    https://localhost:<port>/swagger

### ✅ Authenticate in Swagger (Authorize button)

1.  Click **Authorize** (🔒 icon)
2.  Enter **ONLY the token value**:
        techhive-dev-token
3.  Click **Authorize**, then **Close**
4.  Now try endpoints like:
    *   `GET /api/users`
    *   `POST /api/users`
    *   etc.

> ✅ Swagger will automatically send:
>
>     Authorization: Bearer techhive-dev-token
>
> ❌ Don’t type `Bearer techhive-dev-token` inside the token box, otherwise Swagger may send `Bearer Bearer ...` and you’ll get 401.

***

## 🧪 Testing with cURL (Alternative)

### ✅ Get all users

```bash
curl -i https://localhost:<port>/api/users \
  -H "Authorization: Bearer techhive-dev-token"
```

### ✅ Create a user

```bash
curl -i https://localhost:<port>/api/users \
  -H "Authorization: Bearer techhive-dev-token" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Sandip",
    "lastName": "Koli",
    "email": "sandip.koli@techhive.local",
    "department": "IT",
    "title": "Support Engineer",
    "isActive": true
  }'
```

### ✅ Trigger exception (test error middleware)

```bash
curl -i https://localhost:<port>/api/test/throw \
  -H "Authorization: Bearer techhive-dev-token"
```

Expected:

```json
{ "error": "Internal server error." }
```

***

## 📌 API Endpoints

### Users

*   **GET** `/api/users`
*   **GET** `/api/users/{id:int}`
*   **POST** `/api/users`
*   **PUT** `/api/users/{id:int}`
*   **DELETE** `/api/users/{id:int}`

### Testing

*   **GET** `/api/test/throw` (throws exception intentionally)

***

## ✅ Request Body Examples

### Create User (POST `/api/users`)

```json
{
  "firstName": "Aarav",
  "lastName": "Sharma",
  "email": "aarav.sharma@techhive.local",
  "department": "IT",
  "title": "System Admin",
  "isActive": true
}
```

### Update User (PUT `/api/users/{id}`)

```json
{
  "title": "Senior System Admin",
  "isActive": true
}
```

***

## 🧩 Data Storage Notes

This project uses an **in-memory data store** (`ConcurrentDictionary<int, User>`).

*   Data resets when the app restarts
*   Ideal for development and learning middleware patterns
*   Can be replaced later with **SQL Server / EF Core** for persistence

***

## 🛠 Troubleshooting

### ❌ Error: “Could not parse the JSON file”

Your `appsettings.json` is invalid (extra braces, trailing commas, duplicated JSON objects).  
Validate in PowerShell:

```powershell
Get-Content .\appsettings.json -Raw | ConvertFrom-Json | Out-Null
"JSON is valid ✅"
```

### ❌ 401 Unauthorized in Swagger

*   Ensure you clicked **Authorize**
*   Enter **only** `techhive-dev-token` (not `Bearer ...`)
*   Confirm `appsettings.json` token matches what you are using

***

## 🔐 Security Notes (Next Improvements)

For production readiness, consider:

*   JWT validation using `Microsoft.AspNetCore.Authentication.JwtBearer`
*   Role-based authorization (HR vs IT)
*   Secure secrets using **User Secrets** / **Azure Key Vault**
*   Centralized structured logging (Serilog + sinks)

***

## ✅ Roadmap / Future Enhancements

*   Replace in-memory storage with **EF Core + SQL**
*   Add pagination, filtering, searching
*   Add DTO mapping, FluentValidation
*   Add unit tests & integration tests

***

## 👨‍💻 Author

**Sandip Koli**  
MBA (2025) | CDOE Student  
Project: Middleware-based User Management API (TechHive Solutions scenario)

***
