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

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "consulta-pagos" }));

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
// GET /consultapagos -> Lista todos los pagos, incluyendo el nombre
// del cliente asociado (JOIN con Clientes)
// -----------------------------------------------------------------
app.MapGet("/consultapagos", async () =>
{
    try
    {
        var lista = new List<object>();

        await using var conn = await AbrirConexionAsync();

        const string sql = @"SELECT p.PagoId, p.ClienteId, c.Nombre, p.Monto, p.Fecha, p.Referencia, p.Estado
                              FROM Pagos p
                              LEFT JOIN Clientes c ON c.ClienteId = p.ClienteId
                              ORDER BY p.PagoId DESC";
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            lista.Add(new
            {
                pagoId = reader.GetInt32(0),
                clienteId = reader.GetInt32(1),
                clienteNombre = reader.IsDBNull(2) ? "(sin cliente)" : reader.GetString(2),
                monto = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3),
                fecha = reader.IsDBNull(4) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc),
                referencia = reader.IsDBNull(5) ? null : reader.GetString(5),
                estado = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return Results.Ok(lista);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[consultapagos] Error: {ex}");
        return Results.Problem(
            title: "No se pudo consultar pagos",
            detail: ex.Message,
            statusCode: 500);
    }
});

app.Run();
