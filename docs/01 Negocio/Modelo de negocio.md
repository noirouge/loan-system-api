# Modelo de negocio

Negocio de préstamos informales en una comunidad local de República Dominicana. El prestamista otorga préstamos a trabajadores de cooperativas y empresas, y a particulares. Operan el sistema dos usuarios: el dueño (ADMIN) y un empleado (WORKER).

## La regla más importante

**No hay cuotas ni plazo fijo.** No es un préstamo con tabla de amortización: es una línea de crédito con interés mensual sobre el saldo. El cliente paga lo que quiera, cuando quiera: todo el primer mes, solo el interés durante seis meses, o menos que el interés.

- **El plazo (`term`) es informativo.** Se guarda porque el deudor quiere saber cómo se espera que pague. No genera cuotas ni valida nada.
- **No existe la mora.** El castigo por no pagar es que el interés se capitaliza: se suma al saldo y empieza a generar interés él mismo.
- **El interés es sobre el saldo total.** Cada mes: `tasa × (capital pendiente + interés pendiente)`.

## Ejemplo canónico

Préstamo de 1000 al 10% mensual:

| Evento | Cálculo | Capital | Interés pendiente |
|---|---|---:|---:|
| Desembolso | | 1000 | 0 |
| Cargo mes 1 | 10% de 1000 = 100 | 1000 | 100 |
| Pago de 300 | −100 interés, −200 capital | 800 | 0 |
| Cargo mes 2 | 10% de 800 = 80 | 800 | 80 |
| Pago de 40 | solo interés, parcial | 800 | 40 |
| Cargo mes 3 | 10% de 840 = 84 | 800 | 124 |
| Pago de 324 | −124 interés, −200 capital | 600 | 0 |

> [!warning] Prueba crítica
> En el mes 3 el interés se calcula sobre **840**, no sobre 800, porque quedaron 40 de interés sin pagar. Da **84**. Si una implementación da 80, está mal. Lo verifica la prueba de integración #94.

## Ejemplo del negocio: Fulanito

Préstamo de 100 al 10% mensual, con plazo informativo de 5 meses (capital sugerido por mes: 100 ÷ 5 = 20).

| Mes | Interés del mes | Paga | Se aplica | Capital después |
|---|---|---:|---|---:|
| 1 | 10% de 100 = 10 | 30 | 10 a interés, 20 a capital | 80 |
| 2 | 10% de 80 = 8 | 28 | 8 a interés, 20 a capital | 60 |
| 3 | 10% de 60 = 6 | 46 | 6 a interés, 40 a capital (paga de más) | 20 |
| 4 | 10% de 20 = 2 | 22 | 2 a interés, 20 a capital | 0 |

Termina un mes antes del plazo. Pagar de más siempre está permitido y reduce capital (D-042).

**Si no paga el mes 1:** los 10 de interés quedan pendientes y en el mes 2 el interés se calcula sobre 110, o sea, 11. Debe 100 de capital y 21 de interés. Si paga 31, la cascada aplica 21 a interés y 10 a capital, y queda debiendo 90. Cómo se registra ese interés impago está en P-16.

## Cascada de imputación

Todo pago se aplica en este orden:

1. **Interés pendiente**: todo lo acumulado, sin distinguir de qué mes vino.
2. **Capital.**

| Caso | Resultado |
|---|---|
| Paga más que el interés pendiente | El excedente reduce capital |
| Paga exactamente el interés pendiente | Capital intacto |
| Paga menos que el interés pendiente | El faltante queda pendiente y capitaliza el mes siguiente |

El interés pendiente nunca queda negativo. Un pago mayor que toda la deuda se rechaza (D-044).

## Condonar no es reversar

- **Condonación** (`FORGIVENESS`): "esto existió y decido no cobrarlo". Es una decisión de negocio.
- **Reversión** (`REVERSAL`): "esto nunca debió existir". Corrige un error.

Las dos bajan el interés, pero solo la condonación es decisión de negocio. Por ahora los cargos de interés se condonan, no se reversan (D-017, provisional; P-11).

## Fechas

| Campo | Significado | Uso |
|---|---|---|
| `value_date` | Cuándo ocurrió el hecho | **Manda para todos los cálculos** |
| `created_date` | Cuándo entró al sistema | Solo auditoría |
| `period` | A qué mes pertenece un cargo de interés | Solo en `INTERESTCHARGE`, siempre día 1 |

Caso real: el cliente paga el viernes y se digita el lunes → `value_date` = viernes.

"Hoy" siempre es la fecha local dominicana (UTC−4), nunca la fecha UTC (D-011).

## Congelamientos

Pausan el devengo de interés de un préstamo.

- Mientras un congelamiento tenga `end_date` nulo, el job mensual salta ese préstamo.
- Solo miran hacia adelante: no borran cargos ya generados. Perdonar el mes en curso es una condonación aparte.
- Se cierran escribiendo `end_date`, no cambiando `status`.
- Un préstamo nunca tiene dos congelamientos abiertos.

## Caja

El dueño aporta capital al negocio, y de ese efectivo se presta. La caja registra cada entrada y salida de dinero → [[Caja]].

- **No se puede desembolsar, retirar ni gastar más que el efectivo disponible** (D-016).
- Desembolsos y pagos generan su movimiento de caja automáticamente, en la misma transacción que el asiento del préstamo.
- Aportes, retiros y gastos se registran a mano.

## Ganancia

El capital que vuelve **no es ingreso**; el interés cobrado **sí**. Por eso cada asiento separa capital de interés → [[Ledger de prestamos]].

**Ganancia real = interés cobrado − gastos.**

## Glosario

| Español | En el código |
|---|---|
| Capital | `principal` |
| Desembolso | `DISBURSEMENT` |
| Cargo de interés | `INTERESTCHARGE` |
| Pago | `PAYMENT` |
| Condonación | `FORGIVENESS` |
| Reversión | `REVERSAL` |
| Congelamiento | `Freeze` |
| Caja | `cash_entries` |
| Aporte | `CONTRIBUTION` |
| Retiro | `WITHDRAWAL` |
| Gasto | `EXPENSE` |
| Incobrable | `WRITTENOFF` |
| Asiento | Fila de `loan_entries` o de `cash_entries` |

## Pendiente con el negocio

Ver [[Preguntas abiertas]]: N-01 (corte global o por aniversario), N-02 (pago retroactivo que cruza un corte), N-03 (liquidación con interés prorrateado).
