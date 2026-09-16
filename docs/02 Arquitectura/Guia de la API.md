# Guía de la API

Referencia para consumir la API desde el frontend. El detalle de reglas de negocio está en [[Modelo de negocio]] y la lista corta en [[Endpoints]].

## Convenciones

| Tema | Regla |
|---|---|
| Base | `https://<host>/api`. JSON en `camelCase` |
| Autenticación | `Authorization: Bearer <accessToken>` en todo, salvo `api/auth/*` |
| Fechas de hecho | `YYYY-MM-DD` (`valueDate`, `loanDate`, `startDate`...). Nunca con hora; con hora da `400` |
| Fechas de sistema | ISO 8601 en UTC (`createdDate`, `accessTokenExpiresAt`) |
| Montos | Número con máximo 2 decimales. Se envían **en positivo**; la API aplica el signo |
| Tasa | Fracción: `0.10` = 10 %. Mayor que 0, máximo 1, hasta 4 decimales |
| Enums | Viajan como número (tabla al final) |
| Ids | GUID |
| POST que crea | `201` con `{ "id": "..." }` (clientes y usuarios devuelven el objeto completo) |
| Errores | `{ "message": "..." }`. Un JSON mal formado o un campo requerido faltante da `400` con `ProblemDetails` (`errors`) |

| Código | Significado |
|---|---|
| `400` | Validación o regla de negocio |
| `401` | Sin token, token vencido o inválido, credenciales malas |
| `403` | Rol insuficiente (solo `api/users`) |
| `404` | No existe (o está borrado) |
| `409` | Conflicto: ya reversado, congelamiento ya abierto, usuario repetido, job corriendo |
| `422` | Clave de idempotencia reutilizada con otro pago |

## Autenticación: `api/auth`

El access dura 15 minutos; el refresh, 7 días. Cuando el access vence (`401`), llama a `refresh` y reintenta. Cada refresh **devuelve un refresh nuevo**: guarda siempre el último. Reusar uno viejo cierra todas las sesiones del usuario.

| Método | Ruta | Cuerpo | Respuesta |
|---|---|---|---|
| POST | `/auth/login` | `{ username, password }` | `200` tokens · `401` |
| POST | `/auth/refresh` | `{ refreshToken }` | `200` tokens · `401` |
| POST | `/auth/logout` | `{ refreshToken }` | `204` siempre |

```json
{ "accessToken": "eyJ...", "accessTokenExpiresAt": "2026-09-15T16:15:00Z",
  "refreshToken": "q3X...", "refreshTokenExpiresAt": "2026-09-22T16:00:00Z" }
```

El JWT trae `sub` (id del usuario), `unique_name` y `role` (`ADMIN` o `WORKER`).

## Usuarios: `api/users`

`GET /users/options` lo puede usar cualquier usuario: devuelve los usuarios activos `[{ id, name, lastname, username }]` para elegir `counterpartyUserId` en caja. Todo lo demás es solo ADMIN.

| Método | Ruta | Cuerpo | Respuesta |
|---|---|---|---|
| GET | `/users` | — | `200` `UserDTO[]` (sin borrados) |
| GET | `/users/{id}` | — | `200` `UserDTO` · `404` |
| POST | `/users` | `{ name, lastname, username, password, role }` | `201` `UserDTO` · `409` usuario repetido |
| PUT | `/users` | `{ id, name, lastname, role, status, password? }` | `200` `UserDTO` |
| DELETE | `/users/{id}` | — | `204` |

`UserDTO`: `{ id, name, lastname, username, role, status }`. Contraseña mínima de 8; textos hasta 50. `status` en PUT: `1` o `2`. No puedes cambiar tu propio rol o estado ni borrarte. Cambiar rol, estado o contraseña cierra las sesiones de ese usuario.

## Clientes: `api/customers`

| Método | Ruta | Cuerpo | Respuesta |
|---|---|---|---|
| GET | `/customers` | — | `200` `CustomerDTO[]` |
| GET | `/customers/{id}` | — | `200` · `404` |
| POST | `/customers` | `{ fullname, code?, phone?, note? }` | `201` `CustomerDTO` |
| PUT | `/customers` | `{ id, fullname, code?, phone?, note? }` | `200` |
| DELETE | `/customers/{id}` | — | `204` (borrado lógico) |

`CustomerDTO`: `{ id, fullname, code, phone, note }`. `code` es la ficha.

## Caja: `api/cash-entries`

| Método | Ruta | Cuerpo | Notas |
|---|---|---|---|
| GET | `/cash-entries` | — | Más recientes primero |
| POST | `/cash-entries/contribution` | `{ amount, valueDate, counterpartyUserId, note? }` | Aporte |
| POST | `/cash-entries/withdrawal` | `{ amount, valueDate, counterpartyUserId, note? }` | `400` sin efectivo |
| POST | `/cash-entries/expense` | `{ amount, valueDate, counterpartyUserId?, counterparty?, note? }` | Al menos un counterparty. `400` sin efectivo |
| POST | `/cash-entries/reversal/{id}` | — | Solo aporte, retiro o gasto. `409` ya reversado. Un aporte exige efectivo |

`CashEntryDTO`: `{ id, entryType, amount, valueDate, note, status, counterpartyUserId, counterparty, loanEntryId, reversesEntryId, createdDate }`. `amount` con signo: positivo entra, negativo sale.

## Préstamos: `api/loans`

