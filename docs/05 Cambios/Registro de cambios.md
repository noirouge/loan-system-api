# Registro de cambios

Una entrada por cambio, la más reciente arriba. Cada entrada nombra su tarea de [[Tareas]].

Un commit no puede contener su propio hash, así que desde la tarea #12 las entradas no lo citan. Cada commit lleva `(Task N)` en el mensaje, y se encuentra con:

```bash
git log --grep "Task 13"
```

Las entradas anteriores a la #12 sí llevan hash, porque se reconstruyeron desde el historial.

## 2026-09-13

- **#37**: nuevo `Services/MoneyRounding.Round()`, que redondea a 2 decimales mirando solo el tercer decimal (D-043). Tiene pruebas para 1.266, 1.265, 1.2659, 104.16625 y negativos. No usa `Math.Round`, porque ninguno de sus modos sigue esta regla.
- **#110**: `CustomerStatus` pierde `INACTIVE`, y el comentario de `customers.status` en `db/schema.sql` también (D-046, modifica #2, #5). Queda un comentario donde estaba el valor 2 para que no se reutilice. No había usos en el código.
- **#82**: nuevo proyecto `tests/LoanSystemAPI.IntegrationTests` con `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` y `Microsoft.AspNetCore.Mvc.Testing` 8.0.30 (la misma versión que el runtime instalado), con referencia a la API y agregado a `LoanSystemAPI.sln`. Como la API vive en la raíz del repo, su `.csproj` excluye `tests/**` para no compilar las pruebas dentro de la API.
- Respuestas del usuario: redondeo (D-043), pago mayor que la deuda (D-044), validaciones al crear un préstamo (D-045), se quita `INACTIVE` de clientes (D-046, nueva tarea #110) y pruebas contra `prestamos_test` (D-047). Se desbloquean #37 y las pruebas que solo esperaban P-05 o P-13. P-15 queda solo con el rango del día de pago, y P-16 y P-17 se reescriben con el ejemplo de los asientos.
- Respuestas del usuario: la tasa se envía como fracción (D-041, cierra P-14) y pagar de más reduce capital, con el ejemplo de Fulanito agregado a [[Modelo de negocio]] (D-042). Se reescriben con ejemplos P-01, P-05, P-10, P-13 y P-15, y se agregan P-16 (interés impago aparte del capital) y P-17 (pago sugerido del mes). La #39 queda bloqueada solo por P-15 y la #42 por P-10 y P-16.
- Se registran P-14 y P-15 (cómo se envía la tasa y qué se valida al crear un préstamo), que bloquean la #39; la #42 y la #43 pasan a bloqueadas por P-10. Se actualiza el estado del plan tras terminar las tareas desbloqueadas de las fases 1 y 2.
- **#31**: `CashEntryController`, `CustomersController` y `LoansController` dejan de leer `AdminId` de `IConfiguration` y reciben `ICurrentUserService`; `CreatedBy` y `UpdatedBy` salen de `UserId` (modifica #3, #5, #6, #8, #9, #10). Si `AdminId` falta o no es válido, ahora falla en vez de guardar `Guid.Empty`.
- **#22**: el retiro y el gasto abren una transacción, toman el candado de caja (`CashService.LockCashAsync`) y rechazan con `400` si el efectivo disponible no alcanza, informando cuánto hay. Si dos llegan a la vez, la segunda espera a que la primera confirme y ve el saldo ya descontado (D-016, modifica #8, #9).
- **#20**: la reversión de caja ya no consulta antes si la entrada estaba reversada, porque dos peticiones simultáneas pasaban las dos esa consulta. Ahora inserta directo y captura la violación de `uq_cash_entries_reverses` (`SqlState 23505`), respondiendo `409 Conflict` en vez del `400` anterior o de un `500` (modifica #10).
- **#36**: `Loan`, `LoanEntry` y `Freeze` quedan registrados en `AppDbContext` con sus tablas. `Loan` y `Freeze` ocultan sus borrados lógicos con `HasQueryFilter`, igual que `Customer`; `LoanEntry` no lo necesita porque es append-only.
- **#34**: nuevas entidades `RefreshToken`, `AuditLog` y `JobRun`, registradas en `AppDbContext` con sus tablas. `AuditLog.Changes` se mapea como `jsonb`.
- **#35**: las entidades `LoanEntry` y `Freeze` quedan completas, espejo de `loan_entries` y `freezes`. `LoanEntry` no tiene columnas de actualización porque es append-only; `Period`, `ValueDate`, `StartDate` y `EndDate` son `DateOnly`.
- **#32**: `db/schema.sql` crea `refresh_tokens`, `audit_logs` y `job_runs` con sus índices, incluido `ux_job_runs_success` (modifica #2). `token_hash` es SHA-256 en hexadecimal con índice único; `replaced_by` usa `ON DELETE SET NULL` para que la limpieza de tokens vencidos no choque con la FK; `attempted_user` admite 100 caracteres. Todo es `IF NOT EXISTS`, así que volver a correr el script en la base local basta para crearlas.
- **#30**: nuevo `ICurrentUserService` con `UserId`, implementado por `CurrentUserService`, que lee `AdminId` de la configuración y lanza un error si falta o no es un GUID válido. Registrado como scoped en `Program.cs`, para que en #66 pueda leer el JWT de la petición sin cambiar su ciclo de vida.
- **#23**: `POST api/cash-entries/contribution` rechaza con `400` los montos menores o iguales a cero, igual que el retiro y el gasto (modifica #6).
- **#19**: la reversión de caja hereda el `value_date` de la entrada original en vez de usar la fecha de hoy, para no mover el saldo de un período ya reportado (D-013, modifica #10).
- **#33**: nuevos enums `AuditAction` y `JobRunStatus`, con `: short`.
- **#27**: `Loan.CreatedBy` pasa de `Guid?` a `Guid`, porque `loans.created_by` es `NOT NULL`.
- **#25**: las respuestas de clientes devuelven `CustomerDTO` en vez de la entidad, sin `created_by`, `updated_by` ni fechas de auditoría. El listado proyecta con `Select` antes de consultar. Además del GET de lista se corrigieron el GET por id, el POST y el PUT, que tenían la misma fuga (modifica #3, #4, #5).
- **#21**: nuevo `Services/CashService` con `GetAvailableCashAsync()` (`SUM(amount)` de caja) y `LockCashAsync()`, un `pg_advisory_xact_lock` que serializa las operaciones que sacan dinero para que dos retiros simultáneos no vean el mismo saldo. Registrado como scoped en `Program.cs`.
- **#18**: `cash_entries` pierde `updated_by`, `updated_date` y su FK en `db/schema.sql`; se quitan de la entidad `CashEntry`, y la reversión deja de escribirlos en la entrada original (modifica #2, #10). Quién reversó queda en el `created_by` de la reversión (D-014). En una base ya creada las columnas siguen existiendo: son nullables y EF las ignora.
- **#28**: `User.CreatedBy` pasa a `Guid?`, porque `users.created_by` es nullable y el admin semilla no tiene creador.
- **#26**: `LoansController` inyecta `ILogger<LoansController>` en vez de `ILogger<CustomersController>`.
- **#24**: `GET api/customers` ordena por fecha de creación, más recientes primero, en vez de agrupar por `CreatedDate`, que devolvía grupos y no clientes (modifica #4).
- **#17**: nuevo `Services/LocalDateService` con `Today()`, que devuelve la fecha de hoy en `America/Santo_Domingo` a partir de un `TimeProvider` inyectado. Se registran `TimeProvider.System` y el servicio en `Program.cs`.
- **#16**: `CashEntry.ValueDate`, `Loan.LoanDate` y el `ValueDate` de los DTOs de caja pasan de `DateTime` a `DateOnly`, igual que sus columnas `DATE` (modifica #6, #8, #9, #10, #11). En JSON, `valueDate` ahora va como `YYYY-MM-DD`. `period` nacerá como `DateOnly` en la #35. La reversión usa temporalmente la fecha UTC de hoy, hasta la #19.
- **#14**: en `db/schema.sql`, `users.role` pasa a `DEFAULT 1` (WORKER) y el admin semilla se inserta con `role = 2` (ADMIN), alineados con el enum `UserRole` (modifica #2). Antes todo usuario nuevo nacía ADMIN. La base local todavía necesita la #15.
- Se agregan las pruebas de integración al plan y a las tareas: infraestructura y red de seguridad sobre lo que ya existe (#82–#89) y pruebas por fase (#90–#109). Se cancela la #49, reemplazada por #82 y #94 (D-040).
- `docs/` sale de `.gitignore` (D-039). El vault vuelve a ser visible para git, todavía sin commitear.
- Se deshacen con `git reset` los commits de las tareas #12 y #13, que no se habían publicado, y `docs/` pasa a `.gitignore` (D-038). Los archivos se conservan en el disco. El cambio de `db/schema.sql` de la #13 queda sin commitear.
- **#13**: en `db/schema.sql`, el índice `ux_loan_entries_loan_id_and_period` pasa de `entry_type = 1` (desembolso) a `entry_type = 2` (cargo de interés), que es lo que protege el invariante de un cargo por período (modifica #2). Las bases ya creadas conservan el índice viejo porque `CREATE ... IF NOT EXISTS` no lo reemplaza; se agrega la tarea #81 para recrearlo en la base local.
- **#12**: se crea el vault de documentación en `docs/`, con modelo de negocio, arquitectura, decisiones, plan, tareas y preguntas abiertas. Se agregan `AGENTS.md` y `CLAUDE.md` en la raíz, que apuntan al vault, y se ignoran los archivos de estado local de Obsidian en `.gitignore`.

## 2026-09-09

- **#11** `2c34fff`: `GET api/cash-entries`. Lista las entradas de caja proyectadas a `CashEntryDTO`, sin campos de auditoría.
- **#10** `0f26f04`: `POST api/cash-entries/reversal/{id}`. Reversa aportes, retiros y gastos.
- **#9** `4d6b892`: `POST api/cash-entries/expense`. Gasto con al menos un counterparty.
- **#8** `fdd12e1`: `POST api/cash-entries/withdrawal`. Retiro. La ruta del controller pasa de `api/cash-entry` a `api/cash-entries` (modifica #6).
- **#7** `cc4a4d9`: el aporte deja de exigir counterparty (modifica #6).
- **#6** `9876ded`: `POST api/cash-entry/contribution`. Aporte de caja.

## 2026-08-31

- **#5** `edc1f58`: CRUD de clientes (GET por id, PUT, DELETE lógico) y enums del dominio.

## 2026-08-27

- **#4** `ea976e9`: `GET api/customers`.

## 2026-08-26

- **#3** `f3b6d32`: `POST api/customers`.
- **#1** `dea6d9a`: conexión a PostgreSQL con EF Core y convención snake_case.

## 2026-08-25

- `62248a8`, `c033b53`: commits iniciales con la plantilla de ASP.NET Core Web API.

> [!note] Tarea #2
> El esquema inicial (`db/schema.sql`) se construyó a lo largo de varios commits entre agosto y septiembre; no tiene un commit único.
