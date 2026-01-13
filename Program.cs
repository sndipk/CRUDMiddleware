
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// Services
// ---------------------------
builder.Services.AddEndpointsApiExplorer();




builder.Services.AddSwaggerGen(options =>
{
    const string schemeId = "bearer";

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UserManagementAPI",
        Version = "v1"
    });

    options.AddSecurityDefinition(schemeId, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter: Bearer techhive-dev-token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(schemeId, document)] = new List<string>()
        });
});

// CORS (optional for browser-based internal tools)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllForDev", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Logging is already wired by default in ASP.NET Core.
// We'll use it from app.Logger and DI.

var app = builder.Build();

app.UseCors("AllowAllForDev");

// Swagger (keep open for dev testing; do NOT do this in production unless secured)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------------------------
// In-memory store (thread-safe)
// ---------------------------
var users = new ConcurrentDictionary<int, User>();
var nextId = 0;

UserSeeder.SeedData(users, ref nextId);

// =======================================================
// Step 5: Configure middleware pipeline (REQUIRED ORDER)
// 1) Error-handling middleware FIRST
// 2) Authentication middleware NEXT
// 3) Logging middleware LAST
// =======================================================

// 1) Error handling (outermost)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // Log exception details server-side (do not leak internals to client)
        app.Logger.LogError(ex, "Unhandled exception occurred while processing {Method} {Path}",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Internal server error."
        });
    }
});

// 2) Token authentication middleware
app.Use(async (context, next) =>
{
    // Allow swagger endpoints without token (for local testing convenience)
    // You can remove this if you want swagger secured too.
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        await next();
        return;
    }

    // Read expected token from configuration (recommended)
    // Add in appsettings.json:  "Auth": { "Token": "techhive-dev-token" }
    var expectedToken = builder.Configuration["Auth:Token"];

    // Fallback token (dev only) if config isn't set
    expectedToken ??= "techhive-dev-token";

    // Extract token from Authorization header: "Bearer <token>"
    string? authHeader = context.Request.Headers.Authorization;

    string? providedToken = null;
    if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        providedToken = authHeader["Bearer ".Length..].Trim();
    }

    // Optional: allow token via header X-API-TOKEN (handy for internal tools)
    if (string.IsNullOrWhiteSpace(providedToken) && context.Request.Headers.TryGetValue("X-API-TOKEN", out var tokenValues))
    {
        providedToken = tokenValues.FirstOrDefault();
    }

    // Validate token
    if (string.IsNullOrWhiteSpace(providedToken) || !string.Equals(providedToken, expectedToken, StringComparison.Ordinal))
    {
        // Because Logging middleware is "last", it won't run if we short-circuit here.
        // So we log unauthorized attempts here to satisfy auditing requirements.
        app.Logger.LogWarning("Unauthorized request blocked: {Method} {Path} - Missing/Invalid token",
            context.Request.Method, context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized. Invalid or missing token."
        });
        return; // stop pipeline
    }

    await next();
});

// 3) Logging middleware (last)
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var path = context.Request.Path;

    // Run endpoint / next middleware
    await next();

    // Log after response is generated (status code available)
    var statusCode = context.Response.StatusCode;
    app.Logger.LogInformation("HTTP {Method} {Path} => {StatusCode}", method, path, statusCode);
});

// ---------------------------
// CRUD Endpoints: /api/users
// ---------------------------

// GET all users
app.MapGet("/api/users", () =>
{
    return Results.Ok(users.Values.OrderBy(u => u.Id));
})
.WithName("GetAllUsers")
.WithSummary("Retrieve all users");

// GET user by id
app.MapGet("/api/users/{id:int}", (int id) =>
{
    if (users.TryGetValue(id, out var user))
        return Results.Ok(user);

    return Results.NotFound(new { error = $"User with ID {id} not found." });
})
.WithName("GetUserById")
.WithSummary("Retrieve a user by ID");

