USE DockerWebinar;
GO

CREATE TABLE Clientes
(
    ClienteId INT IDENTITY PRIMARY KEY,

    Cedula VARCHAR(20),

    Nombre VARCHAR(100),

    Saldo DECIMAL(18,2)
);
GO

CREATE TABLE Pagos
(
    PagoId INT IDENTITY PRIMARY KEY,

    ClienteId INT,

    Monto DECIMAL(18,2),

    Fecha DATETIME,

    Referencia VARCHAR(50),

    Estado VARCHAR(20)
);
GO