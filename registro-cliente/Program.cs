using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------
// Configuracion de conexion a SQL Server (contenedor Linux via WSL2,
// alcanzado desde el contenedor Windows a traves de host.docker.internal
// + el netsh portproxy configurado en el host - ver README).
// -----------------------------------------------------------------
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

// -----------------------------------------------------------------
// Health check de proceso (no toca SQL Server)
// -----------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "registro-cliente" }));

// -----------------------------------------------------------------
// Health check real de conectividad a SQL Server
// -----------------------------------------------------------------
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
// POST /registrocliente -> Inserta un nuevo cliente
// -----------------------------------------------------------------
app.MapPost("/registrocliente", async (ClienteInsertDto dto) =>
{
    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"INSERT INTO Clientes (Cedula, Nombre, Saldo)
                              OUTPUT INSERTED.ClienteId
                              VALUES (@Cedula, @Nombre, @Saldo)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Cedula", dto.Cedula ?? string.Empty);
        cmd.Parameters.AddWithValue("@Nombre", dto.Nombre ?? string.Empty);
        cmd.Parameters.AddWithValue("@Saldo", dto.Saldo);

        var nuevoId = await cmd.ExecuteScalarAsync();

        return Results.Ok(new { mensaje = "Cliente registrado correctamente", clienteId = nuevoId });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[registrocliente] Error: {ex}");
        return Results.Problem(
            title: "No se pudo registrar el cliente",
            detail: ex.Message,
            statusCode: 500);
    }
});

app.Run();

record ClienteInsertDto(string? Cedula, string? Nombre, decimal Saldo);
