# Preguntas abiertas

Cuando se responde una pregunta: se pasa a [[Registro de decisiones]], se borra de aquí y se desbloquean sus tareas en [[Tareas]].

## Para el usuario

| ID | Pregunta | Recomendación | Bloquea o afecta |
|---|---|---|---|
| P-02 | ¿Qué tablas audita el interceptor? Cada pago crea un asiento y una caja que ya guardan `created_by` y nunca se editan | No auditar los inserts de `loan_entries` ni de `cash_entries`; sí su cambio a `REVERSED` | Bloquea #69–#72 |
| P-03 | ¿Se borra `Enums/EntryStatus.cs`? Está vacío y nada lo usa | Borrarlo | Bloquea #29 |
| P-04 | ¿Permiso para instalar `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x? JWT no viene en el framework | Sí | Bloquea #60, #63–#67, #73 |
| P-06 | ¿El préstamo pasa a `CLOSED` automáticamente cuando capital e interés llegan a 0, o lo cierra un usuario? | Automático al quedar el saldo en 0 | Bloquea #46 |
| P-07 | `WRITTENOFF` (incobrable): ¿quién lo decide, y el préstamo sigue generando interés? | Solo ADMIN; deja de generar interés y el saldo queda como pérdida visible en reportes | Bloquea #47 |
| P-08 | Borrado de préstamo: ¿se permite si ya tiene desembolso? El dinero ya salió de caja | Solo si todos sus asientos están reversados; si no, rechazar | Bloquea #48 |
| P-09 | ¿Reversar un aporte valida efectivo disponible? Reversar un aporte saca dinero de caja | Sí, por la misma regla de D-016 | Afecta #10, #20 |
| P-11 | ¿Se puede reversar un `INTERESTCHARGE`? La especificación lo permite para errores ("nunca debió existir", como un cargo mal calculado por el job), pero D-017 dice que se condonan | Reversar solo errores del sistema; condonar todo lo demás | Afecta #45 |
| P-12 | Reportes por tipo y reversiones: "interés cobrado" suma asientos `PAYMENT`, pero un pago reversado sigue siendo `PAYMENT` y se contaría como cobrado. Lo mismo con un gasto reversado en "ganancia real" | En reportes filtrados por tipo, excluir asientos con `status = REVERSED` (la reversión ya queda fuera por su propio tipo). En saldos no se filtra status | Bloquea #77, #78, #80, #109 |
| P-15 | Día de pago: escribiste «de 21 a 28 solamente». ¿Es de **21** a 28, o de **1** a 28? | Confirmar el rango | Bloquea #39 |
| P-16 | Interés que no se paga: ¿se queda en la columna `interest` o se pasa a `principal`? Ejemplo: prestaste 100, Fulanito no pagó el mes 1 y en el mes 2 paga 31. Te sigue debiendo 90, así que de tus 100 solo te devolvió 10: los otros 21 son ganancia. Si el interés impago se queda en `interest`, el reporte dice 21 de ganancia. Si se pasa a `principal`, dice 11, y los 10 restantes aparecen como capital devuelto aunque no lo fueran | Dejarlo en `interest`, como dice la especificación | Bloquea #42, #43 |
| P-17 | Pago sugerido. Los asientos de `loan_entries` no guardan lo que el cliente debe pagar, sino cuánto sube o baja cada saldo: el cargo del mes lleva `principal = 0` e `interest = +10`, y en ningún lado queda escrito «20 de capital». Para mostrar «este mes le toca 20 de capital + 10 de interés» hay que calcularlo: capital original ÷ plazo (o lo que quede, si es menos) más el interés pendiente. Como el plazo es opcional, solo se puede mostrar cuando existe. ¿Se muestra? | Sí, calculado al consultar el préstamo, sin guardarlo | Afecta #41 |

## Pendientes con el negocio

| ID | Pregunta | Bloquea o afecta |
|---|---|---|
| N-01 | ¿El corte es global el día 1, o por aniversario de cada préstamo? | Bloquea #53, #54, #58, #59 |
| N-02 | ¿Qué pasa con el interés cuando un pago retroactivo cruza un corte ya calculado? | Afecta #42 |
| N-03 | ¿El saldo de liquidación incluye interés prorrateado del mes en curso? | Afecta #46 |
