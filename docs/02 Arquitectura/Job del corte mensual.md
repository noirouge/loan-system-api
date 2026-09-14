# Job del corte mensual

Genera los cargos de interés. Implementación pendiente (Fase 5 en [[Plan del proyecto]]).

> [!warning] Bloqueo parcial
> El corte es global el día 1 (D-054). Falta N-04: cuándo le toca el primer cargo a un préstamo nuevo. Bloquea #54, #58, #59 y #101; el cálculo (#53) y el mecanismo genérico (#55–#57) sí se pueden construir.

## Qué hace

Para cada préstamo `ACTIVE` y no congelado:

1. Calcula `tasa × (capital pendiente + interés pendiente)`, redondeado a 2 decimales.
2. Inserta un `INTERESTCHARGE` con `period` = día 1 del mes.

## Requisitos

- **Reejecutable sin duplicar.** `INSERT ... ON CONFLICT DO NOTHING`, apoyado en `ux_loan_entries_loan_id_and_period`.
- **Recupera períodos perdidos.** Si el servidor estuvo caído, al arrancar genera los cargos faltantes, no solo el de hoy.
- **Acepta el período como parámetro**, para poder re-correrlo manualmente.
- **Salta préstamos congelados**: los que tienen un congelamiento con `end_date` nulo.

## Diseño (D-033)

- Vive como `BackgroundService` dentro de la API. No requiere dependencias.
- Solo corre una instancia a la vez: **advisory lock de Postgres** (`pg_try_advisory_lock`). Si no obtiene el lock, no hace nada.
- Revisa una vez al día, y al arrancar, si falta el cargo de algún período ya vencido, y lo genera (D-054).

## `job_runs`

```sql
job_runs(id, job_name, period, status, started_at, finished_at,
         processed, skipped, failed, error_message)
```

- **Una fila por período.** Recuperar tres meses perdidos son tres filas.
- Al arrancar se inserta con `RUNNING` y `finished_at` nulo, **en su propia transacción**, separada del trabajo. Si fueran juntas y el proceso fallara, el rollback se llevaría justo la evidencia de que se intentó.
- Al terminar se actualiza con el resultado.
- `period` es nullable, para los jobs que no son por período (limpiezas). Postgres trata los NULL como distintos en un índice único, así que esos jobs pueden terminar en `SUCCESS` muchas veces.

| Contador | Significado |
|---|---|
| `processed` | Cargos creados |
| `skipped` | Préstamos que ya tenían cargo del período. Normal al re-ejecutar, **no es error** |
| `failed` | Préstamos que fallaron por otra causa |

| Resultado | Condición |
|---|---|
| `SUCCESS` (2) | `failed = 0` |
| `PARTIAL` (4) | `failed > 0` y además `processed + skipped > 0` |
| `FAILED` (3) | `failed > 0` y nada procesado ni saltado |

`ux_job_runs_success ON job_runs (job_name, period) WHERE status = 2` garantiza un solo éxito por período. Puede haber varios intentos fallidos del mismo mes.

### Corridas huérfanas

Si el proceso muere a media ejecución, la fila queda en `RUNNING` para siempre. Como el advisory lock garantiza que solo corre una instancia, **cualquier fila en `RUNNING` encontrada mientras se tiene el lock es huérfana**: pasa a `FAILED` con un `error_message` que lo indique.

## Otros jobs

Mismo mecanismo, cada uno con su `job_name` y `period` nulo:

- Limpieza de refresh tokens vencidos (#74).
- Purga de `audit_logs` de más de un año (#75).

## Preguntas que afectan este job

- N-01: corte global o por aniversario.
- N-02: pago retroactivo que cruza un corte ya calculado.
- P-01: redondeo en empate.

## Tareas

#53–#59, #74, #75. Pruebas: #101, #102, #108. Ver [[Tareas]].
