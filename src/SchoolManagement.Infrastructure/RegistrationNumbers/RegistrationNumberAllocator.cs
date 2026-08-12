using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolManagement.Application.EnrollmentWizard;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.RegistrationNumbers;

/// <summary>
/// Allocation atomique SQL Server (UPDLOCK/ROWLOCK/HOLDLOCK) du compteur matricule.
/// Transaction propre si appelée seule ; rejoint la transaction ambiante si P1 (CompleteAsync) en a déjà ouvert une.
/// </summary>
public sealed class RegistrationNumberAllocator : IRegistrationNumberAllocator
{
    private readonly SchoolDbContext _db;

    public RegistrationNumberAllocator(SchoolDbContext db)
    {
        _db = db;
    }

    public async Task<string> PreviewNextAsync(
        Guid schoolId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var counter = await _db.RegistrationNumberCounters
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.SchoolId == schoolId && c.Year == year && !c.IsDeleted,
                cancellationToken);

        if (counter is not null)
        {
            return RegistrationNumberFormat.Format(year, counter.NextValue);
        }

        var seed = await ComputeSeedFromStudentsAsync(schoolId, year, cancellationToken);
        return RegistrationNumberFormat.Format(year, seed);
    }

    public async Task<string> AllocateAsync(
        Guid schoolId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Année de matricule invalide.");
        }

        // P1 : si une transaction ambiante existe déjà (CompleteAsync),
        // rejoindre cette TX — pas de Begin/Commit imbriqués, ni de CreateExecutionStrategy imbriqué.
        // Ainsi un ROLLBACK métier annule aussi l'incrément du compteur.
        if (_db.Database.CurrentTransaction is not null)
        {
            var sequenceInAmbient = await AllocateSequenceAsync(schoolId, year, cancellationToken);
            return RegistrationNumberFormat.Format(year, sequenceInAmbient);
        }

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            try
            {
                var sequence = await AllocateSequenceAsync(schoolId, year, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return RegistrationNumberFormat.Format(year, sequence);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<int> AllocateSequenceAsync(
        Guid schoolId,
        int year,
        CancellationToken cancellationToken)
    {
        // Verrouille la ligne compteur (ou le « gap » via HOLDLOCK) pour sérialiser les allocations.
        const string selectSql = """
            SELECT NextValue
            FROM RegistrationNumberCounters WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            WHERE SchoolId = @schoolId AND [Year] = @year AND IsDeleted = 0
            """;

        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _db.Database.OpenConnectionAsync(cancellationToken);
        }

        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            selectCmd.CommandText = selectSql;
            AddGuidParam(selectCmd, "@schoolId", schoolId);
            AddIntParam(selectCmd, "@year", year);

            var existing = await selectCmd.ExecuteScalarAsync(cancellationToken);
            if (existing is not null and not DBNull)
            {
                var allocated = Convert.ToInt32(existing);
                await BumpCounterAsync(connection, schoolId, year, allocated + 1, cancellationToken);
                return allocated;
            }
        }

        // Première allocation pour SchoolId+Year : seed depuis matricules existants (soft-deleted inclus).
        var seed = await ComputeSeedFromStudentsAsync(schoolId, year, cancellationToken);
        try
        {
            await InsertCounterAsync(connection, schoolId, year, seed + 1, cancellationToken);
            return seed;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Unique violation — un autre worker a créé la ligne ; relire sous verrou.
        }

        await using (var retryCmd = connection.CreateCommand())
        {
            retryCmd.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            retryCmd.CommandText = selectSql;
            AddGuidParam(retryCmd, "@schoolId", schoolId);
            AddIntParam(retryCmd, "@year", year);

            var existing = await retryCmd.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Compteur matricule introuvable après conflit pour SchoolId={schoolId}, Year={year}.");

            var allocated = Convert.ToInt32(existing);
            await BumpCounterAsync(connection, schoolId, year, allocated + 1, cancellationToken);
            return allocated;
        }
    }

    private async Task BumpCounterAsync(
        System.Data.Common.DbConnection connection,
        Guid schoolId,
        int year,
        int nextValue,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = """
            UPDATE RegistrationNumberCounters
            SET NextValue = @nextValue, UpdatedAt = SYSUTCDATETIME()
            WHERE SchoolId = @schoolId AND [Year] = @year AND IsDeleted = 0
            """;
        AddIntParam(cmd, "@nextValue", nextValue);
        AddGuidParam(cmd, "@schoolId", schoolId);
        AddIntParam(cmd, "@year", year);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertCounterAsync(
        System.Data.Common.DbConnection connection,
        Guid schoolId,
        int year,
        int nextValue,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = """
            INSERT INTO RegistrationNumberCounters
                (Id, SchoolId, [Year], NextValue, CreatedAt, IsDeleted)
            VALUES
                (@id, @schoolId, @year, @nextValue, SYSUTCDATETIME(), 0)
            """;
        AddGuidParam(cmd, "@id", Guid.NewGuid());
        AddGuidParam(cmd, "@schoolId", schoolId);
        AddIntParam(cmd, "@year", year);
        AddIntParam(cmd, "@nextValue", nextValue);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ComputeSeedFromStudentsAsync(
        Guid schoolId,
        int year,
        CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters : inclure soft-deleted pour ne jamais réutiliser un ancien numéro.
        var numbers = await _db.Students
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.SchoolId == schoolId)
            .Select(s => s.RegistrationNumber)
            .ToListAsync(cancellationToken);

        return RegistrationNumberFormat.ComputeNextValue(numbers, year);
    }

    private static void AddGuidParam(System.Data.Common.DbCommand cmd, string name, Guid value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static void AddIntParam(System.Data.Common.DbCommand cmd, string name, int value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