// POST create user
app.MapPost("/api/users", (CreateUserRequest request) =>
{
    var validation = UserValidation.ValidateCreate(request);
    if (validation is not null)
        return Results.BadRequest(validation);

    var id = Interlocked.Increment(ref nextId);

    var user = new User
    {
        Id = id,
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        Email = request.Email.Trim(),
        Department = request.Department?.Trim(),
        Title = request.Title?.Trim(),
        IsActive = request.IsActive ?? true,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    users[id] = user;

    return Results.Created($"/api/users/{id}", user);
})
.WithName("CreateUser")
.WithSummary("Create a new user");

// PUT update user
app.MapPut("/api/users/{id:int}", (int id, UpdateUserRequest request) =>
{
    if (!users.TryGetValue(id, out var existing))
        return Results.NotFound(new { error = $"User with ID {id} not found." });

    var validation = UserValidation.ValidateUpdate(request);
    if (validation is not null)
        return Results.BadRequest(validation);

    if (!string.IsNullOrWhiteSpace(request.FirstName))
        existing.FirstName = request.FirstName.Trim();

    if (!string.IsNullOrWhiteSpace(request.LastName))
        existing.LastName = request.LastName.Trim();

    if (!string.IsNullOrWhiteSpace(request.Email))
        existing.Email = request.Email.Trim();

    if (request.Department is not null)
        existing.Department = request.Department.Trim();

    if (request.Title is not null)
        existing.Title = request.Title.Trim();

    if (request.IsActive is not null)
        existing.IsActive = request.IsActive.Value;

    existing.UpdatedAtUtc = DateTime.UtcNow;

    users[id] = existing;

    return Results.Ok(existing);
})
.WithName("UpdateUser")
.WithSummary("Update an existing user");

// DELETE user
app.MapDelete("/api/users/{id:int}", (int id) =>
{
    if (users.TryRemove(id, out _))
        return Results.NoContent();

    return Results.NotFound(new { error = $"User with ID {id} not found." });
})
.WithName("DeleteUser")
.WithSummary("Delete a user");

// A test endpoint to intentionally throw an exception (for Step 6 testing)
app.MapGet("/api/test/throw", () =>
{
    throw new InvalidOperationException("This is a test exception.");
})
.WithName("ThrowTest")
.WithSummary("Throws an exception to test error middleware");

app.Run();


// ===========================
// Helper classes & models
// ===========================
static class UserSeeder
{
    public static void SeedData(ConcurrentDictionary<int, User> users, ref int nextId)
    {
        var id1 = Interlocked.Increment(ref nextId);
        users[id1] = new User
        {
            Id = id1,
            FirstName = "Aarav",
            LastName = "Sharma",
            Email = "aarav.sharma@techhive.local",
            Department = "IT",
            Title = "System Admin",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };

        var id2 = Interlocked.Increment(ref nextId);
        users[id2] = new User
        {
            Id = id2,
            FirstName = "Diya",
            LastName = "Mehta",
            Email = "diya.mehta@techhive.local",
            Department = "HR",
            Title = "HR Specialist",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-8),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
    }
}

static class UserValidation
{
    public static object? ValidateCreate(CreateUserRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(r.FirstName))
            errors["FirstName"] = new[] { "FirstName is required." };

        if (string.IsNullOrWhiteSpace(r.LastName))
            errors["LastName"] = new[] { "LastName is required." };

        if (string.IsNullOrWhiteSpace(r.Email))
            errors["Email"] = new[] { "Email is required." };
        else if (!IsValidEmail(r.Email))
            errors["Email"] = new[] { "Email format is invalid." };

        return errors.Count > 0 ? new { error = "Validation failed.", errors } : null;
    }

    public static object? ValidateUpdate(UpdateUserRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (r.Email is not null && !string.IsNullOrWhiteSpace(r.Email) && !IsValidEmail(r.Email))
            errors["Email"] = new[] { "Email format is invalid." };

        return errors.Count > 0 ? new { error = "Validation failed.", errors } : null;
    }

    private static bool IsValidEmail(string email)
    {
        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email.Trim(), pattern, RegexOptions.IgnoreCase);
    }
}

record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Department,
    string? Title,
    bool? IsActive
);

record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Department,
    string? Title,
    bool? IsActive
);

class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Department { get; set; }
    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
