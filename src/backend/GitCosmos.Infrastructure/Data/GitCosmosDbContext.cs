using Microsoft.EntityFrameworkCore;

namespace GitCosmos.Infrastructure.Data;

public class GitCosmosDbContext(DbContextOptions<GitCosmosDbContext> options) : DbContext(options);
