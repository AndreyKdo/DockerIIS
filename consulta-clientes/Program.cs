using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

string sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "host.docker.internal";
string sqlDatabase = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "DockerWebinar";
string sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "sa";
string sqlPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? "DockerDemo2026!";

string connectionString =
    $"Server={sqlServer},1433;Database={sqlDatabase};User Id={sqlUser};Password={sqlPassword};" +
    "TrustServerCertificate=True;Connect Timeout=5;";

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("PermitirTodo");

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "consulta-clientes" }));

app.MapGet("/health/db", async () =>
{
    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
        return Results.Ok(new { status = "ok", server = sqlServer, database = sqlDatabase });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[health/db] Fallo de conexion a {sqlServer}: {ex}");
        return Results.Problem(
            title: "No se pudo conectar a SQL Server",
            detail: $"{ex.GetType().Name}: {ex.Message}",
            statusCode: 500);
    }
});

// -----------------------------------------------------------------
// GET /consultaclientes -> Lista todos los clientes registrados
// -----------------------------------------------------------------
app.MapGet("/consultaclientes", async () =>
{
    try
    {
        var lista = new List<object>();

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = "SELECT ClienteId, Cedula, Nombre, Saldo FROM Clientes ORDER BY ClienteId DESC";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            lista.Add(new
            {
                clienteId = reader.GetInt32(0),
                cedula = reader.IsDBNull(1) ? null : reader.GetString(1),
                nombre = reader.IsDBNull(2) ? null : reader.GetString(2),
                saldo = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3)
            });
        }

        return Results.Ok(lista);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[consultaclientes] Error: {ex}");
        return Results.Problem(
            title: "No se pudo consultar clientes",
            detail: ex.Message,
            statusCode: 500);
    }
});

app.Run();
