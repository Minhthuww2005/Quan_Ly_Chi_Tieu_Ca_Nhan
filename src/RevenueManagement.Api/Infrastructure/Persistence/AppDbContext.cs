using Microsoft.EntityFrameworkCore;

namespace RevenueManagement.Api.Infrastructure.Persistence;

// TODO(A): thêm DbSet và ApplyConfigurationsFromAssembly khi triển khai entity.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
