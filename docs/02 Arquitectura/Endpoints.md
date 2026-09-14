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

Los POST responden `201` con el id de la entrada creada: `{ "id": "..." }` (#111).

| Método | Ruta | Cuerpo | Qué hace | Estado |
|---|---|---|---|---|
| GET | `api/cash-entries` | — | Lista entradas, más recientes primero. Sin `created_by`, `updated_by` ni `updated_date` | Implementado |
| POST | `api/cash-entries/contribution` | `CashEntryContributionDTO` | Aporte (+) | Implementado |
| POST | `api/cash-entries/withdrawal` | `CashEntryWithdrawalDTO` | Retiro (−), si hay efectivo suficiente | Implementado |
| POST | `api/cash-entries/expense` | `CashEntryExpenseDTO` | Gasto (−), al menos un counterparty, si hay efectivo suficiente | Implementado |
| POST | `api/cash-entries/reversal/{id}` | — | Reversa un aporte, retiro o gasto | Implementado. `409` si la entrada ya estaba reversada |

## Préstamos: `api/loans` *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/loans` | Crea el préstamo con su desembolso (asiento y salida de caja). Responde `201` con `{ "id": "..." }` | Implementado (#39) |
| GET | `api/loans` | Lista los préstamos, más recientes primero, con el nombre del cliente y su saldo (capital, interés y total) | Implementado (#40) |
| GET | `api/loans/{id}` | Préstamo con saldo, asientos en orden de fecha y pago sugerido si tiene plazo (D-049) | Implementado (#41) |
| POST | `api/loans/{id}/payments` | Pago: primero todo el interés pendiente y luego capital, con la fila del préstamo bloqueada y entrada de caja. Rechaza pagos mayores que la deuda total (D-044), futuros o anteriores al préstamo. Exige `idempotencyKey`: la misma clave con el mismo pago devuelve el mismo id, y con otro pago da `422`. Responde `201` con el id del asiento | Implementado (#42, #43) |
| POST | `api/loans/{id}/forgiveness` | Condonación de interés | #44 |
| POST | `api/loans/entries/{id}/reversal` | Reversión de un asiento y de su caja | #45 |

## Congelamientos *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/loans/{id}/freezes` | Abre un congelamiento | #50 |
| POST | `api/freezes/{id}/close` | Cierra un congelamiento | #51 |
| GET | `api/loans/{id}/freezes` | Lista congelamientos del préstamo | #52 |

## Jobs *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/jobs/interest-charges/{period}` | Re-corre el corte de un período | #59 |

## Autenticación y usuarios *(propuesta)*

| Método | Ruta | Qué hace | Tarea |
|---|---|---|---|
| POST | `api/auth/login` | Emite access y refresh | #63 |
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
