# loan-system-api

API para un negocio de préstamos informales en República Dominicana. No maneja cuotas fijas: cada préstamo es una línea de crédito con interés mensual sobre el saldo total (capital + interés pendiente). El frontend (React) es un proyecto aparte.

## Qué hace

- **Clientes** y **usuarios** con dos roles: ADMIN (dueño) y WORKER (empleado).
- **Caja:** aportes, retiros y gastos, con control de efectivo disponible.
- **Préstamos:** desembolso, pagos con cascada (primero interés, luego capital) e idempotencia, condonaciones, reversiones, incobrables y congelamientos.
- **Corte mensual:** job que genera los cargos de interés el día 1 y recupera meses perdidos.
- **Reportes:** saldo de caja, interés devengado y cobrado, deuda pendiente, ganancia real y hoja de cobro mensual.
- **Seguridad:** login con JWT y refresh token rotativo; auditoría automática de cambios y sesiones.

## Stack

.NET 8 · ASP.NET Core · EF Core 8 · PostgreSQL · xUnit (pruebas de integración contra PostgreSQL real).

## Puesta en marcha

Requisitos: .NET 8 SDK y PostgreSQL.

1. **Base de datos.** Crea la base y ejecuta el esquema (se puede volver a correr sin perder datos):
   ```bash
   psql -d prestamos -f db/schema.sql
   ```
2. **Conexión.** Crea `appsettings.Development.json` (no se sube al repo):
   ```json
   { "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=prestamos;Username=<usuario>;Password=<contraseña>" } }
   ```
3. **Clave del JWT** (mínimo 32 bytes, vive en user-secrets, fuera del repo):
   ```bash
   dotnet user-secrets set "Jwt:SigningKey" "<clave-larga-y-aleatoria>"
   ```
4. **Ejecutar:**
   ```bash
   dotnet run
   ```
   Swagger queda en `/swagger`. Entra con `POST /api/auth/login` y pega el `accessToken` en **Authorize**.

Usuario inicial: `admin` / `admin123`. **Cámbiale la contraseña** después del primer login (`PUT /api/users`).

## Pruebas

```bash
dotnet test tests/LoanSystemAPI.IntegrationTests
```

Usan la base `prestamos_test` en el mismo servidor de `appsettings.Development.json`; la crean y reconstruyen sola desde `db/schema.sql`. Nunca corren contra una base que no termine en `_test`. La variable `LOANSYSTEM_TEST_CONNECTION` reemplaza la conexión completa.

## Estructura

| Carpeta | Contenido |
|---|---|
| `Controllers/` | Endpoints |
| `Services/` | Lógica compartida: saldos, caja, tokens, jobs, reportes, auditoría |
| `Entities/`, `DTOs/`, `Enums/` | Modelo, contratos de la API y enumeraciones |
| `Data/` | `AppDbContext` e interceptor de auditoría |
| `db/schema.sql` | Esquema de la base (sin migraciones) |
| `tests/` | Pruebas de integración |
| `docs/` | Documentación del proyecto (vault de Obsidian) |

## Documentación

- [Guía para consumir la API](docs/02%20Arquitectura/Guia%20de%20la%20API.md): endpoints, cuerpos, respuestas, códigos y enums.
- [Modelo de negocio](docs/01%20Negocio/Modelo%20de%20negocio.md): las reglas del préstamo, con ejemplos.
- [Inicio del vault](docs/00%20Inicio.md): arquitectura, decisiones, plan y reglas para contribuir (también para agentes de IA).
