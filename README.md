# Student Marks Management API

A RESTful ASP.NET Core API for managing students, courses, enrollments, and academic marks. The project uses PostgreSQL for persistence, Entity Framework Core for data access, JWT for authentication, and Swagger for API documentation.

This API supports both teacher and student workflows:
- Teachers can create and manage students, courses, enrollments, marks, and academic reports.
- Students can authenticate and view their own data and transcript.

## Features

- User authentication with JWT
- Role-based authorization for `Teacher` and `Student`
- Student CRUD operations
- Course CRUD operations
- Enrollment management and mark updates
- Course statistics and analytics
- Student transcript generation
- Search, sorting, and pagination on list endpoints
- Health checks for API and database readiness
- Swagger UI with JWT bearer support
- CORS support for frontend clients
- Validation and business-rule enforcement
- Structured logging
- UTC timestamp handling
- PostgreSQL database seeding and EF Core migrations

## Tech Stack

- C#
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Npgsql
- JWT / Bearer Authentication
- Swagger / OpenAPI
- .NET 10

## Project Structure

```text
MarksApi/
├── Controllers/
│   ├── AuthController.cs
│   ├── StudentsController.cs
│   ├── CoursesController.cs
│   └── EnrollmentsController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── DbSeeder.cs
├── DTOs/
│   ├── AuthResult.cs
│   ├── CourseCreateDto.cs
│   ├── CourseResponseDto.cs
│   ├── CourseStatisticsDto.cs
│   ├── CourseUpdateDto.cs
│   ├── EnrollmentCreateDto.cs
│   ├── EnrollmentMarkUpdateDto.cs
│   ├── EnrollmentResponseDto.cs
│   ├── LoginRequest.cs
│   ├── PaginatedResult.cs
│   ├── StudentCreateDto.cs
│   ├── StudentResponseDto.cs
│   ├── StudentTranscriptDto.cs
│   ├── StudentUpdateDto.cs
│   ├── TranscriptCourseDto.cs
│   └── ...
├── Entities/
│   ├── Course.cs
│   ├── Enrollment.cs
│   ├── Student.cs
│   └── User.cs
├── Services/
│   ├── AuthService.cs
│   ├── IAuthService.cs
│   └── ...
├── Migrations/
├── Properties/
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── MarksApi.csproj
├── README.md
└── ...
```

## Database Model

The application uses PostgreSQL with the following core tables:

- `Students`
- `Courses`
- `Enrollments`
- `Users`

Relations:
- One student can have many enrollments.
- One course can have many enrollments.
- Each enrollment belongs to one student and one course.
- Each user may optionally be linked to a student record.

## Prerequisites

Before running the project, install:

- .NET 10 SDK
- PostgreSQL
- A valid local PostgreSQL database and user

## Configuration

Use .NET user secrets for local development so credentials are not stored in source control.

Set the following values:

```bash
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5432;Database=marks_db;Username=marks_user;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_SUPER_SECRET_KEY_32_PLUS_CHARS"
dotnet user-secrets set "Jwt:Issuer" "MarksApi"
dotnet user-secrets set "Jwt:Audience" "MarksApiUsers"
dotnet user-secrets set "Jwt:ExpiryMinutes" "60"
dotnet user-secrets set "SeedSettings:DefaultPassword" "YOUR_SEED_PASSWORD"
```

You can also configure values in `appsettings.json` or `appsettings.Development.json` for local testing if needed.

## Database Setup

Create the PostgreSQL database and apply migrations:

```bash
dotnet ef database update
```

The application seed logic runs automatically on startup and creates default records for teacher and student users when appropriate.

## Running the API

From the `MarksApi` directory:

```bash
dotnet restore
dotnet run
```

The application starts on the configured URL, typically:

```text
http://localhost:5062
```

## Health Check

```http
GET /health
```

The health check verifies whether the API is running and whether the database is reachable.

## Swagger / API Documentation

When running in development mode, Swagger is enabled at:

```text
http://localhost:5062/swagger
```

Swagger includes authentication support using a JWT bearer token.

## Authentication

### Login

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "teacher",
  "password": "yourPassword"
}
```

Example successful response:

```json
{
  "token": "eyJhbGciOi...",
  "username": "teacher",
  "role": "Teacher"
}
```

Use the returned token in the `Authorization` header:

```http
Authorization: Bearer <token>
```

## Core Endpoints

### Auth

```http
POST /api/auth/login
GET  /api/auth/test-token
```

### Students

```http
GET    /api/students
GET    /api/students/{id}
GET    /api/students/{id}/transcript
POST   /api/students
PUT    /api/students/{id}
DELETE /api/students/{id}
```

Students can use search, sort, and pagination in list requests:

```http
GET /api/students?page=1&pageSize=10&search=Alice&sortBy=fullName&sortOrder=asc
```

### Courses

```http
GET    /api/courses
GET    /api/courses/{id}
GET    /api/courses/{id}/statistics
POST   /api/courses
PUT    /api/courses/{id}
DELETE /api/courses/{id}
```

Example:

```http
GET /api/courses?page=1&pageSize=10&search=CS&sortBy=code&sortOrder=asc
```

### Enrollments

```http
GET    /api/enrollments
GET    /api/enrollments/{id}
POST   /api/enrollments
PUT    /api/enrollments/{id}/mark
DELETE /api/enrollments/{id}
```

Example:

```http
GET /api/enrollments?page=1&pageSize=10&search=Alice&sortBy=studentFullName&sortOrder=asc
```

## Pagination, Search, and Sorting

List endpoints support pagination and filtering:

- `page`: page number starting at 1
- `pageSize`: number of records per page
- `search`: free-text filter
- `sortBy`: column/property name to sort by
- `sortOrder`: `asc` or `desc`

Example:

```http
GET /api/students?page=1&pageSize=10&search=alice&sortBy=email&sortOrder=asc
```

## Validation Rules

The API validates incoming data before saving it:

- Student registration numbers must not be empty and must be unique.
- Student names must not be empty.
- Email must be valid and unique.
- Course code must be non-empty and unique.
- Course title must be non-empty.
- Credit hours must be between 1 and 4.
- Marks must be between 0 and 100.
- Students cannot enroll in the same course twice.
- Student and course existence is verified before enrollment records are created.

## Business Logic Notes

- A passing mark is considered `>= 50`.
- Transcript averages are weighted by course credit hours.
- Students are only allowed to access their own account data and transcript.
- Teachers can access all records and reports.
- Course and enrollment deletions are blocked when dependent records exist.

## Smoke Tests

This project includes a separate smoke-test console application that exercises the live API end-to-end.

Smoke tests validate:

1. Health endpoint
2. Teacher login
3. JWT token issuance and auth validation
4. Students retrieval
5. Student-by-id retrieval
6. Course retrieval
7. Course-by-id retrieval
8. Enrollment retrieval
9. Course statistics
10. Student transcript

### Run smoke tests

Start the API first:

```bash
cd MarksApi
dotnet run
```

Then run the smoke tests in another terminal:

```bash
dotnet run --project ../MarksApi.SmokeTests/MarksApi.SmokeTests.csproj
```

## Future Improvements

- Add a frontend application
- Expand reporting and analytics
- Add more integration test coverage
- Export transcripts and reports as PDF or CSV
- Improve audit logging and admin operations

## License

This project is intended for academic and learning purposes.

## Author

Student Marks Management API
