# Endpoints

Los mensajes de respuesta van en inglés. Las secciones marcadas *propuesta* aún no existen y sus rutas pueden cambiar.

Las fechas de hecho (`valueDate`, `loanDate`) viajan en JSON como `YYYY-MM-DD`, sin hora.

## Clientes: `api/customers`

Todas las respuestas devuelven `CustomerDTO`, sin campos de auditoría.

| Método | Ruta | Qué hace | Estado |
|---|---|---|---|
| GET | `api/customers` | Lista clientes, más recientes primero | Implementado |
| GET | `api/customers/{id}` | Cliente por id | Implementado |
| POST | `api/customers` | Crea cliente | Implementado |
| PUT | `api/customers` | Actualiza cliente (id en el cuerpo) | Implementado |
| DELETE | `api/customers/{id}` | Borrado lógico | Implementado |

## Caja: `api/cash-entries`

Los POST responden `201` con el id de la entrada creada: `{ "id": "..." }` (#111). Los montos deben ser mayores que cero y tener como máximo 2 decimales; si no, `400` (#112).

| Método | Ruta | Cuerpo | Qué hace | Estado |
|---|---|---|---|---|
| GET | `api/cash-entries` | — | Lista entradas, más recientes primero. Sin `created_by`, `updated_by` ni `updated_date` | Implementado |
| POST | `api/cash-entries/contribution` | `CashEntryContributionDTO` | Aporte (+) | Implementado |
| POST | `api/cash-entries/withdrawal` | `CashEntryWithdrawalDTO` | Retiro (−), si hay efectivo suficiente | Implementado |
| POST | `api/cash-entries/expense` | `CashEntryExpenseDTO` | Gasto (−), al menos un counterparty, si hay efectivo suficiente | Implementado |
| POST | `api/cash-entries/reversal/{id}` | — | Reversa un aporte, retiro o gasto | Implementado. `409` si la entrada ya estaba reversada |

## Préstamos: `api/loans`

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/loans` | Crea el préstamo con su desembolso (asiento y salida de caja). Responde `201` con `{ "id": "..." }` | Implementado (#39) |
| GET | `api/loans` | Lista los préstamos, más recientes primero, con el nombre del cliente y su saldo (capital, interés y total) | Implementado (#40) |
| GET | `api/loans/{id}` | Préstamo con saldo, asientos en orden de fecha y pago sugerido si tiene plazo (D-049) | Implementado (#41) |
| POST | `api/loans/{id}/payments` | Pago: primero todo el interés pendiente y luego capital, con la fila del préstamo bloqueada y entrada de caja. Rechaza pagos mayores que la deuda total (D-044), futuros o anteriores al préstamo. Exige `idempotencyKey`: la misma clave con el mismo pago devuelve el mismo id, y con otro pago da `422`. Responde `201` con el id del asiento | Implementado (#42, #43) |
| POST | `api/loans/{id}/forgiveness` | Condona interés, sin movimiento de caja y nunca más que el interés pendiente. Responde `201` con el id del asiento | Implementado (#44) |
| POST | `api/loans/entries/{id}/reversal` | Reversa un pago o una condonación con los montos invertidos y la misma fecha; si es un pago, reversa también su entrada de caja, apuntando al asiento nuevo (D-018). `409` si ya estaba reversado. Responde `201` con el id de la reversión | Implementado (#45) |
| POST | `api/loans/{id}/write-off` | Marca un préstamo activo como incobrable (D-057). Sigue aceptando pagos. Responde `204` | Implementado (#47) |
| DELETE | `api/loans/{id}` | Borra un préstamo cuyo único asiento es el desembolso: reversa el desembolso y su salida de caja (el dinero vuelve) y marca el préstamo `DELETED` (D-056). Responde `204` | Implementado (#48) |

## Congelamientos

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/loans/{id}/freezes` | Abre un congelamiento sobre un préstamo activo, con `startDate` no futura ni anterior al préstamo (D-060). `409` si ya tiene uno abierto. Responde `201` con `{ "id": "..." }` | Implementado (#50) |
| POST | `api/freezes/{id}/close` | Cierra un congelamiento escribiendo `endDate`, que no puede ser futura ni anterior al inicio. `409` si ya estaba cerrado. Responde `204` | Implementado (#51) |
| GET | `api/loans/{id}/freezes` | Lista los congelamientos del préstamo, el más reciente primero, sin campos de auditoría salvo `authorizedBy`. `endDate` nulo = abierto | Implementado (#52) |

## Jobs *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/jobs/interest-charges/{period}` | Re-corre el corte de un período | #59 |

## Autenticación y usuarios *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/auth/login` | Con usuario y contraseña de un usuario `ACTIVE`, responde `200` con el access (JWT de 15 minutos), el refresh (7 días) y sus vencimientos. `401` igual para usuario desconocido, contraseña mala o usuario inactivo (D-062) | Implementado (#63) |
| POST | `api/auth/refresh` | Rota el refresh | #64 |
| POST | `api/auth/logout` | Revoca el refresh | #65 |
| GET, POST, PUT, DELETE | `api/users` | CRUD de usuarios, solo ADMIN | #68 |

## Reportes *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| GET | `api/reports/cash-balance` | Saldo de caja | #76 |
| GET | `api/reports/accrued-interest` | Interés devengado | #77 |
| GET | `api/reports/collected-interest` | Interés cobrado | #78 |
| GET | `api/reports/pending` | Interés y capital pendientes | #79 |
| GET | `api/reports/profit` | Ganancia real | #80 |
