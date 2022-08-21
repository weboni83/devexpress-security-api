using DevExpress.ExpressApp.Security;
using DevExpress.Persistent.Base;
using Microsoft.EntityFrameworkCore;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using System.Security.Principal;
using System.Security.Claims;
using Microsoft.Identity.Web;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using DevExpress.ExpressApp.WebApi.Services;
using Microsoft.AspNetCore.OData;
using DevExpress.ExpressApp.Core;
using FreeWebApiSecurity.WebApi.JWT;
using DevExpress.ExpressApp.Security.Authentication;
using DevExpress.ExpressApp.Security.Authentication.ClientServer;
using FreeWebApiSecurity.WebApi.Core;
using DevExpress.ExpressApp.AspNetCore.WebApi;
using FreeWebApiSecurity.WebApi.BusinessObjects;

namespace FreeWebApiSecurity.WebApi;

public class Startup {
    public Startup(IConfiguration configuration) {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services) {
        services
            .AddScoped<IAuthenticationTokenProvider, JwtTokenProviderService>()
            .AddScoped<IObjectSpaceProviderFactory, ObjectSpaceProviderFactory>()
            .AddSingleton<IWebApiApplicationSetup, WebApiApplicationSetup>();

        services.AddXafAspNetCoreSecurity(Configuration, options => {
            options.RoleType = typeof(PermissionPolicyRole);
            // ApplicationUser descends from PermissionPolicyUser and supports the OAuth authentication. For more information, refer to the following topic: https://docs.devexpress.com/eXpressAppFramework/402197
            // If your application uses PermissionPolicyUser or a custom user type, set the UserType property as follows:
            options.UserType = typeof(BusinessObjects.ApplicationUser);
            // ApplicationUserLoginInfo is only necessary for applications that use the ApplicationUser user type.
            // If you use PermissionPolicyUser or a custom user type, comment out the following line:
            options.UserLoginInfoType = typeof(BusinessObjects.ApplicationUserLoginInfo);

            options.SupportNavigationPermissionsForTypes = false;
        })
        .AddAuthenticationStandard(options => {
            options.IsSupportChangePassword = true;
        })
        .AddExternalAuthentication(options => {
            options.Events.OnAuthenticated = (externalAuthenticationContext) => {
                // When a user successfully logs in with an OAuth provider, you can get their unique user key.
                // The following code finds an ApplicationUser object associated with this key.
                // This code also creates a new ApplicationUser object for this key automatically.
                // For more information, see the following topic: https://docs.devexpress.com/eXpressAppFramework/402197
                // If this behavior meets your requirements, comment out the line below.
                return;
                if(externalAuthenticationContext.AuthenticatedUser == null &&
                externalAuthenticationContext.Principal.Identity.AuthenticationType != SecurityDefaults.PasswordAuthentication &&
                externalAuthenticationContext.Principal.Identity.AuthenticationType != SecurityDefaults.WindowsAuthentication && !(externalAuthenticationContext.Principal is WindowsPrincipal)) {
                    const bool autoCreateUser = true;

                    IObjectSpace objectSpace = externalAuthenticationContext.LogonObjectSpace;
                    ClaimsPrincipal externalUser = (ClaimsPrincipal)externalAuthenticationContext.Principal;

                    var userIdClaim = externalUser.FindFirst("sub") ?? externalUser.FindFirst(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Unknown user id");
                    string providerUserId = userIdClaim.Value;

                    var userLoginInfo = FindUserLoginInfo(externalUser.Identity.AuthenticationType, providerUserId);
                    if(userLoginInfo != null || autoCreateUser) {
                        externalAuthenticationContext.AuthenticatedUser = userLoginInfo?.User ?? CreateApplicationUser(externalUser.Identity.Name, providerUserId);
                    }

                    object CreateApplicationUser(string userName, string providerUserId) {
                        if(objectSpace.FirstOrDefault<FreeWebApiSecurity.WebApi.BusinessObjects.ApplicationUser>(user => user.UserName == userName) != null) {
                            throw new ArgumentException($"The username ('{userName}') was already registered within the system");
                        }
                        var user = objectSpace.CreateObject<FreeWebApiSecurity.WebApi.BusinessObjects.ApplicationUser>();
                        user.UserName = userName;
                        user.SetPassword(Guid.NewGuid().ToString());
                        user.Roles.Add(objectSpace.FirstOrDefault<PermissionPolicyRole>(role => role.Name == "Default"));
                        ((ISecurityUserWithLoginInfo)user).CreateUserLoginInfo(externalUser.Identity.AuthenticationType, providerUserId);
                        objectSpace.CommitChanges();
                        return user;
                    }
                    ISecurityUserLoginInfo FindUserLoginInfo(string loginProviderName, string providerUserId) {
                        return objectSpace.FirstOrDefault<FreeWebApiSecurity.WebApi.BusinessObjects.ApplicationUserLoginInfo>(userLoginInfo =>
                                            userLoginInfo.LoginProviderName == loginProviderName &&
                                            userLoginInfo.ProviderUserKey == providerUserId);
                    }
                }
            };
        });
        const string customBearerSchemeName = "CustomBearer";
        var authentication = services.AddAuthentication(customBearerSchemeName);
        authentication
            .AddJwtBearer(customBearerSchemeName, options => {
                options.TokenValidationParameters = new TokenValidationParameters() {
                    ValidateIssuerSigningKey = true,
                    //ValidIssuer = Configuration["Authentication:Jwt:Issuer"],
                    //ValidAudience = Configuration["Authentication:Jwt:Audience"],
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Authentication:Jwt:IssuerSigningKey"]))
                };
            });
        //Configure OAuth2 Identity Providers based on your requirements. For more information, see
        //https://docs.devexpress.com/eXpressAppFramework/402197/task-based-help/security/how-to-use-active-directory-and-oauth2-authentication-providers-in-blazor-applications
        //https://developers.google.com/identity/protocols/oauth2
        //https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-oauth2-auth-code-flow
        //https://developers.facebook.com/docs/facebook-login/manually-build-a-login-flow
        authentication.AddMicrosoftIdentityWebApi(Configuration, configSectionName: "Authentication:AzureAd");

        services.AddAuthorization(options => {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme,
                customBearerSchemeName)
                    .RequireAuthenticatedUser()
                    .RequireXafAuthentication()
                    .Build();
        });
        services.AddDbContextFactory<FreeWebApiSecurity.WebApi.BusinessObjects.FreeWebApiSecurityEFCoreDbContext>((serviceProvider, options) => {
            // Uncomment this code to use an in-memory database. This database is recreated each time the server starts. With the in-memory database, you don't need to make a migration when the data model is changed.
            // Do not use this code in production environment to avoid data loss.
            // We recommend that you refer to the following help topic before you use an in-memory database: https://docs.microsoft.com/en-us/ef/core/testing/in-memory
            //options.UseInMemoryDatabase("InMemory");
            string connectionString = Configuration.GetConnectionString("ConnectionString");
            options.UseSqlServer(connectionString);
            options.UseLazyLoadingProxies();
            options.UseSecurity(serviceProvider);
        }, ServiceLifetime.Scoped);

        services
            .AddXafWebApi(Configuration, options => {
                // Use options.BusinessObject<YourBusinessObject>() to make the Business Object available in the Web API and generate the GET, POST, PUT, and DELETE HTTP methods for it.
                options.BusinessObject<Post>();
            });
        services
            .AddControllers()
            .AddOData((options, serviceProvider) => {
                options
                    .AddRouteComponents("api/odata", new EdmModelBuilder(serviceProvider).GetEdmModel())
                    .EnableQueryFeatures(100);
            });

        services.AddSwaggerGen(c => {
            c.EnableAnnotations();
            c.SwaggerDoc("v1", new OpenApiInfo {
                Title = "FreeWebApiSecurity API",
                Version = "v1",
                Description = @"Use AddXafWebApi(options) in the FreeWebApiSecurity.WebApi\Startup.cs file to make Business Objects available in the Web API."
            });
            c.AddSecurityDefinition("JWT", new OpenApiSecurityScheme() {
                Type = SecuritySchemeType.Http,
                Name = "Bearer",
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme() {
                            Reference = new OpenApiReference() {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "JWT"
                            }
                        },
                        new string[0]
                    },
            });
            var azureAdAuthorityUrl = $"{Configuration["Authentication:AzureAd:Instance"]}{Configuration["Authentication:AzureAd:TenantId"]}";
            c.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows() {
                    AuthorizationCode = new OpenApiOAuthFlow() {
                        AuthorizationUrl = new Uri($"{azureAdAuthorityUrl}/oauth2/v2.0/authorize"),
                        TokenUrl = new Uri($"{azureAdAuthorityUrl}/oauth2/v2.0/token"),
                        Scopes = new Dictionary<string, string> {
                            // Configure scopes corresponding to https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-configure-app-expose-web-apis
                            { @"[Enter the scope name in the FreeWebApiSecurity.WebApi\Startup.cs file]", @"[Enter the scope description in the FreeWebApiSecurity.WebApi\Startup.cs file]" }
                        }
                    }
                }
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement() {
                {
                    new OpenApiSecurityScheme {
                        Name = "OAuth2",
                        Scheme = "OAuth2",
                        Reference = new OpenApiReference {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "OAuth2"
                        },
                        In = ParameterLocation.Header
                    },
                    new string[0]
                }
            });
        });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
        if(env.IsDevelopment()) {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "FreeWebApiSecurity WebApi v1");
                c.OAuthClientId(Configuration["Authentication:AzureAd:ClientId"]);
                c.OAuthUsePkce();
            });
        }
        else {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. To change this for production scenarios, see: https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseHttpsRedirection();
        app.UseRequestLocalization();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
        });
    }
}
