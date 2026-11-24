using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Repro;

/// <summary>
///     Workaround for EF Core 11.0.0: gives the migrations scaffolder its own instance of the model snapshot.
/// </summary>
/// <remarks>
///     The context's <see cref="IMigrationsAssembly" /> caches one <see cref="ModelSnapshot" />, and
///     <c>HasPendingModelChanges()</c> and <c>Migrate()</c> finalize its model. <c>SnapshotModelProcessor.Process</c>
///     returns <see langword="null" /> for a finalized model, and the scaffolder then diffs against nothing. A new
///     instance of the snapshot class builds a new, mutable model, which the processor upgrades and finalizes as usual.
///     Register it in the design-time services after <c>AddDbContextDesignTimeServices</c>.
/// </remarks>
public sealed class FreshSnapshotMigrationsAssembly(IMigrationsAssembly inner) : IMigrationsAssembly
{
    private ModelSnapshot? _snapshot;
    private bool _snapshotCreated;

    public ModelSnapshot? ModelSnapshot
    {
        get
        {
            if (!_snapshotCreated)
            {
                var shared = inner.ModelSnapshot;
                _snapshot = shared == null ? null : (ModelSnapshot)Activator.CreateInstance(shared.GetType())!;
                _snapshotCreated = true;
            }

            return _snapshot;
        }
    }

    public IReadOnlyDictionary<string, TypeInfo> Migrations => inner.Migrations;
    public Assembly Assembly => inner.Assembly;
    public string? FindMigrationId(string nameOrId) => inner.FindMigrationId(nameOrId);
    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider) => inner.CreateMigration(migrationClass, activeProvider);
    public void AddMigrations(Assembly additionalMigrationsAssembly) => inner.AddMigrations(additionalMigrationsAssembly);
}
