using Microsoft.EntityFrameworkCore;

namespace Faucet.Data;

public class OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options) : DbContext(options);