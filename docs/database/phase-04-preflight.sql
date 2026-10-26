-- READ-ONLY. Run only after connection access is approved, in the intended database.
-- Do not store private server/login output in public evidence.
SELECT SERVERPROPERTY('ProductVersion') AS ProductVersion,
       SERVERPROPERTY('Edition') AS Edition,
       DB_NAME() AS DatabaseName;
SELECT compatibility_level, collation_name, state_desc,
       is_read_committed_snapshot_on, snapshot_isolation_state_desc
FROM sys.databases WHERE name = DB_NAME();
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints;
SELECT name, delete_referential_action_desc, is_disabled, is_not_trusted
FROM sys.foreign_keys;
SELECT OBJECT_NAME(object_id) AS TableName, name, is_unique, filter_definition
FROM sys.indexes WHERE object_id IN (SELECT object_id FROM sys.tables);
-- The migration-history query fails deliberately if the schema is not deployed.
-- TLS session inspection may need DBA assistance/extra permissions; no grants here.
