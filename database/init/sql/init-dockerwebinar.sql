-- ============================================================
-- Script de inicializacion: Base de datos DockerWebinar
-- Ejecutar desde SSMS o sqlcmd conectado al contenedor SQL Server
-- Servidor: 192.168.92.148 (la IP puede cambiar en tu entorno)
-- Usuario:  sa
-- ============================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'DockerWebinar')
BEGIN
    CREATE DATABASE DockerWebinar;
END
GO

USE DockerWebinar;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Clientes')
BEGIN
    CREATE TABLE Clientes
    (
        ClienteId INT IDENTITY PRIMARY KEY,
        Cedula    VARCHAR(20),
        Nombre    VARCHAR(100),
        Saldo     DECIMAL(18,2)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Pagos')
BEGIN
    CREATE TABLE Pagos
    (
        PagoId      INT IDENTITY PRIMARY KEY,
        ClienteId   INT,
        Monto       DECIMAL(18,2),
        Fecha       DATETIME,
        Referencia  VARCHAR(50),
        Estado      VARCHAR(20)
    );
END
GO

-- Datos de ejemplo opcionales para probar el laboratorio de inmediato
IF NOT EXISTS (SELECT * FROM Clientes)
BEGIN
    INSERT INTO Clientes (Cedula, Nombre, Saldo) VALUES
    ('1-1111-1111', 'Ana Rodriguez', 150000.00),
    ('2-2222-2222', 'Carlos Jimenez', 75000.50),
    ('3-3333-3333', 'Maria Solano', 0.00);
END
GO
