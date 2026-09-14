# Autenticación

En construcción (Fase 6 en [[Plan del proyecto]]). El paquete `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.30 ya está instalado (#60): la misma versión de parche que el runtime local y que `Microsoft.AspNetCore.Mvc.Testing`.

> [!note] Permiso concedido (D-052)
> JWT usa el paquete `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.x, que no viene en el framework compartido.

Mientras tanto, los controllers usan `_adminId` desde `appsettings` como usuario actual (D-019).

## Dos tokens

| Token | Duración | Dónde vive | Revocable |
|---|---|---|---|
| Access (JWT) | 15 minutos | Solo en el cliente. El servidor lo valida por firma, sin tocar la base | No; por eso es corto |
| Refresh | 7 días (D-022) | En `refresh_tokens`, **hasheado** | Sí |

## Flujo

1. **Login** emite los dos.
2. Cuando el access expira, el cliente llama a `api/auth/refresh` con el refresh y recibe un par nuevo.
3. El usuario no vuelve a ver el login hasta que el refresh caduque o se revoque.
4. **Logout** llena `revoked_at`.
5. `AuthTokenService` firma el access con HS256 y los claims `sub` (id del usuario), `unique_name` y `role`, y crea el refresh con 32 bytes aleatorios en base64url. Las fechas salen del `TimeProvider` y la validación del JWT usa ese mismo reloj, sin tolerancia (D-062, #63).

## `refresh_tokens`

```sql
refresh_tokens(id, user_id, token_hash, expires_at, revoked_at,
               replaced_by, ip_address, created_date)
```

| Columna | Detalle |
|---|---|
| `token_hash` | SHA-256 del token (D-021), en hexadecimal (`VARCHAR(64)`) y con índice único, que también sirve para buscarlo. Si alguien lee la tabla, no puede suplantar sesiones. El token es aleatorio de alta entropía, así que no necesita el costo de un hash de contraseña |
| `revoked_at` | Nulo = vigente. Se llena en logout o al revocar las sesiones de un usuario |
| `replaced_by` | FK a `refresh_tokens(id)` con `UNIQUE` (D-023) y `ON DELETE SET NULL`, para que la limpieza de vencidos no choque con la FK. Implementa la rotación |
| `ip_address` | `VARCHAR(45)` (cabe IPv6). Sin `X-Forwarded-For` por ahora (D-026) |

Sin `status` ni columnas de actualización: `revoked_at` y `expires_at` dicen todo. Índice sobre `user_id` para revocar en bloque.

## Rotación y detección de robo

Cada uso del refresh emite uno nuevo y marca el viejo con `replaced_by` apuntando al reemplazo.

Si llega un token que **ya tiene `replaced_by`**, es reúso, señal de robo:

- Se revocan **todos los refresh tokens vigentes del usuario** (D-024) y se fuerza re-login.
- Si dos pestañas refrescan a la vez, la segunda dispara esta regla y el usuario legítimo se desloguea. **Se acepta ese falso positivo**; no hay ventana de gracia (D-025).
- El token usado se retira con un `UPDATE ... WHERE replaced_by IS NULL` dentro de la transacción que crea el nuevo. De dos refresh simultáneos con el mismo token, solo uno lo retira; el otro espera la fila, la encuentra reemplazada y revoca todas las sesiones (#64).

## Contraseñas

- `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core`, que ya viene en el framework (D-020). Sin dependencias.
- El admin semilla de `db/schema.sql` guarda el hash de `admin123` hecho con `PasswordHasher`, no el texto plano. Para las bases creadas antes, el mismo script trae un `UPDATE` que pone el hash solo si la contraseña sigue en texto plano, así que basta con volver a correrlo (#62).
- Nunca se registran contraseñas, hashes ni tokens, ni en logs ni en la [[Auditoria]].

## Configuración

- Clave de firma del JWT en **user-secrets** (D-022). `dotnet user-secrets init` agrega `UserSecretsId` al `.csproj`; no es un paquete.
- `Jwt:SigningKey` son 64 bytes aleatorios en base64, guardados en los user-secrets de la máquina de desarrollo, fuera del repo. En otra máquina se crea con `dotnet user-secrets set "Jwt:SigningKey" "<clave de al menos 32 bytes>"`. Lo que no es secreto está en `appsettings.json`: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` = 15 y `Jwt:RefreshTokenDays` = 7 (#61).

## Usuario actual

`ICurrentUserService` (#30) expone `UserId`. Hoy `CurrentUserService` lo lee del `AdminId` de configuración, y lanza un error si falta o no es un GUID válido (antes los controllers seguían con `Guid.Empty`). En #66 pasa a leer el claim del JWT. Así el cambio a login real toca un solo lugar.

## Limpieza

Los tokens vencidos se borran con un job diario registrado en `job_runs` (#74).

## Tareas

#30, #31, #60–#68, #74. Pruebas: #103–#105. Ver [[Tareas]].
