# Preguntas abiertas

Cuando se responde una pregunta: se pasa a [[Registro de decisiones]], se borra de aquí y se desbloquean sus tareas en [[Tareas]].

## Para el usuario

| ID | Pregunta | Recomendación | Bloquea o afecta |
|---|---|---|---|
| P-09 | ¿Reversar un aporte valida efectivo disponible? Reversar un aporte saca dinero de caja | Sí, por la misma regla de D-016 | Afecta #10, #20 |
| P-11 | ¿Se puede reversar un `INTERESTCHARGE`? La especificación lo permite para errores ("nunca debió existir", como un cargo mal calculado por el job), pero D-017 dice que se condonan | Reversar solo errores del sistema; condonar todo lo demás | Afecta #45 |
| P-18 | Hoy un WORKER puede hacer todo menos marcar incobrable y administrar usuarios (D-063). ¿Qué otras acciones son solo del dueño (ADMIN)? | Solo ADMIN: aportes y retiros de caja, reversiones de caja y de asientos, condonaciones, borrar un préstamo y el reporte de ganancia. El WORKER registra clientes, préstamos, pagos, gastos y congelamientos | Afecta #67, #80 |

## Pendientes con el negocio

| ID | Pregunta | Bloquea o afecta |
|---|---|---|
| N-02 | ¿Qué pasa con el interés cuando un pago retroactivo cruza un corte ya calculado? | Afecta #42 |
| N-03 | ¿El saldo de liquidación incluye interés prorrateado del mes en curso? | Afecta #46 |
| N-04 | Primer cargo de interés con el corte global del día 1 (D-054). Ejemplo: un préstamo del 28 de enero. ¿El 1 de febrero ya se le cobra el mes completo, aunque solo lleve 4 días, o su primer cargo es el 1 de marzo? Recomendación: el primer día 1 posterior a la fecha del préstamo, mes completo; con el día de pago el cliente paga cerca de un mes después (préstamo el 28, cargo el 1, paga el 28) | Bloquea #54, #58, #59, #101 |
