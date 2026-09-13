# Registro de cambios

Una entrada por cambio, la más reciente arriba. Cada entrada nombra su tarea de [[Tareas]].

Un commit no puede contener su propio hash, así que desde la tarea #12 las entradas no lo citan. Cada commit lleva `(Task N)` en el mensaje, y se encuentra con:

```bash
git log --grep "Task 13"
```

Las entradas anteriores a la #12 sí llevan hash, porque se reconstruyeron desde el historial.

## 2026-09-13

- **#14**: en `db/schema.sql`, `users.role` pasa a `DEFAULT 1` (WORKER) y el admin semilla se inserta con `role = 2` (ADMIN), alineados con el enum `UserRole` (modifica #2). Antes todo usuario nuevo nacía ADMIN. La base local todavía necesita la #15.
- Se agregan las pruebas de integración al plan y a las tareas: infraestructura y red de seguridad sobre lo que ya existe (#82–#89) y pruebas por fase (#90–#109). Se cancela la #49, reemplazada por #82 y #94 (D-040).
- `docs/` sale de `.gitignore` (D-039). El vault vuelve a ser visible para git, todavía sin commitear.
- Se deshacen con `git reset` los commits de las tareas #12 y #13, que no se habían publicado, y `docs/` pasa a `.gitignore` (D-038). Los archivos se conservan en el disco. El cambio de `db/schema.sql` de la #13 queda sin commitear.
- **#13**: en `db/schema.sql`, el índice `ux_loan_entries_loan_id_and_period` pasa de `entry_type = 1` (desembolso) a `entry_type = 2` (cargo de interés), que es lo que protege el invariante de un cargo por período (modifica #2). Las bases ya creadas conservan el índice viejo porque `CREATE ... IF NOT EXISTS` no lo reemplaza; se agrega la tarea #81 para recrearlo en la base local.
- **#12**: se crea el vault de documentación en `docs/`, con modelo de negocio, arquitectura, decisiones, plan, tareas y preguntas abiertas. Se agregan `AGENTS.md` y `CLAUDE.md` en la raíz, que apuntan al vault, y se ignoran los archivos de estado local de Obsidian en `.gitignore`.

## 2026-09-09

- **#11** `2c34fff`: `GET api/cash-entries`. Lista las entradas de caja proyectadas a `CashEntryDTO`, sin campos de auditoría.
- **#10** `0f26f04`: `POST api/cash-entries/reversal/{id}`. Reversa aportes, retiros y gastos.
- **#9** `4d6b892`: `POST api/cash-entries/expense`. Gasto con al menos un counterparty.
- **#8** `fdd12e1`: `POST api/cash-entries/withdrawal`. Retiro. La ruta del controller pasa de `api/cash-entry` a `api/cash-entries` (modifica #6).
- **#7** `cc4a4d9`: el aporte deja de exigir counterparty (modifica #6).
- **#6** `9876ded`: `POST api/cash-entry/contribution`. Aporte de caja.

## 2026-08-31

- **#5** `edc1f58`: CRUD de clientes (GET por id, PUT, DELETE lógico) y enums del dominio.

## 2026-08-27

- **#4** `ea976e9`: `GET api/customers`.

## 2026-08-26

- **#3** `f3b6d32`: `POST api/customers`.
- **#1** `dea6d9a`: conexión a PostgreSQL con EF Core y convención snake_case.

## 2026-08-25

- `62248a8`, `c033b53`: commits iniciales con la plantilla de ASP.NET Core Web API.

> [!note] Tarea #2
> El esquema inicial (`db/schema.sql`) se construyó a lo largo de varios commits entre agosto y septiembre; no tiene un commit único.
