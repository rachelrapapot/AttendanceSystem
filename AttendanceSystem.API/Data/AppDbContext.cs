using AttendanceSystem.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ClockEvent> ClockEvents => Set<ClockEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(r => r.RoleId);
            e.Property(r => r.RoleName).HasMaxLength(50).IsRequired();
            e.HasIndex(r => r.RoleName).IsUnique().HasDatabaseName("IX_Roles_RoleName");
            e.HasData(
                new Role { RoleId = 1, RoleName = "Employee" },
                new Role { RoleId = 2, RoleName = "Admin" }
            );
        });

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasKey(emp => emp.EmployeeId);
            e.Property(emp => emp.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(emp => emp.Email).IsUnique().HasDatabaseName("IX_Employees_Email");
            e.Property(emp => emp.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(emp => emp.FirstName).HasMaxLength(100).IsRequired();
            e.Property(emp => emp.LastName).HasMaxLength(100).IsRequired();
            e.Property(emp => emp.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
            e.HasCheckConstraint("CK_Employees_Status", "[Status] IN ('Active', 'Inactive', 'Terminated')");
            e.Property(emp => emp.CreatedAt).HasColumnType("datetimeoffset");
            e.Ignore(emp => emp.FullName);
            e.HasIndex(emp => emp.RoleId).HasDatabaseName("IX_Employees_RoleId");
            e.HasOne(emp => emp.Role)
             .WithMany(r => r.Employees)
             .HasForeignKey(emp => emp.RoleId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.TokenHash).HasMaxLength(256).IsRequired();
            e.Property(rt => rt.ExpiresAt).HasColumnType("datetimeoffset");
            e.Property(rt => rt.RevokedAt).HasColumnType("datetimeoffset");
            e.Property(rt => rt.CreatedAt).HasColumnType("datetimeoffset");
            e.HasIndex(rt => rt.EmployeeId).HasDatabaseName("IX_RefreshTokens_EmployeeId");
            e.HasOne(rt => rt.Employee)
             .WithMany(emp => emp.RefreshTokens)
             .HasForeignKey(rt => rt.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClockEvent>(e =>
        {
            e.HasKey(ce => ce.EventId);
            e.Property(ce => ce.EventType).HasMaxLength(10).IsRequired();
            e.HasCheckConstraint("CK_ClockEvents_EventType", "[EventType] IN ('ClockIn', 'ClockOut')");
            e.Property(ce => ce.Timestamp).HasColumnType("datetimeoffset");
            e.Property(ce => ce.CreatedAt).HasColumnType("datetimeoffset");

            e.HasIndex(ce => ce.EmployeeId).HasDatabaseName("IX_ClockEvents_EmployeeId");
            e.HasIndex(ce => ce.Timestamp).HasDatabaseName("IX_ClockEvents_Timestamp");

            e.HasOne(ce => ce.Employee)
             .WithMany(emp => emp.ClockEvents)
             .HasForeignKey(ce => ce.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(al => al.AuditId);
            e.Property(al => al.Action).HasMaxLength(50).IsRequired();
            e.HasCheckConstraint("CK_AuditLogs_Action",
                "[Action] IN ('ClockIn', 'ClockOut', 'Login', 'LogoutFailed', 'AdminAction')");
            e.Property(al => al.Details).HasColumnType("nvarchar(max)");
            e.Property(al => al.Timestamp).HasColumnType("datetimeoffset");
            e.Property(al => al.CreatedAt).HasColumnType("datetimeoffset");

            e.HasIndex(al => al.EmployeeId).HasDatabaseName("IX_AuditLogs_EmployeeId");
            e.HasIndex(al => al.Timestamp).HasDatabaseName("IX_AuditLogs_Timestamp");
            e.HasIndex(al => al.CreatedAt).HasDatabaseName("IX_AuditLogs_CreatedAt");

            e.HasOne(al => al.Employee)
             .WithMany(emp => emp.AuditLogs)
             .HasForeignKey(al => al.EmployeeId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
