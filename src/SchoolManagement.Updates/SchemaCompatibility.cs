namespace SchoolManagement.Updates;

public static class SchemaCompatibility
{
    public static void Ensure(int current, int fromSchema, int toSchema)
    {
        if (current < MigrationManager.BaselineSchemaVersion)
        {
            throw new MigrationException(
                $"AppSchemaVersion={current} est inférieur à la baseline {MigrationManager.BaselineSchemaVersion}.");
        }

        if (fromSchema < MigrationManager.BaselineSchemaVersion)
        {
            throw new MigrationException("fromSchemaVersion doit être ≥ 1.");
        }

        if (toSchema < fromSchema)
        {
            throw new MigrationException("toSchemaVersion < fromSchemaVersion.");
        }

        if (current < fromSchema)
        {
            throw new MigrationException(
                $"Package fromSchemaVersion={fromSchema} trop élevé pour la version actuelle {current}.");
        }

        if (current > toSchema)
        {
            throw new MigrationException(
                $"Cible {toSchema} inférieure à la version actuelle {current}.");
        }
    }
}
