using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------
// Configuracion de conexion a SQL Server (contenedor Linux via WSL2)
// Se lee desde variables de entorno para no tener que reconstruir la
// imagen si la IP del contenedor Linux cambia al reiniciar WSL2.
// -----------------------------------------------------------------
string sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "192.168.92.148";
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
// Health check: usado por la pagina web para mostrar el estado del
// microservicio (en linea / caido) de forma independiente al de
// api-consultas.
// -----------------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "api-pagos" }));

// -----------------------------------------------------------------
// GET /health/db -> Diagnóstico real de conectividad a SQL Server.
// A diferencia de /health (que solo confirma que el contenedor .NET
// está vivo), este endpoint abre una conexión real y devuelve el
// mensaje exacto de la falla: timeout de red (host inalcanzable),
// login failed (credenciales), certificado, etc.
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
// POST /api/clientes  -> Inserta un nuevo cliente
// -----------------------------------------------------------------
app.MapPost("/api/clientes", async (ClienteInsertDto dto) =>
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
        Console.WriteLine($"[api/clientes] Error: {ex}");
        return Results.Problem(
            title: "No se pudo registrar el cliente",
            detail: ex.Message,
            statusCode: 500);
    }
});

// -----------------------------------------------------------------
// POST /api/pagos  -> Inserta un nuevo pago asociado a un cliente
// -----------------------------------------------------------------
app.MapPost("/api/pagos", async (PagoInsertDto dto) =>
{
    try
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        const string sql = @"INSERT INTO Pagos (ClienteId, Monto, Fecha, Referencia, Estado)
                              OUTPUT INSERTED.PagoId
                              VALUES (@ClienteId, @Monto, GETDATE(), @Referencia, @Estado)";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@ClienteId", dto.ClienteId);
        cmd.Parameters.AddWithValue("@Monto", dto.Monto);
        cmd.Parameters.AddWithValue("@Referencia", dto.Referencia ?? string.Empty);
        cmd.Parameters.AddWithValue("@Estado", dto.Estado ?? "Pendiente");

        var nuevoId = await cmd.ExecuteScalarAsync();

        return Results.Ok(new { mensaje = "Pago registrado correctamente", pagoId = nuevoId });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[api/pagos] Error: {ex}");
        return Results.Problem(
            title: "No se pudo registrar el pago",
            detail: ex.Message,
            statusCode: 500);
    }
});

app.Run();

record ClienteInsertDto(string? Cedula, string? Nombre, decimal Saldo);
record PagoInsertDto(int ClienteId, decimal Monto, string? Referencia, string? Estado);
