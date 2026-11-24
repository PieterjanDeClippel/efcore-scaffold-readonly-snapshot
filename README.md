# Scaffolding after `HasPendingModelChanges()` recreates every table (EF Core 11)

Minimal repro for [dotnet/efcore#36016](https://github.com/dotnet/efcore/issues/36016). SQLite, no server needed:

```
dotnet run
```

```
OK   fresh context: 1 AddColumn, 0 CreateTable
HasPendingModelChanges() = True
FAIL same context, after HasPendingModelChanges(): 0 AddColumn, 1 CreateTable
     expected 1 AddColumn (Email) and no CreateTable; the snapshot was ignored
```

The model adds one column (`Email`) to the snapshot. Scaffolding through the public design-time API
(`AddEntityFrameworkDesignTimeServices`, `AddDbContextDesignTimeServices`, `IMigrationsScaffolder`) gives the
expected `AddColumn` on a fresh context. Calling `context.Database.HasPendingModelChanges()` first on the same
context makes the scaffolder emit a migration that recreates the table, without any warning.

## Cause (release/11.0)

1. `Migrator.HasPendingModelChanges()` (`Migrator.cs:805-808`) finalizes the snapshot model with
   `IModelRuntimeInitializer.Initialize`. The snapshot is the instance cached on the context's scoped
   `IMigrationsAssembly`, so the model is now read-only.
2. `AddDbContextDesignTimeServices(context)` hands the scaffolder that same `IMigrationsAssembly`.
3. `MigrationsScaffolder.ScaffoldMigration` (`MigrationsScaffolder.cs:145-146`) passes the snapshot model to
   `SnapshotModelProcessor.Process`, which returns `null` for a read-only model (`SnapshotModelProcessor.cs:59-66`,
   added by #35432 "Make SnapshotModelProcessor idempotent").
4. The scaffolder treats `null` as "no previous model" and diffs the current model against nothing.

`Migrate()` finalizes the snapshot the same way. `RemoveMigration` has the same pattern with
`Process(modelSnapshot.Model, resetVersion: true).GetRelationalModel()` (`MigrationsScaffolder.cs:272`), where the
`null` becomes a `NullReferenceException`.

#35432's test (`Assert.Same(model, processor.Process(model))`) passes because it uses
`DummyModelRuntimeInitializer`, which never finalizes the model; with the real initializer the second call returns
`null`.

## Fix (this branch)

**Workaround, EF Core 11.0.0 as released:** [`FreshSnapshotMigrationsAssembly`](FreshSnapshotMigrationsAssembly.cs)
wraps the context's `IMigrationsAssembly` in the design-time services and gives the scaffolder its own instance of
the snapshot class. That instance builds a new, mutable model, which the processor upgrades and finalizes as
usual. With it, `dotnet run` passes both scenarios:

```
OK   fresh context: 1 AddColumn, 0 CreateTable
HasPendingModelChanges() = True
OK   same context, after HasPendingModelChanges(): 1 AddColumn, 0 CreateTable
```

**Proposed EF Core change:** [`upstream/efcore-release-11.0.patch`](upstream/efcore-release-11.0.patch), against
`release/11.0` (applies cleanly with `git apply`). `MigrationsScaffolder` does the same thing internally:
`ScaffoldMigration` and `RemoveMigration` take a new snapshot instance when the shared one's model is already
finalized. It is a fresh instance rather than returning the finalized model from `SnapshotModelProcessor`, because
the migrator finalizes the snapshot **without** processing it: a snapshot written by an older EF version would then
skip the annotation upgrades the processor exists for. The patch has not been built or run in EF's own test suite;
the workaround above is the same logic, outside EF.

A regression test would finalize the snapshot model before scaffolding (with the real `IModelRuntimeInitializer`,
not `DummyModelRuntimeInitializer`) and assert that the scaffolded operations are the incremental ones.
