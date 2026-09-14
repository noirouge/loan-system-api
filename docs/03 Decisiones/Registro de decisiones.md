# Registro de decisiones

Cada decisión tiene un id que no se reutiliza. Si una decisión cambia, se agrega una nueva que diga a cuál reemplaza; la vieja no se borra.

## 2026-09-09

| ID | Decisión | Motivo o consecuencia |
|---|---|---|
| D-001 | Ruta del controller de caja en plural: `api/cash-entries` | Consistente con `api/customers` y `api/loans` |
| D-002 | Un DTO por operación de entrada; `CashEntryDTO` es el de lectura | Cada operación valida campos distintos |
| D-003 | El aporte no exige counterparty | Decisión del usuario (commit `cc4a4d9`) |
| D-004 | Los montos de caja se reciben en positivo y el API aplica el signo | El cliente no decide el signo |
| D-005 | Gasto: al menos uno de `counterpartyUserId` o `counterparty`. `Guid.Empty` y texto en blanco cuentan como vacío | `Guid.Empty` pasaría la validación y fallaría contra la FK a `users` |
| D-006 | La reversión manual de caja solo acepta `CONTRIBUTION`, `WITHDRAWAL` y `EXPENSE` | `PAYMENT` y `DISBURSEMENT` se reversan desde el préstamo, para no desincronizar ledger y caja |
| D-007 | La reversión de caja no copia counterparty y lleva la nota `REVERSAL OF THE CASH ENTRY {id}` | Counterparty solo aplica a aportes, retiros y gastos; la trazabilidad va por `reverses_entry_id` |
| D-008 | El GET de caja no expone `created_by`, `updated_by` ni `updated_date`; sí expone `counterparty_user_id` | El rastro de auditoría es interno; sin el counterparty la lista no dice quién movió el dinero |

## 2026-09-13

