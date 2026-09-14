# Auditoría

Tabla `audit_logs`. La escribe `AuditInterceptor` desde #69.

> [!note] Decisión D-053
> El interceptor audita también los inserts y los cambios de estado de `loan_entries` y `cash_entries`.

## Qué registra

Bitácora de **acciones**, no de montos. Los ledgers ya guardan quién creó cada asiento; esto captura lo demás: ediciones de clientes, logins fallidos, congelamientos, condonaciones.

```sql
audit_logs(id, entity_name, entity_id, action, user_id,
           attempted_user, ip_address, changes, created_date)
```

| Columna | Detalle |
|---|---|
| `entity_name`, `entity_id` | Nullables: un login fallido no pertenece a ninguna entidad |
| `user_id` | Nullable: si alguien intenta entrar con un usuario inexistente, no hay id que guardar |
| `attempted_user` | El texto que se tecleó en el login. Muchos intentos con nombres inventados son señal de ataque |
| `action` | `AuditAction`: `CREATE=1`, `UPDATE=2`, `DELETE=3`, `LOGIN=4`, `LOGINFAILED=5`, `LOGOUT=6` |
| `changes` | `JSONB` con los cambios |

Índices sobre `(entity_name, entity_id)` y `(user_id, created_date)`. Sin columnas de actualización: una bitácora no se edita.

## Formato de `changes` (D-027, D-029)

| Acción | Contenido |
|---|---|
| `CREATE` | El registro **completo**, solo valores nuevos |
| `UPDATE` | **Solo los campos que cambiaron**, con `old` y `new` |
| `DELETE` | El registro completo como `old`. **El borrado lógico (`Status = DELETED`) se registra como `DELETE`**, aunque para EF sea un update |

```json
{ "phone": { "old": "809-555-1234", "new": "849-555-9876" } }
```

Se guarda `old` porque es lo único irrecuperable: el valor actual ya está en su tabla. Las claves de `changes` son los nombres de columna y cada campo es siempre un objeto: `CREATE` lleva `{ "columna": { "new": ... } }` por cada columna, `UPDATE` `{ "columna": { "old": ..., "new": ... } }` solo por las que cambiaron, y `DELETE` `{ "columna": { "old": ... } }` por cada columna. Un `UPDATE` sin cambios reales no se registra. `entity_name` es el nombre de la tabla (D-065, #70).

## Implementación

- **Interceptor de `SaveChanges`** en EF Core, usando el `ChangeTracker` (`OriginalValues`, `CurrentValues`). Nada de llamadas manuales desde cada servicio: se olvidan.
- `AuditInterceptor` (scoped, agregado en `AddDbContext`) arma los registros en `SavingChanges`, cuando el `ChangeTracker` todavía tiene los valores originales, con `AuditEntryBuilder`; `AuditLogWriter` los guarda con otro `DbContext`. Usuario e IP salen del JWT y de la conexión; sin petición (un job, una prueba que escribe directo) quedan nulos. Las operaciones masivas (`ExecuteUpdate`, `ExecuteDelete`) no pasan por el `ChangeTracker` y no se auditan: por eso cerrar un congelamiento pasó a ser un cambio normal con la fila bloqueada `FOR UPDATE` (#69).
- **El cambio de negocio manda** (D-030). La bitácora se escribe **después** de confirmar la transacción, en una transacción aparte. Si falla, se loguea el error y el cambio queda guardado.
- Sin transacción explícita, los registros se escriben al terminar `SaveChanges`, que ya confirmó. Dentro de una, esperan al commit (`IDbTransactionInterceptor`) y un rollback los descarta. `AuditLogWriter` atrapa cualquier error al guardarlos, lo registra en el log y no lo propaga (#71).
- **Excepción manual:** `LOGIN`, `LOGINFAILED` y `LOGOUT` no cambian entidades, así que el interceptor no los ve. Se escriben desde un servicio (D-028). En `LOGINFAILED`, `user_id` va lleno si el usuario existe y `attempted_user` siempre lleva lo tecleado.
- `AuthController` los escribe con `AuditLogWriter`: `LOGIN` y `LOGINFAILED` en cada login, con lo tecleado en `attempted_user` y la IP, y `LOGOUT` cuando el logout revoca un token vigente, con el dueño del token. Un login de un usuario inactivo cuenta como `LOGINFAILED` con su `user_id` (#73).

## Exclusiones (D-031)

- **Campos:** `password_hash`, `token_hash`. Nunca se registran contraseñas, hashes ni tokens.
- **Tablas:** `audit_logs` (se auditaría a sí misma), `refresh_tokens` (la rotación cada 15 minutos inundaría la bitácora), `job_runs`.
- **Ledgers (`loan_entries`, `cash_entries`):** sí se auditan (D-053).
- Implementado en `AuditEntryBuilder` (#72): las tablas excluidas no generan registro y las columnas excluidas no aparecen en `changes`. Si lo único que cambió fue una columna excluida, como la contraseña re-hasheada en un login, no queda registro.

## Retención

Los registros de más de un año se purgan con un job diario (#75, D-032).

## Tareas

#69–#73, #75. Pruebas: #106, #107. Ver [[Tareas]].
