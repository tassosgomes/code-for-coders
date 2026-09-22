-- Local development databases for the services declared in docker-compose.yml.
-- The script is safe to run repeatedly and never drops existing databases.

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_audit', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_audit')
\gexec
ALTER ROLE code_for_coders_audit WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_bff_admin', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_bff_admin')
\gexec
ALTER ROLE code_for_coders_bff_admin WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_bff_student', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_bff_student')
\gexec
ALTER ROLE code_for_coders_bff_student WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_commerce', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_commerce')
\gexec
ALTER ROLE code_for_coders_commerce WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_identity', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_identity')
\gexec
ALTER ROLE code_for_coders_identity WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_learning', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_learning')
\gexec
ALTER ROLE code_for_coders_learning WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_media', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_media')
\gexec
ALTER ROLE code_for_coders_media WITH LOGIN PASSWORD :'app_password';

SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', 'code_for_coders_notification', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_notification')
\gexec
ALTER ROLE code_for_coders_notification WITH LOGIN PASSWORD :'app_password';

SELECT 'CREATE ROLE code_for_coders_audit_writer NOLOGIN'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'code_for_coders_audit_writer')
\gexec
ALTER ROLE code_for_coders_audit_writer NOLOGIN;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_audit', 'code_for_coders_audit')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_audit')
\gexec
ALTER DATABASE code_for_coders_audit OWNER TO code_for_coders_audit;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_bff_admin', 'code_for_coders_bff_admin')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_bff_admin')
\gexec
ALTER DATABASE code_for_coders_bff_admin OWNER TO code_for_coders_bff_admin;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_bff_student', 'code_for_coders_bff_student')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_bff_student')
\gexec
ALTER DATABASE code_for_coders_bff_student OWNER TO code_for_coders_bff_student;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_commerce', 'code_for_coders_commerce')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_commerce')
\gexec
ALTER DATABASE code_for_coders_commerce OWNER TO code_for_coders_commerce;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_identity', 'code_for_coders_identity')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_identity')
\gexec
ALTER DATABASE code_for_coders_identity OWNER TO code_for_coders_identity;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_learning', 'code_for_coders_learning')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_learning')
\gexec
ALTER DATABASE code_for_coders_learning OWNER TO code_for_coders_learning;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_media', 'code_for_coders_media')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_media')
\gexec
ALTER DATABASE code_for_coders_media OWNER TO code_for_coders_media;

SELECT format('CREATE DATABASE %I OWNER %I', 'code_for_coders_notification', 'code_for_coders_notification')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'code_for_coders_notification')
\gexec
ALTER DATABASE code_for_coders_notification OWNER TO code_for_coders_notification;
