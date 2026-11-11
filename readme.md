## Ejecutar el programa

```sh
& "C:\Program Files\dotnet\dotnet.exe" run
```

## ingreso a mi base de datos local

```sh
sqlcmd -S localhost -E -C
```

## creacion de mi base de datos y llenado de tabla y datos
```sql
CREATE DATABASE MORA_INTEGGRE;
GO


-- 1. Crear la base de datos
CREATE DATABASE persona;
GO


-- 2. Entrar a la base de datos recién creada
USE persona;
GO

-- 3. Crear la tabla de personas
CREATE TABLE usuarios (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre VARCHAR(50),
    apellido VARCHAR(50),
    edad INT,
    cargo VARCHAR(50)
);
GO

-- 4. Insertar 10 registros de prueba
INSERT INTO usuarios (nombre, apellido, edad, cargo) VALUES 
('Carlos', 'Mendoza', 28, 'Desarrollador'),
('Lucia', 'Fernandez', 34, 'Analista de Datos'),
('Roberto', 'Gomez', 45, 'Gerente TI'),
('Ana', 'Martinez', 31, 'Diseñadora UX'),
('Jorge', 'Salinas', 29, 'Especialista en Redes'),
('Elena', 'Vargas', 38, 'Scrum Master'),
('Miguel', 'Torres', 26, 'Soporte Tecnico'),
('Sofía', 'Castro', 41, 'Arquitecta de Software'),
('Diego', 'Ramirez', 33, 'Administrador de BD'),
('Valentina', 'Perez', 27, 'QA Tester');
GO

-- 5. Verificar que los datos se guardaron correctamente
SELECT * FROM usuarios;
GO
```

## cifrar y descifrar backup 

## Importar a la base de datos

```sh
sqlcmd -S localhost -E -C -Q "RESTORE DATABASE [MORA_INTEGGRE20260801_21-00] FROM DISK='C:\Backup\MORA_INTEGGRE20260801_21-00_extraido\MORA_INTEGGRE20260801_21-00.bak' WITH REPLACE, MOVE 'MORA_INTEGGRE' TO 'C:\Backup\MORA_INTEGGRE.mdf', MOVE 'MORA_INTEGGRE_log' TO 'C:\Backup\MORA_INTEGGRE_log.ldf'"
```
## Pushear a mi repositorio

```sh
git -c http.sslVerify=false push origin main
```

```sh
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```