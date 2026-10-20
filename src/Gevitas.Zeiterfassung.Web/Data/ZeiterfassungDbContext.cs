using Gevitas.Zeiterfassung.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gevitas.Zeiterfassung.Web.Data;

public class ZeiterfassungDbContext : DbContext
{
    public ZeiterfassungDbContext(DbContextOptions<ZeiterfassungDbContext> options)
        : base(options)
    {
    }

    public DbSet<Mitarbeiter> Mitarbeiter => Set<Mitarbeiter>();
    public DbSet<Zeitbuchung> Zeitbuchungen => Set<Zeitbuchung>();
    public DbSet<Buchungsprotokoll> Buchungsprotokolle => Set<Buchungsprotokoll>();
    public DbSet<Tagesbemerkung> Tagesbemerkungen => Set<Tagesbemerkung>();
    public DbSet<Abwesenheitsart> Abwesenheitsarten => Set<Abwesenheitsart>();
    public DbSet<Abwesenheit> Abwesenheiten => Set<Abwesenheit>();
    public DbSet<Antrag> Antraege => Set<Antrag>();
    public DbSet<Tagesberechtigung> Tagesberechtigungen => Set<Tagesberechtigung>();
    public DbSet<Kontobuchung> Kontobuchungen => Set<Kontobuchung>();
    public DbSet<Regelquittierung> Regelquittierungen => Set<Regelquittierung>();
    public DbSet<Benachrichtigung> Benachrichtigungen => Set<Benachrichtigung>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Mitarbeiter>(e =>
        {
            e.HasIndex(m => m.Benutzername).IsUnique();
            e.HasIndex(m => m.Personalnummer).IsUnique();
            e.Property(m => m.Benutzername).HasMaxLength(64).IsRequired();
            e.Property(m => m.Personalnummer).HasMaxLength(32).IsRequired();
            e.Property(m => m.Vorname).HasMaxLength(100);
            e.Property(m => m.Nachname).HasMaxLength(100);
            e.Property(m => m.Email).HasMaxLength(200);
            e.Property(m => m.Abteilung).HasMaxLength(100);
            e.Property(m => m.Design).HasMaxLength(10);
            e.Property(m => m.Sprache).HasMaxLength(5);
            e.HasOne(m => m.Vorgesetzter).WithMany().HasForeignKey(m => m.VorgesetzterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.AbweichenderGenehmiger).WithMany().HasForeignKey(m => m.AbweichenderGenehmigerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Zeitbuchung>(e =>
        {
            e.HasIndex(b => new { b.MitarbeiterId, b.Datum });
            e.HasOne(b => b.Mitarbeiter).WithMany().HasForeignKey(b => b.MitarbeiterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Buchungsprotokoll>(e =>
        {
            e.HasIndex(p => new { p.MitarbeiterId, p.Datum });
            e.Property(p => p.Details).HasMaxLength(500);
            e.Property(p => p.Bemerkung).HasMaxLength(1000);
        });

        modelBuilder.Entity<Tagesbemerkung>(e =>
        {
            e.HasIndex(t => new { t.MitarbeiterId, t.Datum }).IsUnique();
            e.Property(t => t.Text).HasMaxLength(2000);
        });

        modelBuilder.Entity<Abwesenheitsart>(e =>
        {
            e.HasKey(a => a.Code);
            e.Property(a => a.Code).HasMaxLength(10);
            e.Property(a => a.Name).HasMaxLength(150);
            e.Property(a => a.Farbe).HasMaxLength(20);
        });

        modelBuilder.Entity<Abwesenheit>(e =>
        {
            e.HasIndex(a => new { a.MitarbeiterId, a.Von, a.Bis });
            e.HasOne(a => a.Mitarbeiter).WithMany().HasForeignKey(a => a.MitarbeiterId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Art).WithMany().HasForeignKey(a => a.ArtCode).OnDelete(DeleteBehavior.Restrict);
            e.Property(a => a.Bemerkung).HasMaxLength(1000);
        });

        modelBuilder.Entity<Antrag>(e =>
        {
            e.HasIndex(a => new { a.GenehmigerId, a.Status });
            e.HasIndex(a => new { a.AntragstellerId, a.Status });
            e.HasOne(a => a.Antragsteller).WithMany().HasForeignKey(a => a.AntragstellerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Genehmiger).WithMany().HasForeignKey(a => a.GenehmigerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.EntschiedenVon).WithMany().HasForeignKey(a => a.EntschiedenVonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(a => a.Abwesenheit).WithMany().HasForeignKey(a => a.AbwesenheitId).OnDelete(DeleteBehavior.SetNull);
            e.Property(a => a.Bemerkung).HasMaxLength(1000);
            e.Property(a => a.Entscheidungsbemerkung).HasMaxLength(1000);
        });

        modelBuilder.Entity<Tagesberechtigung>(e =>
        {
            e.HasIndex(t => new { t.MitarbeiterId, t.Datum }).IsUnique();
        });

        modelBuilder.Entity<Kontobuchung>(e =>
        {
            e.HasIndex(k => new { k.MitarbeiterId, k.Datum });
            e.HasIndex(k => k.Vorgang);
            e.Property(k => k.Text).HasMaxLength(200);
            e.Property(k => k.Bemerkung).HasMaxLength(1000);
        });

        modelBuilder.Entity<Regelquittierung>(e =>
        {
            e.HasIndex(r => new { r.MitarbeiterId, r.Datum, r.Regelcode }).IsUnique();
            e.Property(r => r.Regelcode).HasMaxLength(50);
        });

        modelBuilder.Entity<Benachrichtigung>(e =>
        {
            e.HasIndex(b => new { b.MitarbeiterId, b.Gelesen });
            e.Property(b => b.Titel).HasMaxLength(200);
            e.Property(b => b.Text).HasMaxLength(2000);
        });
    }
}
