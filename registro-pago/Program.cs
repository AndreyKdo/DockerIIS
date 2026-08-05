using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

string sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "host.docker.internal";
string sqlDatabase = Environment.GetEnvironmentVariable("SQL_DATABASE") ?? "DockerWebinar";
string sqlUser = Environment.GetEnvironmentVariable("SQL_USER") ?? "sa";
string sqlPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD") ?? "DockerDemo2026!";

string connectionString =
    $"Server={sqlServer},1433;Database={sqlDatabase};User Id={sqlUser};Password={sqlPassword};" +
    "TrustServerCertificate=True;Connect Timeout=30;Encrypt=False;";

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
// Abre la conexion a SQL Server con reintentos. La ruta de red de
// este laboratorio (contenedor Windows -> portproxy -> WSL2 ->
// contenedor Linux) puede perder el handshake inicial de forma
// intermitente; reintentar 2-3 veces es mas robusto que subir el
// timeout indefinidamente.
// -----------------------------------------------------------------
async Task<SqlConnection> AbrirConexionAsync()
{
    Exception ultimoError = new Exception("No se pudo abrir la conexion");
    for (int intento = 1; intento <= 3; intento++)
    {
        try
        {
            var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            return conn;
        }
        catch (Exception ex)
        {
            ultimoError = ex;
            Console.WriteLine($"[SQL] Intento {intento}/3 fallido: {ex.Message}");
            if (intento < 3) await Task.Delay(700);
        }
    }
    throw ultimoError;
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "registro-pago" }));

app.MapGet("/health/db", async () =>
{
    try
    {
        await using var conn = await AbrirConexionAsync();
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
// POST /registropago -> Inserta un nuevo pago asociado a un cliente
// -----------------------------------------------------------------
app.MapPost("/registropago", async (PagoInsertDto dto) =>
{
    try
    {
        await using var conn = await AbrirConexionAsync();

        const string sql = @"INSERT INTO Pagos (ClienteId, Monto, Fecha, Referencia, Estado)
                              OUTPUT INSERTED.PagoId
                              VALUES (@ClienteId, @Monto, SYSUTCDATETIME(), @Referencia, @Estado)";

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
        Console.WriteLine($"[registropago] Error: {ex}");
        return Results.Problem(
            title: "No se pudo registrar el pago",
            detail: ex.Message,
            statusCode: 500);
    }
});

app.Run();

record PagoInsertDto(int ClienteId, decimal Monto, string? Referencia, string? Estado);
