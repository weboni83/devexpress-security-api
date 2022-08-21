using DevExpress.ExpressApp.EFCore.Updating;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using DevExpress.Persistent.BaseImpl.EF;
using DevExpress.ExpressApp.Design;
using DevExpress.ExpressApp.EFCore.DesignTime;

namespace FreeWebApiSecurity.WebApi.BusinessObjects;

// This code allows our Model Editor to get relevant EF Core metadata at design time.
// For details, please refer to https://supportcenter.devexpress.com/ticket/details/t933891.
public class FreeWebApiSecurityContextInitializer : DbContextTypesInfoInitializerBase {
	protected override DbContext CreateDbContext() {
		var optionsBuilder = new DbContextOptionsBuilder<FreeWebApiSecurityEFCoreDbContext>()
            .UseSqlServer(@";");
        return new FreeWebApiSecurityEFCoreDbContext(optionsBuilder.Options);
	}
}
//This factory creates DbContext for design-time services. For example, it is required for database migration.
public class FreeWebApiSecurityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FreeWebApiSecurityEFCoreDbContext> {
	public FreeWebApiSecurityEFCoreDbContext CreateDbContext(string[] args) {
		throw new InvalidOperationException("Make sure that the database connection string and connection provider are correct. After that, uncomment the code below and remove this exception.");
		//var optionsBuilder = new DbContextOptionsBuilder<FreeWebApiSecurityEFCoreDbContext>();
		//optionsBuilder.UseSqlServer(@"Integrated Security=SSPI;Pooling=false;Data Source=(localdb)\\mssqllocaldb;Initial Catalog=FreeWebApiSecurity");
		//return new FreeWebApiSecurityEFCoreDbContext(optionsBuilder.Options);
	}
}
[TypesInfoInitializer(typeof(FreeWebApiSecurityContextInitializer))]
public class FreeWebApiSecurityEFCoreDbContext : DbContext {
	public FreeWebApiSecurityEFCoreDbContext(DbContextOptions<FreeWebApiSecurityEFCoreDbContext> options) : base(options) {
	}
	public DbSet<ModuleInfo> ModulesInfo { get; set; }
	public DbSet<ModelDifference> ModelDifferences { get; set; }
	public DbSet<ModelDifferenceAspect> ModelDifferenceAspects { get; set; }
	public DbSet<PermissionPolicyRole> Roles { get; set; }
	public DbSet<FreeWebApiSecurity.WebApi.BusinessObjects.ApplicationUser> Users { get; set; }
    public DbSet<FreeWebApiSecurity.WebApi.BusinessObjects.ApplicationUserLoginInfo> UserLoginInfos { get; set; }
	public DbSet<Post> Post { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<FreeWebApiSecurity.WebApi.BusinessObjects.ApplicationUserLoginInfo>(b => {
            b.HasIndex(nameof(DevExpress.ExpressApp.Security.ISecurityUserLoginInfo.LoginProviderName), nameof(DevExpress.ExpressApp.Security.ISecurityUserLoginInfo.ProviderUserKey)).IsUnique();
        });
    }
}
