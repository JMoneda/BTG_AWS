using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BTG.Api.Config;
using BTG.Api.Endpoints;
using BTG.Application.Exceptions;
using Serilog;
using BTG.Infrastructure;
using Amazon.Lambda.AspNetCoreServer.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Si corre en AWS, usar hosting Lambda 
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);


// Logging con Serilog
builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

// Inyección de dependencias
builder.Services.AddInfrastructure(builder.Configuration);

// Seguridad y autenticación (AddAuthentication + AddAuthorization + JwtBearer)
builder.Services.AddSecurity(builder.Configuration);

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BTG API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Bearer. Ej: Bearer {token}",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Middleware global de excepciones
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (BusinessException ex)
    {
        ctx.Response.StatusCode = ex.Status;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = "Error interno", detail = ex.Message });
    }
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// claims de JWT conserva "sub", "role", etc
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Log del header Authorization
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    Console.WriteLine(string.IsNullOrWhiteSpace(authHeader)
        ? "No se encontró header Authorization"
        : $"Authorization header: {authHeader}");
    await next();
});

// Autenticación y Autorización
app.UseAuthentication();
app.UseAuthorization();

// Log del usuario autenticado
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var userId = context.User.FindFirst("sub")?.Value
                     ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? "(sin sub)";
        var roles = string.Join(",", context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value));

        Console.WriteLine($"Usuario autenticado: {userId}");
        Console.WriteLine($"Roles: {roles}");
    }
    else
    {
        Console.WriteLine("Usuario NO autenticado");
    }

    await next();
});

// Endpoints REST
app.MapAuthEndpoints();
app.MapClientesEndpoints();
app.MapFondosEndpoints();
app.MapTransaccionesEndpoints();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

await app.RunAsync();
