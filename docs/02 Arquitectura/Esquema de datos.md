# Esquema de datos

La definición completa está en `db/schema.sql`. Esta nota explica **para qué** es cada tabla y qué **no se puede romper**.

## Tablas de dominio

| Tabla | Para qué | Mutabilidad |
|---|---|---|
| `users` | Usuarios del sistema (dueño y empleado) | Editable, borrado lógico |
| `customers` | Deudores | Editable, borrado lógico |
| `loans` | Condiciones del préstamo: capital original, tasa, fecha, plazo informativo, día de pago | Editable, borrado lógico |
| `loan_entries` | Ledger del préstamo: cada movimiento de capital e interés → [[Ledger de prestamos]] | **Append-only** |
| `freezes` | Congelamientos del devengo | Se cierran con `end_date` |
| `cash_entries` | Ledger de caja: entradas y salidas de efectivo → [[Caja]] | **Append-only** |

**No hay columna de saldo en ninguna tabla.** El saldo siempre es la suma de los asientos.

## Tablas de infraestructura

No participan en el cálculo de saldos ni tienen FK hacia préstamos o asientos. No llevan `status` ni columnas de actualización. Pendientes de crear (#32).

| Tabla | Para qué | Nota |
|---|---|---|
| `refresh_tokens` | Sesiones revocables | [[Autenticacion]] |
| `audit_logs` | Bitácora de acciones, no de montos | [[Auditoria]] |
| `job_runs` | Registro de cada corrida de los jobs | [[Job del corte mensual]] |

## Invariantes

Ninguna tarea puede romper esto:

1. El interés pendiente nunca queda negativo. El excedente va a capital.
2. Un préstamo nunca tiene dos cargos de interés del mismo período.
3. Un asiento nunca se edita ni se borra. Única excepción: su `status` pasa a `REVERSED` cuando se reversa.
4. El saldo siempre es la suma de los asientos. No hay columna de saldo.
5. Un préstamo nunca tiene dos congelamientos abiertos.
6. Los montos son `decimal`, nunca `float`.
7. Un asiento se reversa una sola vez.
8. La caja no puede quedar en negativo por un desembolso, un retiro o un gasto (D-016).

## Restricciones críticas

| Restricción | Protege |
|---|---|
| `ux_loan_entries_loan_id_and_period ON loan_entries (loan_id, period) WHERE entry_type = 2` | Invariante 2. `2` es `INTERESTCHARGE` |
| `ux_freezes_if_open ON freezes (loan_id) WHERE end_date IS NULL AND status = 1` | Invariante 5 |
| `uq_loan_entries_idempotency_key` | Pagos duplicados |
| `uq_loan_entries_reverses_entry_id`, `uq_cash_entries_reverses` | Invariante 7 |
| `uq_cash_entries_loan_entry_id` | Un solo movimiento de caja por asiento de préstamo |
| `ux_job_runs_success ON job_runs (job_name, period) WHERE status = 2` | Un período se completa con éxito una sola vez (pendiente, #32) |

Las restricciones se verifican con pruebas de integración contra PostgreSQL real (#93).

## Enums

Valores tal como están en el código (D-009, D-010). **El código manda sobre la especificación original.**

| Enum | Valores |
|---|---|
| `LoanEntryType` | `DISBURSEMENT=1`, `INTERESTCHARGE=2`, `PAYMENT=3`, `FORGIVENESS=4`, `REVERSAL=5` |
| `LoanEntryStatus` | `APPLIED=1`, `REVERSED=2` |
| `LoanStatus` | `ACTIVE=1`, `CLOSED=2`, `WRITTENOFF=3` (incobrable), `DELETED=4` |
| `CashEntryType` | `CONTRIBUTION=1`, `WITHDRAWAL=2`, `DISBURSEMENT=3`, `PAYMENT=4`, `EXPENSE=5`, `REVERSAL=6` |
| `CashEntryStatus` | `APPLIED=1`, `REVERSED=2` |
| `CustomerStatus` | `ACTIVE=1`, `INACTIVE=2`, `DELETED=3` |
| `UserRole` | `WORKER=1`, `ADMIN=2` |
| `UserStatus` | `ACTIVE=1`, `INACTIVE=2`, `DELETED=3` |
| `FreezeStatus` | `ACTIVE=1`, `DELETED=2` |
| `EntryStatus` | Vacío, sin uso (P-03, #29) |

Infraestructura (#33):

| Enum | Valores |
|---|---|
| `AuditAction` | `CREATE=1`, `UPDATE=2`, `DELETE=3`, `LOGIN=4`, `LOGINFAILED=5`, `LOGOUT=6` |
| `JobRunStatus` | `RUNNING=1`, `SUCCESS=2`, `FAILED=3`, `PARTIAL=4` |

## Cambios de esquema pendientes

| Tarea | Cambio |
|---|---|
| #15 | En la base local: `UPDATE users SET role = 2 WHERE username = 'admin'` |
| #81 | En la base local: recrear `ux_loan_entries_loan_id_and_period` con `entry_type = 2`. `CREATE ... IF NOT EXISTS` no reemplaza el índice viejo, así que volver a correr el script no basta |
| #32 | Crear `refresh_tokens`, `audit_logs` y `job_runs` con sus índices |
| #62 | Reemplazar `'admin123'` del admin semilla por su hash |
