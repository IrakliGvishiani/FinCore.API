using CloudinaryDotNet;
using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Mapping;
using FinCore.Application.Models.Cloudinary;
using FinCore.Application.Models.ExchangeDtos;
using FinCore.Application.Models.SuspiciousActivity;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Infrastructure;
using FinCore.Infrastructure.Persistence;
using FinCore.Infrastructure.Seed;
using FinCore.Middleware;
using FinCore.Others;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.Filters;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;


namespace FinCore
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services
                .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter());
            });

            //SQL
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
                .EnableDetailedErrors()
                .LogTo(Console.WriteLine, LogLevel.Information) 
                );
            

            // MAPSTER 
            var config = TypeAdapterConfig.GlobalSettings;
            config.Scan(typeof(MappingConfiguration).Assembly);

            builder.Services.AddSingleton(config);
            builder.Services.AddScoped<IMapper, ServiceMapper>();

            //Repository
            builder.Services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IManagerRepository, ManagerRepository>();
            builder.Services.AddScoped<ISavedRecipientRepository, SavedRecipientRepository>();
            builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

            //Service
            builder.Services.AddScoped<ITransferService, TransferService>();
            builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IRedisService, RedisService>();
            builder.Services.AddScoped<ISmtpClientWrapper, SmtpClientWrapper>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<CommonResponse>();
            builder.Services.AddScoped<IProfileService, ProfileService>();
            builder.Services.AddScoped<ICloudinaryImageService, CloudinaryImageService>();
            builder.Services.AddScoped<IConversionService, ConversionService>();
            builder.Services.AddScoped<IAccountStatementService,AccountStatementService>();
            builder.Services.AddScoped<IAuditService, AuditService>();
            builder.Services.AddScoped<IUserManagementService, UserManagementService>();
            builder.Services.AddScoped<IUserBanCache, UserBanCache>();
            builder.Services.AddScoped<IManagerService, ManagerService>();
            builder.Services.Configure<SuspiciousActivityOptions>(
            builder.Configuration.GetSection(SuspiciousActivityOptions.SectionName));
            builder.Services.AddScoped<ISuspiciousActivityDetectionService, SuspiciousActivityDetectionService>();
            builder.Services.AddScoped<ISavedRecipientService, SavedRecipientService>();
            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
            builder.Services.AddScoped<IVirtualCardService, VirtualCardService>();
            builder.Services.AddScoped<IReceiptService, ReceiptService>();
            builder.Services.AddScoped<ISimulatedPaymentService,SimulatedPaymentService>();

            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient<IExchangeRateProvider, NbgExchangeRateProvider>(c =>
            {
                c.BaseAddress = new Uri("https://nbg.gov.ge/");
                c.Timeout = TimeSpan.FromSeconds(5);
            });

            builder.Services.Configure<ConversionOptions>(
            builder.Configuration.GetSection(ConversionOptions.SectionName));
            builder.Services.AddScoped<IFxPricingService, FxPricingService>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

            builder.Services.Configure<AdminSeedOptions>(
            builder.Configuration.GetSection(AdminSeedOptions.SectionName));

            /// SWAGGER
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "My API",
                    Version = "v1",
                    Description = "This is my API description"
                });


                options.ExampleFilters();


                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste your token here"
                });

                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            });

            builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();

            // CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AngularApp", policy =>
                {
                    var frontendUrl = builder.Configuration["AppSettings:FrontendBaseUrl"]
                                      ?? "http://localhost:4200";

                    policy
                        .WithOrigins(frontendUrl.TrimEnd('/'))
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            // IDENTITY
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 10;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                options.Lockout.AllowedForNewUsers = true;


                options.User.RequireUniqueEmail = true;
            }).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

            // Redis
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = builder.Configuration["Redis:ConnectionString"];

                return ConnectionMultiplexer.Connect(configuration);
            });


            // AUTHENTICATION
            var secret = builder.Configuration.GetValue<string>("JWT:Secret");
            var issuer = builder.Configuration.GetValue<string>("JWT:Issuer"); ;
            var audience = builder.Configuration.GetValue<string>("JWT:Audience");
            var key = Encoding.ASCII.GetBytes(secret);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // Rejects still-valid access tokens of users banned after the token was issued.
                    // Fails open if Redis is unavailable (see UserBanCache.IsBannedAsync).
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                        if (string.IsNullOrEmpty(userId))
                            return;

                        var banCache = context.HttpContext.RequestServices
                            .GetRequiredService<IUserBanCache>();

                        if (await banCache.IsBannedAsync(userId))
                            context.Fail("User is banned.");
                    }
                };
            });

            // CLOUDINARY
            builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("Cloudinary"));
            var cloudinarySettings = builder.Configuration.GetSection("Cloudinary").Get<CloudinarySettings>();
            var account = new Account(cloudinarySettings.CloudName, cloudinarySettings.ApiKey, cloudinarySettings.ApiSecret);

            var cloudinary = new Cloudinary(account)
            {
                Api = { Secure = true }
            };


            builder.Services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownNetworks.Clear();
                o.KnownProxies.Clear();
            });

            builder.Services.AddSingleton(cloudinary);


            var app = builder.Build();
            app.MapGet("/health", () => Results.Ok("Healthy"));

            // Configure the HTTP request pipeline.
            app.UseForwardedHeaders();
            app.UseMiddleware<ErrorHandlingMiddleware>();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseHttpsRedirection();
            app.UseCors("AngularApp");
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();
        
            try
            {
                using (var scope = app.Services.CreateScope())
                {
                    var sp = scope.ServiceProvider;
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();


                    await db.Database.MigrateAsync();
                    await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);
                    await AdminSeeder.SeedAsync(sp);
                    await IdentitySeeder.SeedLedgerAccountsAsync(db);
                }
            }
            catch(Microsoft.Data.SqlClient.SqlException ex)
            {
                Console.WriteLine($"SQL Error Number: {ex.Number}");
                Console.WriteLine($"SQL Error Message: {ex.Message}");
                Console.WriteLine($"SQL Procedure: {ex.Procedure}");
                Console.WriteLine($"SQL Line Number: {ex.LineNumber}");

                throw;
            }

            app.Run();
        }
    }
}