| Método | Ruta | Cuerpo | Notas |
|---|---|---|---|
| POST | `/loans` | `{ customerId, principal, interestRate, loanDate, paymentDay, term? }` | Desembolsa y saca el dinero de caja. `paymentDay` 1–28. `loanDate` no futura. `400` sin efectivo |
| GET | `/loans` | — | `LoanDTO[]` con saldo |
| GET | `/loans/{id}` | — | `LoanDetailDTO` |
| POST | `/loans/{id}/payments` | `{ amount, valueDate, idempotencyKey, note? }` | Cascada: interés y luego capital. No más que la deuda total |
| POST | `/loans/{id}/forgiveness` | `{ amount, valueDate, note? }` | Condona interés, sin caja. No más que el interés pendiente |
| POST | `/loans/entries/{entryId}/reversal` | — | Reversa pago, condonación o cargo de interés (este solo si su interés sigue pendiente completo). `409` ya reversado |
| POST | `/loans/{id}/write-off` | — | Incobrable. `204`. Solo desde `ACTIVE` |
| DELETE | `/loans/{id}` | — | Solo si su único asiento es el desembolso. Devuelve el dinero a caja. `204` |

`LoanDTO`: `{ id, customerId, customerFullname, principal, term, interestRate, loanDate, paymentDay, status, balance: { principal, interest, total } }`.

`LoanDetailDTO` agrega:
- `suggestedPayment`: `{ principal, interest, total }`, o `null` si no tiene plazo, no está activo o no debe nada.
- `entries`: `[{ id, entryType, principal, interest, period, valueDate, status, reversesEntryId, note, createdDate }]`. En asientos, positivo sube la deuda y negativo la baja.

**Pagos e idempotencia:** genera un GUID nuevo por cada pago y reenvía el mismo si reintentas (timeout, doble clic). Mismo GUID y mismo pago → `201` con el mismo id. Mismo GUID con otro monto, fecha o nota → `422`.

Estados: un pago que salda la deuda cierra el préstamo (`CLOSED`); una reversión que le devuelve saldo lo reabre.

## Congelamientos

| Método | Ruta | Cuerpo | Notas |
|---|---|---|---|
| POST | `/loans/{loanId}/freezes` | `{ startDate, reason? }` | Préstamo activo. `409` si ya hay uno abierto |
| POST | `/freezes/{id}/close` | `{ endDate }` | `204`. `409` ya cerrado |
| GET | `/loans/{loanId}/freezes` | — | `[{ id, loanId, startDate, endDate, reason, authorizedBy, createdDate }]`. `endDate: null` = abierto |

Mientras un congelamiento cubre el día 1, ese mes no se cobra interés.

## Reportes: `api/reports`

Rangos `from`/`to` opcionales e inclusivos (`YYYY-MM-DD`); `from > to` da `400`.

| Ruta | Respuesta |
|---|---|
| `/reports/cash-balance?date=` | `{ date, contributions, withdrawals, disbursements, payments, expenses, balance }` |
| `/reports/accrued-interest?from=&to=` | `{ from, to, amount }` |
| `/reports/collected-interest?from=&to=` | `{ from, to, amount }` |
| `/reports/pending?date=` | `{ date, principal, interest, total, writtenOffPrincipal, writtenOffInterest }` |
| `/reports/profit?from=&to=` | `{ from, to, collectedInterest, expenses, profit }` |
| `/reports/monthly-collection?month=YYYY-MM&paymentDay=` | Hoja de cobro (abajo) |

**Hoja de cobro:** `{ month, paymentDay, totalInterestDue, totalPrincipalDue, totalDue, totalPaid, totalRemaining, loans: [...] }`. Cada fila:

| Campo | Significado |
|---|---|
| `loanId`, `customerId`, `customerName`, `customerCode` | Préstamo y cliente |
| `loanDate`, `paymentDay`, `installment` | `installment`: `2-5` (pago 2 de 5) o `0-1` sin plazo |
| `originalPrincipal`, `interestRate` | Datos del préstamo |
| `owedAtCut` | Lo que debía antes del cargo del mes |
| `interestDue`, `principalDue`, `totalDue` | Cuota del mes |
| `paid`, `paidInterest`, `paidPrincipal` | Abonos con fecha en el mes |
| `remaining` | Deuda al cierre del mes |
| `status`, `frozen` | Estado actual; congelado ese mes |

## Jobs

| Método | Ruta | Notas |
|---|---|---|
| POST | `/jobs/interest-charges/{YYYY-MM-01}` | Corre el corte de un mes ya iniciado. Nunca duplica. `200` `{ id, jobName, period, status, processed, skipped, failed, errorMessage }` · `409` corriendo |

El corte también corre solo al arrancar y cada día.

## Enums

| Enum | Valores |
|---|---|
| `role` | `1` WORKER, `2` ADMIN |
| Usuario `status` | `1` ACTIVE, `2` INACTIVE, `3` DELETED |
| Préstamo `status` | `1` ACTIVE, `2` CLOSED, `3` WRITTENOFF (incobrable), `4` DELETED |
| Caja `entryType` | `1` CONTRIBUTION, `2` WITHDRAWAL, `3` DISBURSEMENT, `4` PAYMENT, `5` EXPENSE, `6` REVERSAL |
| Caja `status` | `1` APPLIED, `2` REVERSED |
| Asiento `entryType` | `1` DISBURSEMENT, `2` INTERESTCHARGE, `3` PAYMENT, `4` FORGIVENESS, `5` REVERSAL |
| Asiento `status` | `1` APPLIED, `2` REVERSED |
| Job `status` | `1` RUNNING, `2` SUCCESS, `3` FAILED, `4` PARTIAL |

## Swagger

En desarrollo, `/swagger` muestra todos los esquemas. Usa **Authorize** con el `accessToken` del login.
