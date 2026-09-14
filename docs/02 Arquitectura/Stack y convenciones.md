# Stack y convenciones

## Stack

- .NET 8 (`net8.0`), ASP.NET Core Web API con controllers
- EF Core 8 + Npgsql + `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention`)
- PostgreSQL
- Swagger (Swashbuckle) en desarrollo
- Pruebas de integración con xUnit contra `prestamos_test` en el PostgreSQL local (D-047). Diseño en [[Plan del proyecto]]

El esquema **no usa migraciones de EF**: se escribe a mano en `db/schema.sql`.

## Estructura

```
Controllers/   un controller por recurso
DTOs/          un DTO por operación de entrada, uno de lectura por recurso
Entities/      espejo de las tablas
Enums/         un archivo por enum
Data/          AppDbContext
Services/      servicios compartidos entre controllers
db/schema.sql  esquema de la base, escrito a mano
tests/         pruebas de integración (proyecto aparte; la API excluye tests/**)
docs/          este vault
```

## Controllers

- Heredan de `Controller`. Llevan `[ApiController]` y `[Route("api/recurso-en-plural-kebab")]`.
- El constructor inyecta `AppDbContext _dbContext`, `ILogger<NombreController> _logger`, `ICurrentUserService _currentUserService` y los servicios que use (`CashService`, `LocalDateService`).
- `CreatedBy`/`UpdatedBy` salen de `_currentUserService.UserId`. Hoy es el `AdminId` de configuración; con el login (#66) será el usuario del JWT, sin tocar los controllers (D-019).
- Rutas con id usan restricción de tipo `{id:guid}`, y el parámetro lleva `[FromRoute]`. Los cuerpos llevan `[FromBody]`.
- Firma: `async Task<ActionResult<XDTO>>`, o `Task<IActionResult>` cuando no se devuelve cuerpo.
- Las validaciones de negocio van **antes** del `try` y devuelven `BadRequest(new { message = "..." })`.
- Todo el acceso a datos va dentro de `try/catch (Exception ex)`:
  - `_logger.LogError(ex, "MENSAJE EN MAYÚSCULAS")`
  - `return StatusCode(500, new { message = "..." })`
- No encontrado: `NotFound(new { message = $"The X with id {id} was not found" })`.
- Crear: `Created()` o `CreatedAtAction(...)`. Borrar: `NoContent()`.
- **Los mensajes de la API van en inglés.**
- Borrado **lógico**: `Status = DELETED`, nunca `Remove`.

## Consultas

- Las lecturas proyectan a DTO con `.Select(...)` **antes** de `ToListAsync()`, para que EF solo traiga las columnas necesarias y no se filtren campos de auditoría.

## Entities

- Campos obligatorios: `[Required]` en la línea de arriba y `required public` en la declaración.
- Nullables con `?`, espejando exactamente el `NULL`/`NOT NULL` de la tabla.
- Columnas `DATE` → `DateOnly`; columnas `TIMESTAMPTZ` → `DateTime` en UTC.
- Valores por defecto en línea: `= DateTime.UtcNow`, `= CustomerStatus.ACTIVE`, `= ""`.
- Bloque de auditoría al final: `CreatedBy`, `CreatedDate`, `UpdatedBy`, `UpdatedDate`.
- La propiedad de estado se llama `Status`.
- Excepción histórica: en `CashEntry`, `amount` y `status` están en minúscula. Se mantiene así.

## Enums

- Siempre `: short` (se guardan como `SMALLINT`).
- Valores en MAYÚSCULAS, sin guion bajo (`INTERESTCHARGE`, `WRITTENOFF`), empezando en **1**.
- Coma final, y comentario en español al lado si hace falta (`WRITTENOFF = 3, //INCOBRABLE`).

## DTOs

- Un DTO por operación de entrada: `CashEntryContributionDTO`, `CashEntryWithdrawalDTO`, `CashEntryExpenseDTO`.
- El DTO de lectura del recurso lleva el nombre simple: `CashEntryDTO`, `CustomerDTO`.
- Heredan entre sí cuando evitan repetir campos (`CustomerDTO : CustomerRegisterDTO`).
- Los montos se reciben **en positivo**; el API aplica el signo según el tipo (D-004).

## Base de datos

- Tablas en plural y snake_case. `ToTable("nombre")` explícito en `OnModelCreating`.
- `HasQueryFilter` para ocultar los borrados lógicos.
- Constraints con nombre: `fk_tabla_campo`, `uq_tabla_campo`; índices únicos `ux_...`.
- Montos `NUMERIC(11,2)`, tasas `NUMERIC(5,4)`. En C# siempre `decimal`, nunca `float` ni `double`.
- Ids: `Guid.NewGuid()` generado en la aplicación (UUID v4, D-012).
- **"Hoy" sale de `LocalDateService.Today()`**, nunca de `DateTime.Now` ni `DateTime.UtcNow`: usa la hora dominicana y un `TimeProvider` que las pruebas pueden reemplazar.

## Formato de archivos

- `.cs`: UTF-8 **con BOM** y finales de línea **CRLF**, como los genera Visual Studio.
- Namespaces con llaves (`namespace LoanSystemAPI.Controllers { ... }`), no de archivo.

## Commits

- **Una tarea, un commit.** Al terminar una tarea el agente hace el commit, **nunca el push**: el push lo hace el usuario a mano.
- El commit incluye el código **y** las actualizaciones del vault de esa tarea (D-037, D-039).
- `feat: ...` para funcionalidad nueva. Ejemplo: `feat: Cash-Entry Withdrawal Post Endpoint`
- `Update: ...` para ajustes a algo existente. Ejemplo: `Update: endpoint post contribution don't need the field counterparty`
- `docs: ...` para cambios solo de documentación.
- En inglés, con Title Case en los `feat:` y `docs:`.
- **Terminan con `(Task N)`**, por ejemplo `(Task 13)`. No se usa `#13` porque GitHub lo convierte en un enlace al issue o PR 13.
- Si el cambio mezcla dos cosas distintas, se separa en dos commits.
- Rama de trabajo: `dev`. Rama principal: `main`.
