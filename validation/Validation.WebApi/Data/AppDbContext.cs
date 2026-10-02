using Microsoft.EntityFrameworkCore;
using Validation.WebApi.Models;

namespace Validation.WebApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ValidationOrder> Orders => Set<ValidationOrder>();
    public DbSet<ValidationAudit> Audits => Set<ValidationAudit>();
}