| ID | Decisión | Motivo o consecuencia |
|---|---|---|
| D-009 | `LoanEntryType` se queda como en el código (`DISBURSEMENT=1` … `REVERSAL=5`), no como la especificación original (0–4) | Los enums empiezan en 1 en todo el proyecto. **Consecuencia:** el índice de cargos debe usar `entry_type = 2` (#13) |
| D-010 | `UserRole` se queda `WORKER=1`, `ADMIN=2` (la especificación decía lo contrario) | **Consecuencia:** corregir default y semilla en SQL (#14, #15) |
| D-011 | `value_date`, `period` y `loan_date` como `DateOnly`; "hoy" en hora local dominicana | Con `DateTime.UtcNow`, lo registrado después de las 8 p. m. caería al día siguiente |
| D-012 | UUID v4 (`Guid.NewGuid()`) en esta versión, no v7; el proyecto sigue en .NET 8 | `Guid.CreateVersion7()` requiere .NET 9 |
| D-013 | Toda reversión hereda el `value_date` del original | Una reversión fechada hoy movería el saldo de un mes ya reportado |
| D-014 | `LoanStatus.CLOSED` se queda (no `SETTLED`). `loans.payment_day` se queda. `cash_entries` pierde `updated_by` y `updated_date` | Quién hizo una reversión queda en el `created_by` de la reversión (#18) |
| D-015 | Los montos calculados se redondean a 2 decimales | Caso de empate pendiente: P-01 |
| D-016 | Retiro, gasto y desembolso no pueden dejar la caja en negativo | Si no hay dinero, no se puede retirar ni gastar |
| D-017 | Los cargos de interés no se reversan, se condonan. **Cambiada por D-071** | P-11 |
| D-018 | La caja de una reversión de préstamo apunta al asiento de reversión nuevo (`loan_entry_id = R1`) | `loan_entry_id` es único y el asiento original ya lo usa su propia caja |
| D-019 | `_adminId` de configuración era temporal hasta que existiera el login. **Reemplazada en #66**: el usuario actual sale del claim `sub` del JWT | Solo para pruebas |
| D-020 | Contraseñas con `PasswordHasher<T>`; el admin semilla pasa a hash | Viene en el framework, sin dependencias |
| D-021 | `token_hash` con SHA-256 | El token ya es aleatorio; no necesita un hash lento |
| D-022 | Refresh token de 7 días; clave del JWT en user-secrets | Decisión del usuario |
| D-023 | `replaced_by` con FK y `UNIQUE` | Un token no se reemplaza dos veces |
| D-024 | Reúso de refresh token → revocar todos los tokens vigentes del usuario | Si hubo robo, no se sabe qué otras sesiones están comprometidas |
| D-025 | Refresh concurrente: se acepta el falso positivo, sin ventana de gracia | Decisión del usuario |
| D-026 | `ip_address` como `VARCHAR(45)`; sin `X-Forwarded-For` por ahora | Decisión del usuario |
| D-027 | Auditoría: `CREATE` guarda el registro completo; `UPDATE` solo los campos cambiados con `old` y `new` | Decisión del usuario |
| D-028 | `LOGIN`, `LOGINFAILED` y `LOGOUT` se registran manualmente | No cambian entidades; el interceptor no los ve |
| D-029 | El borrado lógico se audita como `DELETE` | Registra la intención, no el mecanismo |
| D-030 | El cambio de negocio manda: la bitácora se escribe después del commit, en transacción aparte | Un fallo de auditoría no debe perder una operación real |
| D-031 | Campos excluidos de auditoría: `password_hash`, `token_hash`. Tablas excluidas: `audit_logs`, `refresh_tokens`, `job_runs` | Evita recursión e inundación de la bitácora |
| D-032 | `audit_logs` se purga pasado un año y los tokens vencidos se limpian; ambos como jobs con fila en `job_runs` | Decisión del usuario |
| D-033 | Job: `BackgroundService` dentro de la API, advisory lock, una fila de `job_runs` por período, huérfanas pasan a `FAILED`, `period` nullable, criterios de `SUCCESS`/`PARTIAL`/`FAILED` | Delegado por el usuario. Detalle en [[Job del corte mensual]] |
| D-034 | Enums nuevos con `: short` | Convención del proyecto |
| D-035 | No se instalan dependencias sin permiso del usuario | Control sobre el `.csproj` |
| D-036 | La documentación vive en `docs/` como vault de Obsidian; las tareas nunca se borran ni se renumeran | Cualquier agente puede continuar el proyecto leyendo solo el vault |
| D-037 | Una tarea, un commit, hecho por el agente e incluyendo el vault; el push lo hace el usuario. Los mensajes terminan con `(Task N)` | Un commit no puede citar su propio hash; `Task N` evita el enlace automático de GitHub a issues |
| D-038 | `docs/` va en `.gitignore` por ahora: el vault se mantiene local y no se commitea. **Reemplaza la parte de D-037 que incluía el vault en cada commit**; el resto de D-037 sigue vigente | Decisión del usuario. Se deshicieron los commits de las tareas #12 y #13, que no se habían publicado |
| D-039 | `docs/` sale de `.gitignore`. **Reemplaza a D-038**: vuelve a regir D-037 completo, incluido el vault en cada commit | Decisión del usuario |
| D-040 | El plan incluye pruebas de integración, contra PostgreSQL real y no contra una base simulada. Se cancela la #49 | Decisión del usuario. Índices parciales, `23505`, `FOR UPDATE` y advisory locks no se pueden probar con EF InMemory ni SQLite |
| D-041 | La tasa de interés se envía y se guarda como fracción: `0.10` es 10% (P-14) | Decisión del usuario. Riesgo: alguien escribe `10` queriendo decir 10%; la validación está en P-15 |
| D-042 | Pagar más que la cuota está permitido: después del interés pendiente, el excedente reduce capital. Si no se paga, el interés se capitaliza y el mes siguiente se calcula sobre el total (10% de 110 = 11) | Decisión del usuario, confirma la cascada y la capitalización de la especificación. Ejemplo de Fulanito en [[Modelo de negocio]] |
| D-043 | Redondeo a 2 decimales mirando solo el tercer decimal: si es mayor que 5 sube, si es 5 o menos se queda (1.266 → 1.27; 1.265 → 1.26; 1.2659 → 1.26). Siempre en C#, nunca con `ROUND` de Postgres | Regla del usuario (P-01); el caso 1.2659 lo decidió el agente por delegación |
| D-044 | Un pago mayor que toda la deuda (capital + interés pendiente) se rechaza con `400` indicando el máximo. El capital nunca queda negativo (P-10) | Decisión del usuario |
| D-045 | Al crear un préstamo: no se valida el estado del cliente, solo que exista y no esté borrado; `term` es opcional y, si se envía, mayor que 0; `loan_date` no puede ser futura; el desembolso en caja usa `loan_date` como `value_date`; la tasa debe ser mayor que 0 y como máximo 1; el capital mayor que 0 (P-15) | Decisiones del usuario; tasa y capital delegados al agente. El rango del día de pago sigue en P-15 |
| D-046 | Se elimina `CustomerStatus.INACTIVE`: un cliente está activo si tiene un préstamo activo, y eso se calcula, no se guarda (#110) | Delegado por el usuario (P-15) |
| D-047 | Pruebas de integración con xUnit y `Microsoft.AspNetCore.Mvc.Testing`, en un proyecto aparte, contra una segunda base `prestamos_test` en el PostgreSQL local, sin Docker (P-05, P-13) | Decisión del usuario. Las pruebas se niegan a correr si el nombre de la base no termina en `_test` |
| D-048 | El día de pago de un préstamo va de 1 a 28, para que exista en todos los meses (P-15) | Decisión del usuario |
| D-049 | Cuando el préstamo tiene plazo, la consulta muestra un pago sugerido del mes: capital original ÷ plazo (o el capital pendiente, si es menor) más el interés pendiente. Se calcula al consultar y no se guarda. Sin plazo no se muestra (P-17) | Decisión del usuario. Los asientos guardan cuánto sube o baja cada saldo, no cuánto se debe pagar |
| D-050 | El interés impago se queda en la columna `interest`: no se pasa a `principal` (P-16, opción A). La deuda total que se muestra es capital pendiente + interés pendiente. Los cargos de interés no generan movimiento de caja, porque no entra ni sale dinero | Decisión del usuario. Es la única forma de que el reporte de ganancia cuadre |
| D-051 | Los commits no llevan la línea `Co-Authored-By`. Configurado en `.claude/settings.local.json` (`attribution`), que no se versiona | Pedido del usuario |
| D-052 | Permiso para instalar `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x (P-04) | Decisión del usuario |
| D-053 | La bitácora de auditoría registra también los asientos de `loan_entries` y `cash_entries`: sus inserts y sus cambios de estado. Siguen fuera las tablas de infraestructura y los campos sensibles (D-031) (P-02) | Decisión del usuario |
| D-054 | Por ahora el corte de interés es global, el día 1 de cada mes. El job revisa una vez al día, y al arrancar, si falta el cargo de algún período ya vencido y lo genera, por si el día 1 falló (N-01) | Decisión del usuario, confirmada el 2026-09-14 («por ahora sí»). Cuándo le toca el primer cargo a un préstamo nuevo sigue en N-04 |
| D-055 | En los reportes filtrados por tipo no se excluye nada: cada reversión cuenta junto con el tipo del asiento que reversa, con su signo, así un pago y su reversión suman 0 (P-12) | Decisión del usuario, confirmada el 2026-09-14. Sin esto, la reversión de un `PAYMENT` quedaría fuera del filtro por ser de tipo `REVERSAL` |
| D-056 | Un préstamo solo se puede borrar si su único asiento es el desembolso: sin cargos, pagos ni condonaciones. Borrar es reversar el desembolso (asiento y caja, el dinero vuelve a la caja) y marcar el préstamo `DELETED`; no se borra ninguna fila (P-08) | Decisión del usuario («si todavía no tiene un cobro generado»), leída en su forma más estricta |
| D-057 | `WRITTENOFF` (incobrable): el cliente ya no va a pagar (mala paga, fallecimiento, etc.). Deja de generar interés, si llega a pagar el pago se acepta, y lo puede marcar cualquier usuario (P-07; D-069) | Significado dado por el usuario; los tres detalles los recomendó el agente y el usuario los confirmó el 2026-09-14 |
| D-058 | Un préstamo pasa solo a `CLOSED` cuando un pago o una condonación deja capital e interés en 0, y vuelve a `ACTIVE` si una reversión le devuelve saldo (P-06) | Recomendación del agente, confirmada por el usuario el 2026-09-14 |
| D-059 | Se borra `Enums/EntryStatus.cs`, que estaba vacío (P-03) | Decisión del usuario |
| D-060 | Congelamientos: solo se abren sobre un préstamo `ACTIVE`, con `start_date` no futura ni anterior a la fecha del préstamo; `authorized_by` es el usuario actual hasta que exista el login. Se cierran con `end_date` no futura ni anterior al inicio, y cerrar uno ya cerrado da `409` | Recomendación del agente, con las mismas reglas de fechas que los pagos (D-045). Un préstamo cerrado o incobrable no genera interés, así que no hay nada que congelar |
| D-061 | `JobRunner` no vuelve a correr un período que ya terminó en `SUCCESS`: no crea fila y devuelve `null`. Si el trabajo lanza una excepción, la corrida queda `FAILED` con el mensaje en `error_message`. El lock es por job, no global | Recomendación del agente. Un segundo `SUCCESS` del mismo período chocaría con `ux_job_runs_success`; cómo re-correr a mano un período ya exitoso se decide en #59 (bloqueada por N-04) |
| D-062 | JWT firmado con HS256, claims `sub`, `unique_name` y `role`, sin renombrarlos a los de .NET. Emisión y validación usan el `TimeProvider` y el vencimiento no tiene tolerancia. El login responde el mismo `401` para usuario desconocido, contraseña mala o usuario no activo, y el logout responde `204` exista o no el token | Recomendación del agente. Con el mismo reloj las pruebas pueden vencer tokens moviendo la hora; las respuestas iguales no revelan qué usuarios existen |
| D-063 | Todo endpoint exige usuario autenticado (política por defecto), salvo `api/auth`. Solo ADMIN: el CRUD de usuarios; lo demás también lo hace un WORKER (D-069) | Recomendación del agente con lo que ya estaba decidido; qué otras acciones son solo del dueño queda en P-18 |
| D-064 | Usuarios: contraseña de al menos 8 caracteres; nombre, apellido y usuario obligatorios, de hasta 50; el nombre de usuario no cambia y es único también frente a los borrados. `PUT` acepta estado `ACTIVE` o `INACTIVE`; se borra con `DELETE` (lógico). Nadie cambia su propio rol o estado ni se borra a sí mismo. Cambiar rol, estado o contraseña, o borrar, revoca los refresh tokens del usuario | Recomendación del agente. Evita que el único admin se deje fuera y que un usuario degradado siga refrescando su sesión |
| D-065 | `changes` de auditoría: las claves son los nombres de columna y cada campo es un objeto con `old` y/o `new`: `CREATE` lleva `new` de todas las columnas, `UPDATE` `old` y `new` solo de las que cambiaron, y `DELETE` `old` de todas. `entity_name` es el nombre de la tabla. Un `UPDATE` sin cambios reales no deja registro | Recomendación del agente. Un solo formato para las tres acciones, compatible con el ejemplo de D-027 |
| D-066 | Reportes: filtran por `value_date`. Los de saldo (caja, pendiente) aceptan `date` opcional, "hasta ese día inclusive"; los de flujo (interés devengado y cobrado, ganancia) aceptan `from` y `to` opcionales e inclusivos, y `from` posterior a `to` da `400`. Cada reversión cuenta con el tipo del asiento que reversa, uniéndola con su original. Por ahora los ve cualquier usuario autenticado (P-18) | Recomendación del agente sobre D-055. La reversión hereda el `value_date` del original (D-013), así el par cae siempre en el mismo rango |
| D-067 | El reporte de pendiente separa lo incobrable: `principal` e `interest` son de los préstamos que no están `WRITTENOFF`, y `writtenOffPrincipal` y `writtenOffInterest` de los incobrables. Los borrados no aparecen. Se usa el estado actual del préstamo, también con `date` en el pasado | Recomendación del agente. Sumar deuda que ya no se espera cobrar inflaría lo pendiente; el historial de estados no se guarda |
| D-068 | El primer cargo de interés de un préstamo es el día 1 del mes siguiente a su fecha, por el mes completo (N-04) | Decisión del usuario |
| D-069 | Lo único exclusivo de ADMIN es administrar usuarios; todo lo demás, incluido marcar incobrable, lo puede hacer un WORKER (P-18). Cambia D-057 y D-063 | Decisión del usuario |
| D-070 | Reversar un aporte exige efectivo disponible suficiente, con el mismo candado de caja que un retiro (P-09) | Decisión del usuario |
| D-071 | Un cargo de interés se puede reversar solo si fue un error del sistema; cualquier otro caso se condona. La API lo permite si el interés del cargo sigue pendiente completo (P-11) | Recomendación del agente aceptada por el usuario. Cambia D-017 |
| D-072 | Un pago con fecha anterior a un cargo ya generado no recalcula ese cargo (N-02) | Decisión del usuario |
| D-073 | Liquidar un préstamo es pagar el capital y el interés ya generado; no se cobra interés prorrateado del mes en curso (N-03). Es lo que ya hace el sistema | Decisión del usuario |
| D-074 | Job de cargos: cobra préstamos `ACTIVE` con fecha anterior al período y sin congelamiento que cubra el día del corte; calcula con los asientos anteriores al corte; crea el cargo con EF (para que se audite) bajo el bloqueo de la fila del préstamo, y el índice único evita duplicados; `created_by` es el ADMIN activo más antiguo. Un período ya exitoso se vuelve a correr como `interest-charges:yyyy-MM` sin `period` | Recomendación del agente. `ON CONFLICT` en SQL crudo saltaría la auditoría (D-053); el índice de `job_runs` solo admite un éxito por período |
| D-075 | Hoja de cobro mensual: aparecen los préstamos no borrados que debían algo antes del corte del día 1 del mes. Cuota = todo el interés pendiente (atrasado + cargo del mes) + capital: original ÷ plazo, sin pasar del capital pendiente, o todo el capital sin plazo ("0-1"). Abono = pagos con fecha en el mes; restan = deuda al cierre del mes. `status` es el actual y `frozen` el del mes | Acordado con el usuario |
