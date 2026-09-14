-- CREATE DATABASE prestamos;

-- DROP TABLE job_runs;
-- DROP TABLE audit_logs;
-- DROP TABLE refresh_tokens;
-- DROP TABLE cash_entries;
-- DROP TABLE freezes;
-- DROP TABLE loan_entries;
-- DROP TABLE loans;
-- DROP TABLE customers;
-- DROP TABLE users;

CREATE TABLE IF NOT EXISTS users(
id             UUID PRIMARY KEY,
name           VARCHAR(50) NOT NULL,
lastname       VARCHAR(50) NOT NULL,
username       VARCHAR(50) NOT NULL,
password_hash       VARCHAR(100) NOT NULL,
role 		   SMALLINT NOT NULL DEFAULT 1, -- WORKER = 1, ADMIN = 2
status         SMALLINT NOT NULL DEFAULT 1, -- ACTIVE = 1, INACTIVE = 2, DELETED = 3
created_by     UUID,
created_date   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
updated_by     UUID,
updated_date   TIMESTAMPTZ,
CONSTRAINT uq_users_username UNIQUE (username),
CONSTRAINT fk_users_created_by FOREIGN KEY (created_by) REFERENCES users(id),
CONSTRAINT fk_users_updated_by FOREIGN KEY (updated_by) REFERENCES users(id)
);

-- LA CLAVE DEL ADMIN SEMILLA ES admin123, PERO AQUI SOLO VA SU HASH DE PasswordHasher (D-020)
INSERT INTO users (id, name, lastname, username, password_hash, role)
VALUES (gen_random_uuid(), 'ADMIN', 'DEFAULT', 'admin', 'AQAAAAIAAYagAAAAEGSYQCSnLCBGB2aJY20VxKfkVAESHgGL2Pnfhx5O2+ftuNFjtHtjZYv71raG/X5PSw==', 2)
ON CONFLICT (username) DO NOTHING;

-- BASES CREADAS ANTES DE LA TAREA 62: EL ADMIN SEMILLA TENIA admin123 EN TEXTO PLANO
UPDATE users SET password_hash = 'AQAAAAIAAYagAAAAEGSYQCSnLCBGB2aJY20VxKfkVAESHgGL2Pnfhx5O2+ftuNFjtHtjZYv71raG/X5PSw==' WHERE username = 'admin' AND password_hash = 'admin123';

-- SELECT * FROM users;



