using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Repro;

// The model adds one column (Email) to the snapshot. Every scaffold below should therefore produce one AddColumn.
var failures = 0;

// 1. Control: scaffold on a context that has not touched migrations yet.
using (var db = new AppDb())
{
    Check("fresh context", Scaffold(db));
}

// 2. The bug: ask EF whether there is anything to scaffold, then scaffold on the same context.
using (var db = new AppDb())
{
    Console.WriteLine($"HasPendingModelChanges() = {db.Database.HasPendingModelChanges()}");
    Check("same context, after HasPendingModelChanges()", Scaffold(db));
}

return failures;

void Check(string scenario, ScaffoldedMigration migration)
{
    var creates = Count(migration.MigrationCode, "migrationBuilder.CreateTable(");
    var adds = Count(migration.MigrationCode, "migrationBuilder.AddColumn<");
    var ok = creates == 0 && adds == 1;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {scenario}: {adds} AddColumn, {creates} CreateTable");
    if (!ok) Console.WriteLine("     expected 1 AddColumn (Email) and no CreateTable; the snapshot was ignored");
}

static int Count(string text, string token)
    => (text.Length - text.Replace(token, "").Length) / token.Length;

// Programmatic scaffolding through the public design-time API, as a tool or framework does it.
static ScaffoldedMigration Scaffold(AppDb db)
{
    var services = new ServiceCollection();
    services.AddEntityFrameworkDesignTimeServices();
    services.AddDbContextDesignTimeServices(db);

    // The provider's design-time services, found the way the EF tools find them.
    var providerAssembly = Assembly.Load(db.Database.ProviderName!);
    var attribute = providerAssembly.GetCustomAttribute<DesignTimeProviderServicesAttribute>()!;
    var designTimeServices = (IDesignTimeServices)Activator.CreateInstance(
        providerAssembly.GetType(attribute.TypeName, throwOnError: true)!)!;
    designTimeServices.ConfigureDesignTimeServices(services);

    // The workaround (fix branch): the scaffolder gets its own snapshot instance, never the one the context's
    // migrator finalized in HasPendingModelChanges() or Migrate().
    var sharedAssembly = db.GetService<IMigrationsAssembly>();
    services.Replace(ServiceDescriptor.Singleton<IMigrationsAssembly>(new FreshSnapshotMigrationsAssembly(sharedAssembly)));

    using var provider = services.BuildServiceProvider();
    return provider.GetRequiredService<IMigrationsScaffolder>().ScaffoldMigration("AddEmail", "Repro");
}
