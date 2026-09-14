# Ledger de préstamos

Tabla `loan_entries`. Implementación pendiente (Fase 3 en [[Plan del proyecto]]). Las reglas de negocio están en [[Modelo de negocio]].

## Principio

**Ledger append-only.** No se guardan saldos: los asientos son inmutables y el saldo es una proyección.

- Capital pendiente = `SUM(principal)`
- Interés pendiente = `SUM(interest)`
- Deuda total = capital pendiente + interés pendiente (D-050)
- Lo calcula `LoanBalanceService`, en una sola consulta para uno o varios préstamos (#38)

Nada se edita ni se borra. Un error se corrige con un asiento de reversión que lleva los montos invertidos y apunta al original.

## Dos cuentas separadas

Cada asiento declara cuánto afecta al **capital** y cuánto al **interés**. Los signos son siempre:

- **Positivo sube la deuda.**
- **Negativo la baja.**

Esta separación es la única forma de calcular ganancias: el capital que vuelve no es ingreso, el interés cobrado sí.

> [!note] Signos distintos a los de la caja
> En `loan_entries` el signo se mide desde la deuda del cliente. En `cash_entries` se mide desde la caja del negocio (positivo = entra dinero). Un pago es negativo aquí y positivo allá.

## Semántica de cada tipo

| Tipo | `principal` | `interest` | `period` | Notas |
|---|---|---|---|---|
| `DISBURSEMENT` (1) | +monto | 0 | NULL | Uno por préstamo. Genera caja negativa |
| `INTERESTCHARGE` (2) | 0 | +monto | Día 1 del mes | Lo crea el job mensual. No genera caja |
| `PAYMENT` (3) | ≤ 0 | ≤ 0 | NULL | Cascada: interés primero, luego capital. Genera caja positiva |
| `FORGIVENESS` (4) | 0 | −monto | NULL | Sin dinero de por medio. No genera caja |
| `REVERSAL` (5) | Invertido | Invertido | NULL | Lleva `reverses_entry_id` |

## Reversión

- El asiento original pasa a `REVERSED`; el nuevo nace `APPLIED`.
- **Las dos filas siguen sumando en el saldo.** Se anulan por los signos, no por filtrar el status. Excluir las reversadas del `SUM` descuenta dos veces.
- La reversión **hereda el `value_date` del original**, no la fecha de hoy (D-013).
- Si el asiento reversado tenía movimiento de caja, se crea también la reversión de caja. Su `loan_entry_id` apunta al **asiento de reversión nuevo**, no al original (D-018). Ejemplo completo en [[Caja]].
- Por ahora los cargos de interés no se reversan, se condonan (D-017; P-11).

## Pagos: idempotencia

- Cada pago lleva `idempotency_key` con índice único.
- **Inserta primero y captura la violación** (`SqlState 23505`). No consultes antes: eso tiene condición de carrera.
- Misma clave con un cuerpo distinto → error del cliente, **422**.
- Implementado en #43. La clave se busca bajo el bloqueo de la fila del préstamo y antes de validar el monto: así el reintento de un pago que saldó la deuda devuelve el pago original en vez de rechazarse por exceder la deuda. El índice único sigue siendo la garantía, también para la misma clave en préstamos distintos.

## Pagos: concurrencia

Dos pagos simultáneos al mismo préstamo leen el mismo saldo y el segundo pisa al primero.

Solución: bloquear la fila del préstamo con `SELECT ... FOR UPDATE` al inicio de la transacción.

Implementado en #42: `SELECT id FROM loans WHERE id = ... FOR UPDATE` antes de leer el saldo.

## Redondeo

Todo monto calculado se redondea a 2 decimales mirando solo el tercer decimal: si es mayor que 5 sube, si es 5 o menos se queda (D-043).

Lo hace `MoneyRounding.Round()`. No se usa `Math.Round`, porque ninguno de sus modos sigue esta regla.

## Reportes

| Reporte | Cálculo |
|---|---|
| Interés devengado | `SUM(interest)` de asientos `INTERESTCHARGE` |
| Interés cobrado | `SUM(interest)` de asientos `PAYMENT`, con signo invertido |
| Interés pendiente | `SUM(interest)` de toda la columna |
| Capital pendiente | `SUM(principal)` de toda la columna |
| Ganancia real | Interés cobrado − gastos de caja |

**Filtra por tipo, no por signo.** `PAYMENT` y `FORGIVENESS` llevan interés negativo, pero solo el primero es dinero que entró.

> [!note] Reversiones en reportes por tipo
> Un pago reversado sigue siendo de tipo `PAYMENT` y su reversión es de tipo `REVERSAL`. En los reportes filtrados por tipo, la reversión cuenta con el tipo del asiento que reversa, así el par suma 0 (D-055).

## Tareas

#35, #36, #38–#48. Pruebas: #93–#99. Ver [[Tareas]].