CREATE TABLE IF NOT EXISTS customers(
id					UUID PRIMARY KEY,
fullname 			VARCHAR(100) NOT NULL,
code				VARCHAR(100),
note				TEXT,
phone				VARCHAR(30),
status         SMALLINT NOT NULL DEFAULT 1, -- ACTIVE = 1, DELETED = 3
created_by     UUID NOT NULL,
created_date   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
updated_by     UUID,
updated_date   TIMESTAMPTZ,
CONSTRAINT fk_customers_created_by FOREIGN KEY (created_by) REFERENCES users(id),
CONSTRAINT fk_customers_updated_by FOREIGN KEY (updated_by) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS loans(
id 					UUID PRIMARY KEY,
customer_id 		UUID NOT NULL,
principal			NUMERIC(11,2) NOT NULL,
term				SMALLINT, --Plazo en meses
interest_rate		NUMERIC(5,4) NOT NULL,
loan_date			DATE NOT NULL,
payment_day         SMALLINT NOT NULL DEFAULT 1,
status         SMALLINT NOT NULL DEFAULT 1, 
created_by     UUID NOT NULL,
created_date   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
updated_by     UUID,
updated_date   TIMESTAMPTZ,

CONSTRAINT fk_loans_customer_id FOREIGN KEY (customer_id) REFERENCES customers(id),
CONSTRAINT fk_loans_created_by FOREIGN KEY (created_by) REFERENCES users(id),
CONSTRAINT fk_loans_updated_by FOREIGN KEY (updated_by) REFERENCES users(id)
);


CREATE TABLE IF NOT EXISTS loan_entries(
id 					UUID PRIMARY KEY,
loan_id 			UUID NOT NULL,
idempotency_key 	UUID,
entry_type 			SMALLINT NOT NULL,
principal			NUMERIC(11,2) NOT NULL,
interest			NUMERIC(11, 2) NOT NULL,
note				TEXT,
period 				DATE,
reverses_entry_id 	UUID,
value_date 			DATE NOT NULL,
status         SMALLINT NOT NULL DEFAULT 1, 
created_by     UUID NOT NULL,
created_date   TIMESTAMPTZ NOT NULL DEFAULT NOW(),


CONSTRAINT fk_loan_entries_loan_id FOREIGN KEY (loan_id) REFERENCES loans(id),
CONSTRAINT fk_loan_entries_created_by FOREIGN KEY (created_by) REFERENCES users(id),
CONSTRAINT uq_loan_entries_idempotency_key UNIQUE (idempotency_key),
CONSTRAINT fk_loan_entries_reverses_entry_id FOREIGN KEY (reverses_entry_id) REFERENCES loan_entries(id),
CONSTRAINT uq_loan_entries_reverses_entry_id UNIQUE (reverses_entry_id) 
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_loan_entries_loan_id_and_period
ON loan_entries (loan_id, period)
WHERE entry_type = 2; -- INTERESTCHARGE = 2

CREATE TABLE IF NOT EXISTS freezes(
id 					UUID PRIMARY KEY,
loan_id 			UUID NOT NULL,
start_date			DATE NOT NULL,
end_date			DATE,
reason				TEXT,
authorized_by		UUID NOT NULL,
status         SMALLINT NOT NULL DEFAULT 1, 
created_by     UUID NOT NULL,
created_date   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
updated_by     UUID,
updated_date   TIMESTAMPTZ,


CONSTRAINT fk_freezes_loan_id FOREIGN KEY(loan_id) REFERENCES loans(id),
CONSTRAINT fk_freezes_authorized FOREIGN KEY (authorized_by) REFERENCES users(id),
CONSTRAINT fk_freezes_created_by FOREIGN KEY (created_by) REFERENCES users(id),
CONSTRAINT fk_freezes_updated_by FOREIGN KEY (updated_by) REFERENCES users(id)
);


CREATE UNIQUE INDEX IF NOT EXISTS ux_freezes_if_open
ON freezes(loan_id)
WHERE end_date IS NULL AND status = 1;


CREATE TABLE IF NOT EXISTS cash_entries(
id 						UUID PRIMARY KEY,
entry_type				SMALLINT NOT NULL,
amount					NUMERIC(11, 2) NOT NULL,
value_date				DATE NOT NULL,
loan_entry_id			UUID,
reverses_entry_id 		UUID,
note					TEXT,
status         			SMALLINT NOT NULL DEFAULT 1, 
created_by     			UUID NOT NULL,
created_date   			TIMESTAMPTZ NOT NULL DEFAULT NOW(),
counterparty_user_id 	UUID,
counterparty         	VARCHAR(100),
CONSTRAINT fk_cash_entries_created_by FOREIGN KEY (created_by) REFERENCES users(id),
CONSTRAINT fk_cash_entries_counterparty_user FOREIGN KEY (counterparty_user_id) REFERENCES users(id),
CONSTRAINT fk_cash_entries_loan_entry_id FOREIGN KEY (loan_entry_id) REFERENCES loan_entries(id),
CONSTRAINT uq_cash_entries_loan_entry_id UNIQUE (loan_entry_id),
CONSTRAINT fk_cash_entries_reverses FOREIGN KEY (reverses_entry_id) REFERENCES cash_entries(id),
CONSTRAINT uq_cash_entries_reverses UNIQUE (reverses_entry_id)
);


CREATE TABLE IF NOT EXISTS refresh_tokens(
id 						UUID PRIMARY KEY,
user_id 				UUID NOT NULL,
token_hash 				VARCHAR(64) NOT NULL, -- SHA-256 EN HEXADECIMAL
expires_at 				TIMESTAMPTZ NOT NULL,
revoked_at 				TIMESTAMPTZ,
replaced_by 			UUID,
ip_address 				VARCHAR(45),
created_date   			TIMESTAMPTZ NOT NULL DEFAULT NOW(),
CONSTRAINT fk_refresh_tokens_user_id FOREIGN KEY (user_id) REFERENCES users(id),
CONSTRAINT fk_refresh_tokens_replaced_by FOREIGN KEY (replaced_by) REFERENCES refresh_tokens(id) ON DELETE SET NULL,
CONSTRAINT uq_refresh_tokens_token_hash UNIQUE (token_hash),
CONSTRAINT uq_refresh_tokens_replaced_by UNIQUE (replaced_by)
);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user_id
ON refresh_tokens (user_id);


CREATE TABLE IF NOT EXISTS audit_logs(
id 						UUID PRIMARY KEY,
entity_name				VARCHAR(100),
entity_id				UUID,
action					SMALLINT NOT NULL, -- CREATE = 1, UPDATE = 2, DELETE = 3, LOGIN = 4, LOGINFAILED = 5, LOGOUT = 6
user_id					UUID,
attempted_user			VARCHAR(100),
ip_address				VARCHAR(45),
changes					JSONB,
created_date   			TIMESTAMPTZ NOT NULL DEFAULT NOW(),
CONSTRAINT fk_audit_logs_user_id FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_entity
ON audit_logs (entity_name, entity_id);

CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id_created_date
ON audit_logs (user_id, created_date);


CREATE TABLE IF NOT EXISTS job_runs(
id 						UUID PRIMARY KEY,
job_name				VARCHAR(100) NOT NULL,
period					DATE,
status         			SMALLINT NOT NULL DEFAULT 1, -- RUNNING = 1, SUCCESS = 2, FAILED = 3, PARTIAL = 4
started_at				TIMESTAMPTZ NOT NULL DEFAULT NOW(),
finished_at				TIMESTAMPTZ,
processed				INTEGER NOT NULL DEFAULT 0,
skipped					INTEGER NOT NULL DEFAULT 0,
failed					INTEGER NOT NULL DEFAULT 0,
error_message			TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_job_runs_success
ON job_runs (job_name, period)
WHERE status = 2; -- SUCCESS = 2
