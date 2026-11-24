using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Repro;

// The current model: Email was added after the Initial migration.
public sealed class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
}

public sealed class AppDb : DbContext
{
    public DbSet<Person> People => Set<Person>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=repro.db");
}

// The migration and snapshot as `dotnet ef migrations add Initial` writes them: no Email yet.
[DbContext(typeof(AppDb))]
[Migration("20260101000000_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.CreateTable(
            name: "People",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_People", x => x.Id));

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "People");
}

[DbContext(typeof(AppDb))]
public sealed class AppDbModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");

        modelBuilder.Entity("Repro.Person", b =>
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<string>("Name").IsRequired().HasColumnType("TEXT");
            b.HasKey("Id");
            b.ToTable("People");
        });
    }
}
