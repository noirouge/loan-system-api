# Job del corte mensual

Genera los cargos de interés. El mecanismo genérico de jobs ya existe (#55–#57); el job de cargos en sí sigue pendiente (Fase 5 en [[Plan del proyecto]], N-04).

> [!warning] Bloqueo parcial
> El corte es global el día 1 (D-054). El primer cargo de un préstamo es el día 1 del mes siguiente a su fecha (D-068).

## Qué hace

Para cada préstamo `ACTIVE` y no congelado:

1. Calcula `tasa × (capital pendiente + interés pendiente)`, redondeado a 2 decimales con la regla de D-043. Lo hace `InterestCharge.Calculate` (#53); si la deuda es 0 o menos, el cargo es 0.
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
- `DailyJobsService` (`BackgroundService`) corre al arrancar y luego cada 24 horas todos los `IDailyJob` registrados. Cada job decide qué tiene pendiente y lo corre dentro de `JobRunner`, que toma `pg_try_advisory_lock(2001, hashtext(job_name))` en una conexión que queda abierta hasta liberarlo. El error de un job se registra en el log y no detiene a los demás ni a la API. Las pruebas lo apagan con `Jobs:Enabled=false` y corren los jobs a mano (#55).

## `job_runs`

```sql
job_runs(id, job_name, period, status, started_at, finished_at,
         processed, skipped, failed, error_message)
```

- **Una fila por período.** Recuperar tres meses perdidos son tres filas.
- Al arrancar se inserta con `RUNNING` y `finished_at` nulo, **en su propia transacción**, separada del trabajo. Si fueran juntas y el proceso fallara, el rollback se llevaría justo la evidencia de que se intentó.
- Al terminar se actualiza con el resultado.
- `JobRunner.RunAsync(job, período, trabajo)` guarda la fila `RUNNING` con su propio `SaveChanges` antes del trabajo y escribe el resultado con `ExecuteUpdate`, fuera del change tracker. El trabajo devuelve `JobRunCounters`. Si lanza una excepción, la corrida queda `FAILED` con el mensaje en `error_message`. Un período que ya terminó en `SUCCESS` no se vuelve a correr (D-061, #56).
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

Si el proceso muere a media ejecución, la fila queda en `RUNNING` para siempre. Como el advisory lock garantiza que solo corre una instancia, **cualquier fila en `RUNNING` encontrada mientras se tiene el lock es huérfana**: pasa a `FAILED` con un `error_message` que lo indique. `JobRunner` lo hace apenas toma el lock, antes de revisar el período, y solo con las filas del mismo `job_name` (#57).

## Otros jobs

Mismo mecanismo, cada uno con su `job_name` y `period` nulo:

- Limpieza de refresh tokens vencidos: `RefreshTokenCleanupJob`, `job_name` = `refresh-token-cleanup` (#74).
- Purga de `audit_logs` de más de un año: `AuditLogPurgeJob`, `job_name` = `audit-log-purge` (#75).

## Preguntas que afectan este job

- N-04: cuándo le toca el primer cargo a un préstamo nuevo.
- N-02: pago retroactivo que cruza un corte ya calculado.
- P-01: redondeo en empate.

## Tareas

#53–#59, #74, #75. Pruebas: #101, #102, #108. Ver [[Tareas]].
