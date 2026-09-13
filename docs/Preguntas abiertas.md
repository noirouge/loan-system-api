# Preguntas abiertas

Cuando se responde una pregunta: se pasa a [[Registro de decisiones]], se borra de aquí y se desbloquean sus tareas en [[Tareas]].

## Para el usuario

| ID | Pregunta | Recomendación | Bloquea o afecta |
|---|---|---|---|
| P-01 | Redondeo. Regla del usuario: si el tercer decimal es mayor que 5 sube, si es 5 o menos se queda (1.266 → 1.27; 1.265 → 1.26). Falta un caso: ¿se mira solo el tercer decimal, o todo lo que sigue? Ejemplo: ¿1.2659 da 1.26 o 1.27? Pasa con tasas de 4 decimales, como 12.5%: 833.33 × 0.125 = 104.16625 | Mirar solo el tercer decimal (1.2659 → 1.26), que es como se hace a mano. Se redondea siempre en C#, nunca con `ROUND` de Postgres, que usa otra regla | Bloquea #37, #53 |
| P-02 | ¿Qué tablas audita el interceptor? Cada pago crea un asiento y una caja que ya guardan `created_by` y nunca se editan | No auditar los inserts de `loan_entries` ni de `cash_entries`; sí su cambio a `REVERSED` | Bloquea #69–#72 |
| P-03 | ¿Se borra `Enums/EntryStatus.cs`? Está vacío y nada lo usa | Borrarlo | Bloquea #29 |
| P-04 | ¿Permiso para instalar `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x? JWT no viene en el framework | Sí | Bloquea #60, #63–#67, #73 |
| P-05 | ¿Permiso para instalar 4 paquetes estándar de .NET en un proyecto de pruebas **aparte**? La API no cambia. `xunit` sirve para escribir las pruebas; `xunit.runner.visualstudio` y `Microsoft.NET.Test.Sdk` para ejecutarlas desde Visual Studio o con `dotnet test`; `Microsoft.AspNetCore.Mvc.Testing` levanta la API dentro de la prueba y llama a sus endpoints como lo haría el frontend | Sí | Bloquea #82–#109 |
| P-06 | ¿El préstamo pasa a `CLOSED` automáticamente cuando capital e interés llegan a 0, o lo cierra un usuario? | Automático al quedar el saldo en 0 | Bloquea #46 |
| P-07 | `WRITTENOFF` (incobrable): ¿quién lo decide, y el préstamo sigue generando interés? | Solo ADMIN; deja de generar interés y el saldo queda como pérdida visible en reportes | Bloquea #47 |
| P-08 | Borrado de préstamo: ¿se permite si ya tiene desembolso? El dinero ya salió de caja | Solo si todos sus asientos están reversados; si no, rechazar | Bloquea #48 |
| P-09 | ¿Reversar un aporte valida efectivo disponible? Reversar un aporte saca dinero de caja | Sí, por la misma regla de D-016 | Afecta #10, #20 |
| P-10 | Pagar más que la cuota está permitido y reduce capital (D-042). Falta el caso de pagar más que **toda** la deuda. Ejemplo: en el mes 4 Fulanito debe 22 en total (20 de capital y 2 de interés) y paga 30. ¿Qué se hace con los 8 de más? (a) rechazar el pago y avisar que el máximo es 22; (b) aceptarlo y dejar el capital en −8, o sea, el negocio le debe 8 a Fulanito | (a) Rechazar con `400` indicando el máximo | Bloquea #42, #43 |
| P-11 | ¿Se puede reversar un `INTERESTCHARGE`? La especificación lo permite para errores ("nunca debió existir", como un cargo mal calculado por el job), pero D-017 dice que se condonan | Reversar solo errores del sistema; condonar todo lo demás | Afecta #45 |
| P-12 | Reportes por tipo y reversiones: "interés cobrado" suma asientos `PAYMENT`, pero un pago reversado sigue siendo `PAYMENT` y se contaría como cobrado. Lo mismo con un gasto reversado en "ganancia real" | En reportes filtrados por tipo, excluir asientos con `status = REVERSED` (la reversión ya queda fuera por su propio tipo). En saldos no se filtra status | Bloquea #77, #78, #80, #109 |
| P-13 | Las pruebas borran todos los datos antes de cada prueba, así que necesitan una base propia, **nunca `prestamos`**. **(A) Contenedor de Docker:** se crea y se destruye solo en cada corrida; requiere Docker abierto al correr las pruebas, el paquete `Testcontainers.PostgreSql` y descargar la imagen `postgres:18` la primera vez. **(B) Base `prestamos_test` en tu PostgreSQL:** la creas una vez con `CREATE DATABASE prestamos_test;`; no necesita Docker ni paquete extra | (B), con una protección que impide correr las pruebas si el nombre de la base no termina en `_test` | Bloquea #83 |
| P-15 | Validaciones al crear un préstamo. (1) Si el cliente está `INACTIVE`, ¿se bloquea el préstamo? (2) ¿El plazo es obligatorio? (3) Día de pago: ¿de 1 a 28, o de 1 a 31 usando el último día en los meses cortos? (4) ¿La fecha del préstamo puede ser futura? (5) ¿La salida de caja del desembolso lleva la fecha del préstamo? (6) Como la tasa va en fracción, ¿se rechazan tasas mayores que 1? | (1) Sí (2) Sí, si se va a mostrar el pago sugerido (P-17) (3) De 1 a 28 (4) No (5) Sí (6) Sí: atrapa a quien escriba `10` queriendo decir 10% | Bloquea #39 |
| P-16 | Interés que no se paga. El negocio lo describe como «se abona al capital», pero la especificación lo guarda aparte, en la cuenta de interés. El saldo final es el mismo, pero la ganancia no. Ejemplo: Fulanito no paga el mes 1 y en el mes 2 paga 31. Con la especificación, 21 son interés cobrado y 10 capital. Sumándolo al capital, serían 11 de interés y 20 de capital. En los dos casos queda debiendo 90, pero el reporte de ganancia muestra 21 o 11 | Mantenerlo aparte, como dice la especificación: es la única forma de que la ganancia cuadre | Bloquea #42 |
| P-17 | ¿El sistema debe calcular y mostrar un pago sugerido del mes? Por ejemplo, para Fulanito: 20 de capital (100 ÷ 5, o lo que quede si es menos) más el interés pendiente. Es solo informativo, no obliga a pagarlo | Sí, como dato de consulta del préstamo | Afecta #41 |

## Pendientes con el negocio

| ID | Pregunta | Bloquea o afecta |
|---|---|---|
| N-01 | ¿El corte es global el día 1, o por aniversario de cada préstamo? | Bloquea #53, #54, #58, #59 |
| N-02 | ¿Qué pasa con el interés cuando un pago retroactivo cruza un corte ya calculado? | Afecta #42 |
| N-03 | ¿El saldo de liquidación incluye interés prorrateado del mes en curso? | Afecta #46 |
