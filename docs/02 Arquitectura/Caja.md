# Caja

Tabla `cash_entries`. Registra cada entrada y salida de efectivo del negocio, la razón y quién movió el dinero.

## Signo de `amount`

Se mide desde la caja del negocio:

- **Positivo = entra dinero**: `CONTRIBUTION`, `PAYMENT`.
- **Negativo = sale dinero**: `WITHDRAWAL`, `DISBURSEMENT`, `EXPENSE`.
- `REVERSAL` lleva el signo contrario al de la entrada que anula.

El API recibe los montos **en positivo** y aplica el signo (D-004).

**Efectivo disponible = `SUM(amount)` de todas las entradas**, sin filtrar por status: las reversiones se anulan por signo.

Lo calcula `CashService.GetAvailableCashAsync()`. Toda operación que saca dinero llama antes a `CashService.LockCashAsync()` dentro de una transacción: es un advisory lock de Postgres que hace esperar a la segunda operación simultánea, para que dos retiros no vean el mismo saldo.

## Semántica de cada tipo

| Tipo | Signo | `counterparty_user_id` | `counterparty` | `loan_entry_id` | Origen |
|---|---|---|---|---|---|
| `CONTRIBUTION` (1) | + | Sí | — | — | Manual |
| `WITHDRAWAL` (2) | − | Sí | — | — | Manual |
| `DISBURSEMENT` (3) | − | — | — | Sí | Automático al desembolsar |
| `PAYMENT` (4) | + | — | — | Sí | Automático al recibir un pago |
| `EXPENSE` (5) | − | Opcional | Opcional | — | Manual. Al menos uno de los dos counterparty |
| `REVERSAL` (6) | Inverso | — | — | Sí, si viene de un préstamo | Manual o automático |

- `counterparty_user_id`: el usuario que aportó o retiró el dinero.
- `counterparty`: texto libre de en qué se gastó (máximo 100 caracteres).
- En pagos y desembolsos, `amount` lleva el **monto total** (capital + interés). El desglose vive en `loan_entries`.

## Validaciones

| Operación | Validación |
|---|---|
| Aporte | Monto > 0 |
| Retiro | Monto > 0 · **efectivo disponible suficiente** (pendiente, #22) |
| Gasto | Monto > 0 · al menos un counterparty (`Guid.Empty` y texto en blanco cuentan como vacío) · **efectivo disponible suficiente** (pendiente, #22) |
| Desembolso | **Efectivo disponible suficiente** (#39) |
| Reversión manual | Solo `CONTRIBUTION`, `WITHDRAWAL` o `EXPENSE` · que no esté reversada. ¿Reversar un aporte valida efectivo? P-09 |

## Reversión manual

`POST api/cash-entries/reversal/{id}`. Solo para aportes, retiros y gastos. Pagos y desembolsos se reversan desde el préstamo, para que el ledger del préstamo y la caja no se desincronicen (D-006).

1. Se crea la entrada `REVERSAL`, `APPLIED`, con `amount = -original.amount`, `reverses_entry_id = original.id` y el mismo `value_date` de la original (D-013).
2. La original pasa a `REVERSED`.
3. Las dos cosas van en un solo `SaveChangesAsync`, es decir, en una transacción.

Pendiente de corregir en el código actual: capturar `23505` en vez de consultar antes (#20).

## Reversión de un pago (ejemplo)

Un cliente paga 300 (100 de interés, 200 de capital) y el pago resulta ser un error:

| Tabla | Fila | Tipo | Montos | Referencias |
|---|---|---|---|---|
| `loan_entries` | P1 | `PAYMENT` | principal −200, interest −100 | — |
| `cash_entries` | C1 | `PAYMENT` | +300 | `loan_entry_id = P1` |
| `loan_entries` | R1 | `REVERSAL` | principal +200, interest +100 | `reverses_entry_id = P1` |
| `cash_entries` | C2 | `REVERSAL` | −300 | `loan_entry_id = R1`, `reverses_entry_id = C1` |

- C2 apunta a **R1**, no a P1 (D-018). P1 ya lo usa C1, y `loan_entry_id` es único.
- C2 hereda el `value_date` de C1.
- P1 y C1 pasan a `REVERSED`.
- Todo en una sola transacción.

## Tareas

#6–#11, #18–#23. Pruebas: #88–#92. Ver [[Tareas]].
