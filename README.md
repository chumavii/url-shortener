# URL Shortener 

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![React](https://img.shields.io/badge/Frontend-React-61DAFB?logo=react)
![Docker](https://img.shields.io/badge/Docker-444444?logo=docker)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-336791?logo=postgresql)
![Redis](https://img.shields.io/badge/Redis-4B4B4B?logo=redis)
![GitHub Actions](https://img.shields.io/github/actions/workflow/status/chumavii/url-shortener/ci.yml?label=CI%20Build&logo=github)
![Vercel](https://img.shields.io/badge/Vercel-black?logo=vercel)

---

## Overview

A **full-stack URL shortening service** built with **.NET 8 Web API**, **PostgreSQL**, and **Redis**, paired with a **React + Vite** frontend.  
The backend is **containerized with Docker** for consistent development, testing, and deployment, while the frontend is **deployed on Vercel**.

<img width="824" height="606" alt="image" src="https://github.com/user-attachments/assets/a137b5cd-71eb-4ec0-aa8e-d986a3777ffb" />

---

## Features

- Shorten long URLs into clean, shareable links  
- Expand shortened URLs back to their original form  
- Persistent storage with **PostgreSQL**  
- High-speed caching using **Redis**  
- Automated integration testing via **xUnit + GitHub Actions**  
- **Dockerized backend** for seamless local and CI environments  
- **React + Vite frontend**, deployed on **Vercel**

---

## 🧱 Project Structure

```
urlshortener/
│
├── UrlShortener/                 # Main Web API project
│   ├── Controllers/              # API endpoints (UrlController)
│   ├── Data/                     # EF Core DbContext and migrations
│   ├── Models/                   # Entity and DTO classes
│   ├── Services/                 # Helper and logic classes (e.g., URL generation)
│   ├── Middleware/               # Custom middlewares (logging, exception handling)
│   ├── Program.cs                # Application entry point and service configuration
│   ├── appsettings.json          # Configuration file
│   └── Dockerfile                # Backend Docker configuration
│
├── UrlShortener.Tests/           # Test project
│   ├── UnitTests/                # Unit tests for controllers and helpers
│   ├── IntegrationTests/         # Tests that use real DB/Redis via containers
│   └── Dockerfile                # Test Docker configuration
│
├── Utilities.Encode/             # Helper project for URL encoding
│   └── Url64Helper.cs            # Base64-style short code generator
│
├── urlshortener.ui/              # Frontend (React + Vite)
│   ├── src/                      # Components, pages, and services
│   ├── public/                   # Static assets
│   ├── vite.config.ts            # Vite configuration
│   └── package.json              # Frontend dependencies
│
├── docker-compose.yml            # Local multi-container setup (API + DB + Redis)
├── docker-compose.test.yml       # Test environment setup for CI
├── ci.yml                        # GitHub Actions CI pipeline
└── README.md                     # Project documentation
```


---

## ⚙️ Local Development


1️. Clone the repository
```bash
git clone https://github.com/chumavii/UrlShortener.git
cd urlshortener
```

2️. Create a .env file
```bash
POSTGRES_USER=
POSTGRES_PASSWORD=
POSTGRES_DB=
REDIS_HOST=
```

3️. Run the backend with Docker Compose
```bash
docker compose up --build
```
This starts:
 - The .NET 8 API
 - PostgreSQL
 - Redis

API available at → http://localhost:8080

4️. Run the frontend (React + Vite)
```bash
cd urlshortener.ui
npm install
npm run dev
```

---

## 🧪 Running Tests

To run the full integration test suite locally:

```bash
dotnet test
```

Your CI pipeline automatically:
- Spins up PostgreSQL & Redis containers  
- Waits until services are healthy  
- Runs all tests using xUnit

- ---

## Tech Stack

| Layer | Technology |
|-------|-------------|
| **Frontend** | React + Vite (TypeScript) |
| **Backend** | ASP.NET Core 8 Web API |
| **Database** | PostgreSQL |
| **Cache** | Redis |
| **Testing** | xUnit + WebApplicationFactory |
| **CI/CD** | GitHub Actions |
| **Deployment** | Backend via Docker / Frontend via Vercel |

---

## Author

**Chuma**  
Backend Engineer •
[GitHub @chumavii](https://github.com/chumavii)

