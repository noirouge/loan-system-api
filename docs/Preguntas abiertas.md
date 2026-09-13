# Preguntas abiertas

Cuando se responde una pregunta: se pasa a [[Registro de decisiones]], se borra de aquí y se desbloquean sus tareas en [[Tareas]].

## Para el usuario

| ID | Pregunta | Recomendación | Bloquea o afecta |
|---|---|---|---|
| P-01 | Redondeo en empate: ¿1.265 da 1.26 o 1.27? `Math.Round` de C# da 1.26 por defecto; `ROUND` de Postgres da 1.27 | 1.27 (`MidpointRounding.AwayFromZero`), igual que Postgres y que la cuenta a mano | Bloquea #37, #53 |
| P-02 | ¿Qué tablas audita el interceptor? Cada pago crea un asiento y una caja que ya guardan `created_by` y nunca se editan | No auditar los inserts de `loan_entries` ni de `cash_entries`; sí su cambio a `REVERSED` | Bloquea #69–#72 |
| P-03 | ¿Se borra `Enums/EntryStatus.cs`? Está vacío y nada lo usa | Borrarlo | Bloquea #29 |
| P-04 | ¿Permiso para instalar `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x? JWT no viene en el framework | Sí | Bloquea #60, #63–#67, #73 |
| P-05 | ¿Permiso para los paquetes de pruebas de integración? `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` y `Microsoft.AspNetCore.Mvc.Testing` 8.0.x. Si en P-13 se elige contenedor, también `Testcontainers.PostgreSql` | Sí. Sin ellos no hay forma de probar lo que depende de PostgreSQL | Bloquea #82–#109 |
| P-06 | ¿El préstamo pasa a `CLOSED` automáticamente cuando capital e interés llegan a 0, o lo cierra un usuario? | Automático al quedar el saldo en 0 | Bloquea #46 |
| P-07 | `WRITTENOFF` (incobrable): ¿quién lo decide, y el préstamo sigue generando interés? | Solo ADMIN; deja de generar interés y el saldo queda como pérdida visible en reportes | Bloquea #47 |
| P-08 | Borrado de préstamo: ¿se permite si ya tiene desembolso? El dinero ya salió de caja | Solo si todos sus asientos están reversados; si no, rechazar | Bloquea #48 |
| P-09 | ¿Reversar un aporte valida efectivo disponible? Reversar un aporte saca dinero de caja | Sí, por la misma regla de D-016 | Afecta #10, #20 |
| P-10 | Pago mayor que la deuda total (capital + interés): ¿se rechaza, o el capital puede quedar negativo? | Rechazar con `400` indicando el saldo | Bloquea #42, #43 |
| P-11 | ¿Se puede reversar un `INTERESTCHARGE`? La especificación lo permite para errores ("nunca debió existir", como un cargo mal calculado por el job), pero D-017 dice que se condonan | Reversar solo errores del sistema; condonar todo lo demás | Afecta #45 |
| P-12 | Reportes por tipo y reversiones: "interés cobrado" suma asientos `PAYMENT`, pero un pago reversado sigue siendo `PAYMENT` y se contaría como cobrado. Lo mismo con un gasto reversado en "ganancia real" | En reportes filtrados por tipo, excluir asientos con `status = REVERSED` (la reversión ya queda fuera por su propio tipo). En saldos no se filtra status | Bloquea #77, #78, #80, #109 |
| P-13 | ¿Dónde corre PostgreSQL en las pruebas? **Contenedor** (Testcontainers sobre Docker, que ya está instalado): cada corrida arranca una base limpia y desechable, pero agrega un paquete y Docker tiene que estar abierto. **Base local de prueba** (`prestamos_test` en el PostgreSQL 18 local): sin paquete extra, pero hay que crearla a mano y nunca puede ser la base de desarrollo | Contenedor con la imagen `postgres:18`, la misma versión mayor que la local | Bloquea #83 |
| P-14 | ¿Cómo se envía la tasa de interés al crear un préstamo: `10` (porcentaje) o `0.10` (fracción)? La columna `interest_rate` guarda `0.1000` | Porcentaje en la API (`10`), que es como lo dice el negocio; el API lo divide entre 100 al guardar | Bloquea #39 |
| P-15 | Validaciones al crear un préstamo: ¿el cliente debe estar `ACTIVE`? ¿`term` es opcional? ¿`payment_day` va de 1 a 28 o de 1 a 31? ¿`loan_date` puede ser futura? ¿El desembolso usa `loan_date` como `value_date`? | Cliente `ACTIVE`; `term` opcional; `payment_day` de 1 a 28 para que exista en todos los meses; `loan_date` no futura; desembolso con `value_date = loan_date` | Bloquea #39 |

## Pendientes con el negocio

| ID | Pregunta | Bloquea o afecta |
|---|---|---|
| N-01 | ¿El corte es global el día 1, o por aniversario de cada préstamo? | Bloquea #53, #54, #58, #59 |
| N-02 | ¿Qué pasa con el interés cuando un pago retroactivo cruza un corte ya calculado? | Afecta #42 |
| N-03 | ¿El saldo de liquidación incluye interés prorrateado del mes en curso? | Afecta #46 |
