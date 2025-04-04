# Proyecto .NET - AuthNetCore

## Descripción
Este es un proyecto en .NET Core para la autenticación de usuarios y la gestión de sesiones, que incluye características como el registro de usuarios, el inicio de sesión, la generación de tokens JWT, y la revocación de tokens.

## Requisitos previos

Antes de comenzar, asegúrate de tener instalados los siguientes requisitos:

- [.NET SDK](https://dotnet.microsoft.com/download) (preferentemente la última versión de .NET Core o .NET 5+).
- [Visual Studio](https://visualstudio.microsoft.com/) o [Visual Studio Code](https://code.visualstudio.com/) para la edición del código.
- [SQL Server](https://www.microsoft.com/en-us/sql-server) o alguna base de datos compatible.
- [Postman](https://www.postman.com/) (opcional, para probar los endpoints de la API).

# Instrucciones para Crear las Tablas en SQL Server

A continuación se muestran las instrucciones para crear las tablas en SQL Server basadas en los modelos proporcionados. Utiliza los siguientes scripts SQL para crear las tablas en tu base de datos.

## 1. Crear la Tabla `Account`

La tabla `Account` almacenará la información básica de la cuenta, como el nombre, apellido, correo electrónico, fecha de nacimiento, estado de bloqueo y los intentos fallidos de inicio de sesión.

```sql
CREATE TABLE Account (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) UNIQUE NOT NULL,
    BirthDate DATE NOT NULL,
    IsLocked BIT NOT NULL DEFAULT 0,
    FailedLoginAttempts INT NOT NULL DEFAULT 0
);
```

2. Crear la Tabla AccountAuth
La tabla `AccountAuth` almacenará la información de autenticación de la cuenta, como el AccountId, el hash de la contraseña y la sal de la contraseña.

```sql
CREATE TABLE AccountAuth (
    AccountId INT PRIMARY KEY,
    PasswordHash VARBINARY(MAX) NOT NULL,
    PasswordSalt VARBINARY(MAX) NOT NULL,
    FOREIGN KEY (AccountId) REFERENCES Account(Id) ON DELETE CASCADE
);
```

3. Crear la Tabla AccountSessionsDto
La tabla `AccountSessionsDto` almacenará las sesiones activas de los usuarios, que incluyen el token de sesión y el estado de revocación.

```sql
CREATE TABLE AccountSessionsDto (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    AccountId INT NOT NULL,
    Token NVARCHAR(MAX) NOT NULL,
    IsRevoked BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (AccountId) REFERENCES Account(Id) ON DELETE CASCADE
);
```

Crear la Tabla BlackListTokenDto
La tabla `BlackListTokenDto` almacenará los tokens que han sido comprometidos o revocados, junto con el AccountId asociado.

```sql
CREATE TABLE BlackListTokenDto (
    AccountId INT NOT NULL,
    Token NVARCHAR(MAX) NOT NULL,
    PRIMARY KEY (AccountId, Token),
    FOREIGN KEY (AccountId) REFERENCES Account(Id) ON DELETE CASCADE
);
```

## Instrucciones de instalación

### 1. Clonar el repositorio
Clona este repositorio en tu máquina local usando el siguiente comando:
git clone https://github.com/tu-usuario/tu-repositorio.git´
O puedes hacerlo directo desde Github Desktop

## Instrucciones para restablcer deependencias desde Visual Studio IDE 
Para restaurar las dependencias de un proyecto .NET en Visual Studio, sigue estos pasos:

## 1. Abrir el Proyecto en Visual Studio

Abre Visual Studio e importa el archivo de solución (`.sln`) del proyecto. Si no tienes el archivo `.sln`, abre la carpeta del proyecto en Visual Studio.

## 2. Restaurar Dependencias

Una vez abierto el proyecto, sigue uno de estos métodos para restaurar las dependencias:

### Opción 1: Usar el Menú de Herramientas

1. En el menú superior, selecciona **Herramientas**.
2. Luego, elige **Administrador de paquetes NuGet** y haz clic en **Restaurar paquetes NuGet**.

### Opción 2: Usar el Explorador de Soluciones

1. En el **Explorador de Soluciones**, haz clic derecho sobre la solución (en la parte superior del árbol de proyectos).
2. Selecciona **Restaurar paquetes NuGet**.

Visual Studio descargará e instalará automáticamente todas las dependencias necesarias desde los archivos de configuración del proyecto, como el archivo `.csproj`.

## 3. Verificación

Una vez restauradas las dependencias, deberías ver que la carpeta `packages` se ha actualizado en el directorio del proyecto. Si no se muestra, verifica que las dependencias se hayan restaurado correctamente.

¡Listo! Las dependencias del proyecto se han restaurado y puedes continuar con el desarrollo o ejecución de la aplicación.

# Instrucciones para Ejecutar la Aplicación ASP.NET Core Web API desde Visual Studio IDE

Para ejecutar una aplicación ASP.NET Core Web API en Visual Studio, sigue estos pasos:

## 1. Abrir el Proyecto en Visual Studio

Abre Visual Studio e importa el archivo de solución (`.sln`) de tu proyecto. Si no tienes el archivo `.sln`, abre la carpeta del proyecto en Visual Studio.

## 2. Seleccionar el Proyecto de Inicio

Asegúrate de que el proyecto de la Web API esté seleccionado como el proyecto de inicio. Si tienes múltiples proyectos en la solución, haz clic derecho sobre el proyecto de la API en el **Explorador de Soluciones** y selecciona **Establecer como proyecto de inicio**.

## 3. Configuración de la Aplicación

Antes de ejecutar, verifica que la configuración de la aplicación esté correcta:

- Abre el archivo `appsettings.json` para comprobar las configuraciones necesarias, como las cadenas de conexión a la base de datos, las variables de entorno, etc.
- Si tu API utiliza configuraciones específicas de entorno, asegúrate de haber configurado correctamente las variables de entorno en Visual Studio.

## 4. Ejecutar la Aplicación

Para ejecutar la aplicación:

1. Haz clic en el botón de **Iniciar** (el botón verde con el ícono de un "play") en la barra superior de Visual Studio, o presiona **F5** en tu teclado.
   
   > Esto lanzará la aplicación en el servidor web integrado de Visual Studio (Kestrel).

2. Si la configuración es correcta, la API debería estar disponible en la URL configurada, que generalmente será algo como `https://localhost:5001` o `http://localhost:5000` (dependiendo de tu configuración).

## 5. Acceder a la API

Una vez que la aplicación esté ejecutándose, puedes acceder a los endpoints de la Web API usando herramientas como:

- **Postman** para realizar peticiones HTTP.
- **Swagger UI**, si tu proyecto está configurado con Swashbuckle o una herramienta similar para documentación de APIs.
  
Normalmente, Swagger estará disponible en una URL como `https://localhost:5001/swagger` para ver y probar los endpoints de la API.

## 6. Detener la Aplicación

Cuando quieras detener la aplicación, puedes hacerlo de las siguientes maneras:

- Haz clic en el botón de **Detener** (el ícono de cuadrado rojo) en la barra superior de Visual Studio.
- O presiona **Shift + F5** en tu teclado.

¡Listo! Ahora tu aplicación ASP.NET Core Web API está ejecutándose y accesible en el servidor local.


