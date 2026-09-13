# Sistema de préstamos — Inicio

> [!important] Para cualquier agente de IA
> Este vault es la memoria del proyecto. Lee esta nota completa antes de hacer cualquier cosa, y después las notas que indique la tarea. No necesitas el historial de conversaciones anteriores.

## Qué es

API en .NET 8 + EF Core + PostgreSQL para un negocio de préstamos informales en República Dominicana. Dos usuarios operan el sistema: el dueño (ADMIN) y un empleado (WORKER). El frontend (React) es un proyecto aparte.

No es un préstamo con cuotas: es una línea de crédito con interés mensual sobre el saldo total. Todo lo demás se deriva de esa regla → [[Modelo de negocio]].

## Orden de lectura

1. [[Modelo de negocio]] — las reglas del negocio. Sin esto nada tiene sentido.
2. [[Stack y convenciones]] — cómo está escrito el código y cómo imitarlo.
3. [[Tareas]] — qué está hecho y qué sigue.
4. [[Preguntas abiertas]] — lo que está bloqueado y por qué.
5. La nota de arquitectura del área en la que vas a trabajar.

## Mapa del vault

| Carpeta | Contenido |
|---|---|
| `01 Negocio` | [[Modelo de negocio]] |
| `02 Arquitectura` | [[Stack y convenciones]], [[Esquema de datos]], [[Ledger de prestamos]], [[Caja]], [[Job del corte mensual]], [[Autenticacion]], [[Auditoria]], [[Endpoints]] |
| `03 Decisiones` | [[Registro de decisiones]] |
| `04 Plan` | [[Plan del proyecto]], [[Tareas]] |
| `05 Cambios` | [[Registro de cambios]] |
| raíz | [[Preguntas abiertas]] |

## Reglas de trabajo

Aplican a cualquier agente, en cualquier sesión.

1. **No escribas código si no te lo piden.** Cuando el usuario diga "todavía no hagas nada", solo lee, analiza y pregunta.
2. **No instales dependencias sin permiso.** Ningún paquete NuGet ni herramienta. Primero verifica si el framework compartido `Microsoft.AspNetCore.App` ya lo trae.
3. **Antes de implementar, señala contradicciones.** Si algo choca con [[Modelo de negocio]], con un invariante de [[Esquema de datos]] o con una decisión de [[Registro de decisiones]], dilo antes de escribir. Si algo es ambiguo, pregunta; no asumas.
4. **Imita el estilo existente.** Ver [[Stack y convenciones]].
5. **Revisa [[Preguntas abiertas]] antes de empezar una tarea.** Una tarea `Bloqueada` no se empieza.
6. **Al terminar una tarea, haz el commit, pero nunca el push.** El push lo hace el usuario a mano. Formato del mensaje en [[Stack y convenciones]].
7. **Mantén el vault al día en el mismo commit que la tarea.** Ver la sección siguiente.

## Cómo actualizar el vault

Al terminar una tarea:

1. En [[Tareas]], cambia su **Estado**.
2. Si la tarea cambia algo que entregó una tarea anterior, en la fila de **la tarea anterior** pon `¿Modificada? = Sí` y agrega el número de la nueva en `Modificada por`.
3. Agrega una entrada en [[Registro de cambios]].
4. Si se tomó una decisión, agrégala a [[Registro de decisiones]] con el siguiente `D-###`.
5. Si se respondió una pregunta, pásala a decisiones, quítala de [[Preguntas abiertas]] y desbloquea las tareas que dependían de ella.
6. Si cambió un endpoint, actualiza [[Endpoints]].

**Las tareas nunca se borran ni se renumeran.** Si el plan cambia, se crea una tarea nueva que modifica o cancela a la anterior. Es la misma idea que el ledger del sistema: nada se edita, todo se corrige con un asiento nuevo.

## Fuente de verdad

- El **código** manda sobre *qué hace* el sistema hoy.
- El **vault** manda sobre *por qué* se hizo así y *qué sigue*.
- Si se contradicen, avisa al usuario antes de tocar cualquiera de los dos.
