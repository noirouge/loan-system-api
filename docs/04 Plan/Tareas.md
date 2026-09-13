# Tareas

**Estados:** `Pendiente` · `En progreso` · `Completada` · `Bloqueada (motivo)` · `Cancelada`

**Reglas:**

- Los números nunca se reutilizan ni se renumeran, y las tareas nunca se borran.
- Si una tarea cambia lo que entregó otra, su descripción dice *modifica #N*. Cuando la nueva se completa, en la fila de **#N** se marca `¿Modificada? = Sí` y se agrega el número de la nueva en `Modificada por`.
- Una tarea `Cancelada` lleva en `Modificada por` las tareas que la reemplazan.
- Una tarea `Bloqueada` no se empieza. El motivo está en [[Preguntas abiertas]].
- Las tareas están agrupadas por fase, así que dentro de una tabla los números no siempre van seguidos.
- El orden de las fases y sus dependencias están en [[Plan del proyecto]].
- **La siguiente tarea nueva es la #110.**

## Fase 0: Historial y documentación

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 1 | Configurar conexión a PostgreSQL | Completada | No | N/A |
| 2 | Crear esquema inicial de la base (`db/schema.sql`) | Completada | Sí | 13, 14, 18 |
| 3 | POST crear cliente | Completada | Sí | 25 |
| 4 | GET listar clientes | Completada | Sí | 24, 25 |
| 5 | CRUD de clientes (GET por id, PUT, DELETE lógico) y enums | Completada | Sí | 25 |
| 6 | POST aporte de caja | Completada | Sí | 7, 8, 16, 23 |
| 7 | Quitar la validación de counterparty del aporte (modifica #6) | Completada | No | N/A |
| 8 | POST retiro de caja; ruta del controller a `api/cash-entries` (modifica #6) | Completada | Sí | 16 |
| 9 | POST gasto de caja | Completada | Sí | 16 |
| 10 | POST reversión de caja | Completada | Sí | 16, 18, 19 |
| 11 | GET listar entradas de caja | Completada | Sí | 16 |
| 12 | Crear vault de documentación, plan y tareas | Completada | No | N/A |

## Pruebas: infraestructura y red de seguridad

Recomendado antes de las correcciones de la Fase 1 que cambian comportamiento. Diseño en [[Plan del proyecto]].

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 82 | Proyecto `LoanSystemAPI.IntegrationTests` con xUnit | Bloqueada (P-05) | No | N/A |
| 83 | Base PostgreSQL de prueba creada con `db/schema.sql` | Bloqueada (P-05, P-13) | No | N/A |
| 84 | `WebApplicationFactory<Program>` apuntando a la base de prueba; `public partial class Program` | Bloqueada (P-05) | No | N/A |
| 85 | Aislamiento entre pruebas: `TRUNCATE ... CASCADE`, admin semilla y colección de xUnit sin paralelismo | Bloqueada (P-05) | No | N/A |
| 86 | Reloj controlable: `TimeProvider` registrado en la app y uno falso para las pruebas | Bloqueada (P-05) | No | N/A |
| 87 | Pruebas de integración de clientes: CRUD y borrado lógico oculto por `HasQueryFilter` | Bloqueada (P-05) | No | N/A |
| 88 | Pruebas de integración de caja: aporte, retiro y gasto con su signo, validación de counterparty, listado sin campos de auditoría | Bloqueada (P-05) | No | N/A |
| 89 | Pruebas de integración de la reversión de caja: tipos permitidos, reversión repetida rechazada, saldo que se anula por signo | Bloqueada (P-05) | No | N/A |

## Fase 1: Correcciones

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 13 | Corregir índice `ux_loan_entries_loan_id_and_period` a `entry_type = 2` (modifica #2) | Completada | No | N/A |
| 14 | `users.role` en SQL: `DEFAULT 1`, admin semilla con `role = 2`, comentario corregido (modifica #2) | Completada | No | N/A |
| 15 | Actualizar el rol del admin en la base local: `UPDATE users SET role = 2 WHERE username = 'admin'` | Pendiente | No | N/A |
| 16 | Migrar `value_date`, `period` y `loan_date` a `DateOnly` en entidades y DTOs (modifica #6, #8, #9, #10, #11) | Completada | No | N/A |
| 17 | Helper de fecha "hoy" en hora local dominicana | Completada | No | N/A |
| 18 | Quitar `updated_by` y `updated_date` de `cash_entries`: SQL, entidad y reversión (modifica #2, #10) | Completada | No | N/A |
| 19 | La reversión de caja hereda el `value_date` del original (modifica #10) | Completada | No | N/A |
| 20 | Reversión de caja: capturar `23505` en vez de consultar antes (modifica #10) | Pendiente | No | N/A |
| 21 | Servicio de efectivo disponible (`SUM(amount)` de caja) | Completada | No | N/A |
| 22 | Validar efectivo disponible en retiro y gasto (modifica #8, #9) | Pendiente | No | N/A |
| 23 | Validar monto mayor que cero en el aporte (modifica #6) | Completada | No | N/A |
| 24 | Corregir el `GroupBy(CreatedDate)` de GET clientes, que devuelve grupos en vez de clientes (modifica #4) | Completada | No | N/A |
| 25 | Proyectar GET clientes a DTO sin campos de auditoría (modifica #4) | Completada | No | N/A |
| 26 | Corregir `ILogger<CustomersController>` en `LoansController` | Completada | No | N/A |
| 27 | `Loan.CreatedBy` de `Guid?` a `Guid` | Completada | No | N/A |
| 28 | `User.CreatedBy` de `Guid` a `Guid?` | Completada | No | N/A |
| 29 | Borrar `Enums/EntryStatus.cs`, que está vacío | Bloqueada (P-03) | No | N/A |
| 81 | Recrear en la base local el índice `ux_loan_entries_loan_id_and_period` con `entry_type = 2` | Pendiente | No | N/A |
| 90 | Pruebas de efectivo disponible: retiro y gasto rechazados sin fondos (#21, #22) | Bloqueada (P-05) | No | N/A |
| 91 | Pruebas de fechas: `value_date` heredado en la reversión y "hoy" dominicano cerca de la medianoche UTC (#16, #17, #19) | Bloqueada (P-05) | No | N/A |
| 92 | Prueba de dos reversiones simultáneas de la misma entrada: una gana y la otra recibe un error controlado, no un 500 (#20) | Bloqueada (P-05) | No | N/A |

## Fase 2: Infraestructura

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 30 | Servicio de usuario actual detrás de una interfaz; hoy devuelve `AdminId` | Pendiente | No | N/A |
| 31 | Reemplazar `_adminId` de los controllers por el servicio de usuario actual (modifica #3, #5, #6, #8, #9, #10) | Pendiente | No | N/A |
| 32 | Tablas `refresh_tokens`, `audit_logs` y `job_runs` con sus índices en `schema.sql` (modifica #2) | Pendiente | No | N/A |
| 33 | Enums `AuditAction` y `JobRunStatus` | Completada | No | N/A |
| 34 | Entidades `RefreshToken`, `AuditLog` y `JobRun`, registradas en `AppDbContext` | Pendiente | No | N/A |
| 35 | Completar las entidades `LoanEntry` y `Freeze` | Pendiente | No | N/A |
| 36 | Registrar `Loan`, `LoanEntry` y `Freeze` en `AppDbContext` | Pendiente | No | N/A |
| 37 | Helper de redondeo a 2 decimales | Bloqueada (P-01) | No | N/A |
| 93 | Pruebas de restricciones de la base: un cargo por período, un congelamiento abierto, una reversión por asiento; `schema.sql` reejecutable | Bloqueada (P-05) | No | N/A |

## Fase 3: Préstamos

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 38 | Proyección de saldo del préstamo (`SUM(principal)`, `SUM(interest)`) | Pendiente | No | N/A |
| 39 | POST crear préstamo con desembolso: préstamo + asiento + caja en una transacción, validando efectivo | Pendiente | No | N/A |
| 40 | GET listar préstamos con saldo | Pendiente | No | N/A |
| 41 | GET préstamo por id con saldo y asientos | Pendiente | No | N/A |
| 42 | POST pago con cascada (interés → capital), `SELECT ... FOR UPDATE` y entrada de caja | Pendiente | No | N/A |
| 43 | Idempotencia del pago: `idempotency_key`, captura de `23505`, `422` si cambia el cuerpo | Pendiente | No | N/A |
| 44 | POST condonación de interés | Pendiente | No | N/A |
| 45 | POST reversión de asiento de préstamo y de su caja (`loan_entry_id` = asiento nuevo) | Pendiente | No | N/A |
| 46 | Cierre de préstamo (`CLOSED`) | Bloqueada (P-06) | No | N/A |
| 47 | Préstamo incobrable (`WRITTENOFF`) | Bloqueada (P-07) | No | N/A |
| 48 | Borrado lógico de préstamo (`DELETED`) | Bloqueada (P-08) | No | N/A |
| 49 | Proyecto de pruebas y prueba del ejemplo canónico (mes 3 = 84) | Cancelada | Sí | 82, 94 |
| 94 | Prueba del ejemplo canónico de punta a punta: mes 3 = 84, cierre con capital 600 e interés 0 | Bloqueada (P-05) | No | N/A |
| 95 | Pruebas de la cascada: pago menor, igual y mayor que el interés pendiente; el interés nunca queda negativo | Bloqueada (P-05) | No | N/A |
| 96 | Pruebas del desembolso: préstamo, asiento y caja en una transacción; rechazo sin efectivo | Bloqueada (P-05) | No | N/A |
| 97 | Pruebas de idempotencia del pago: misma clave y cuerpo crean un solo pago; misma clave con otro cuerpo da `422` | Bloqueada (P-05) | No | N/A |
| 98 | Prueba de dos pagos simultáneos al mismo préstamo (`SELECT ... FOR UPDATE`) | Bloqueada (P-05) | No | N/A |
| 99 | Pruebas de condonación y de reversión de asiento con su caja (`loan_entry_id` = asiento nuevo) | Bloqueada (P-05) | No | N/A |

## Fase 4: Congelamientos

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 50 | POST abrir congelamiento (`23505` → `409` si ya hay uno abierto) | Pendiente | No | N/A |
| 51 | POST cerrar congelamiento (escribe `end_date`) | Pendiente | No | N/A |
| 52 | GET congelamientos de un préstamo | Pendiente | No | N/A |
| 100 | Pruebas de congelamientos: segundo abierto rechazado con `409` y cierre con `end_date` | Bloqueada (P-05) | No | N/A |

## Fase 5: Job del corte mensual

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 53 | Cálculo del cargo: tasa × (capital + interés pendiente) | Bloqueada (N-01) | No | N/A |
| 54 | Inserción idempotente con `ON CONFLICT DO NOTHING` | Bloqueada (N-01) | No | N/A |
| 55 | `BackgroundService` con advisory lock de Postgres | Pendiente | No | N/A |
| 56 | Registro en `job_runs`: fila de arranque en transacción propia y resultado final | Pendiente | No | N/A |
| 57 | Detección de corridas huérfanas | Pendiente | No | N/A |
| 58 | Recuperación de períodos perdidos al arrancar | Bloqueada (N-01) | No | N/A |
| 59 | Endpoint para re-correr un período manualmente | Bloqueada (N-01) | No | N/A |
| 101 | Pruebas del job de cargos: re-ejecución sin duplicar, salto de congelados, recuperación de períodos perdidos | Bloqueada (P-05, N-01) | No | N/A |
| 102 | Pruebas de `job_runs`: `SUCCESS`, `PARTIAL` y `FAILED`, corridas huérfanas, un solo éxito por período, advisory lock | Bloqueada (P-05) | No | N/A |

## Fase 6: Autenticación

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 60 | Instalar `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x | Bloqueada (P-04) | No | N/A |
| 61 | Inicializar user-secrets y guardar la clave del JWT | Pendiente | No | N/A |
| 62 | Reemplazar `admin123` del admin semilla por su hash (modifica #2) | Pendiente | No | N/A |
| 63 | POST `api/auth/login`: access de 15 min y refresh de 7 días hasheado | Bloqueada (P-04) | No | N/A |
| 64 | POST `api/auth/refresh` con rotación y detección de reúso | Bloqueada (P-04) | No | N/A |
| 65 | POST `api/auth/logout` | Bloqueada (P-04) | No | N/A |
| 66 | El servicio de usuario actual lee el claim del JWT (modifica #30) | Bloqueada (P-04) | No | N/A |
| 67 | `[Authorize]` y roles en los controllers | Bloqueada (P-04) | No | N/A |
| 68 | CRUD de usuarios (solo ADMIN) | Pendiente | No | N/A |
| 103 | Helper de pruebas para autenticarse y obtener un JWT | Bloqueada (P-04, P-05) | No | N/A |
| 104 | Pruebas de sesión: login, refresh con rotación, reúso que revoca todas las sesiones, logout | Bloqueada (P-04, P-05) | No | N/A |
| 105 | Pruebas de autorización por rol: WORKER contra ADMIN | Bloqueada (P-04, P-05) | No | N/A |

## Fase 7: Auditoría

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 69 | Interceptor de `SaveChanges` con el `ChangeTracker` | Bloqueada (P-02) | No | N/A |
| 70 | Formato de `changes`: `CREATE` completo, `UPDATE` solo cambiados, borrado lógico como `DELETE` | Bloqueada (P-02) | No | N/A |
| 71 | Escritura de la bitácora después del commit, en transacción aparte | Bloqueada (P-02) | No | N/A |
| 72 | Exclusión de campos sensibles y tablas de infraestructura | Bloqueada (P-02) | No | N/A |
| 73 | Registro manual de `LOGIN`, `LOGINFAILED` y `LOGOUT` | Bloqueada (P-04) | No | N/A |
| 106 | Pruebas de auditoría: `CREATE` completo, `UPDATE` solo cambiados, borrado lógico como `DELETE`, campos sensibles excluidos | Bloqueada (P-02, P-05) | No | N/A |
| 107 | Prueba de que un fallo al escribir la bitácora no revierte el cambio de negocio | Bloqueada (P-02, P-05) | No | N/A |

## Fase 8: Mantenimiento

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 74 | Job diario: borrar refresh tokens vencidos | Pendiente | No | N/A |
| 75 | Job diario: purgar `audit_logs` de más de un año | Pendiente | No | N/A |
| 108 | Pruebas de mantenimiento: borrado de tokens vencidos y purga de la bitácora de más de un año | Bloqueada (P-05) | No | N/A |

## Fase 9: Reportes

| # | Tarea | Estado | ¿Modificada? | Modificada por |
|---:|---|---|---|---:|
| 76 | Reporte de saldo de caja | Pendiente | No | N/A |
| 77 | Reporte de interés devengado | Bloqueada (P-12) | No | N/A |
| 78 | Reporte de interés cobrado | Bloqueada (P-12) | No | N/A |
| 79 | Reporte de interés y capital pendientes | Pendiente | No | N/A |
| 80 | Reporte de ganancia real | Bloqueada (P-12) | No | N/A |
| 109 | Pruebas de reportes sobre el ejemplo canónico, con pagos y gastos reversados | Bloqueada (P-05, P-12) | No | N/A |
