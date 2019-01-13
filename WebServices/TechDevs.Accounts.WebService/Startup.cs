﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TechDevs.Accounts.Middleware;
using TechDevs.Accounts.Repositories;
using TechDevs.Clients;
using TechDevs.Clients.Theme;
using TechDevs.Mail;
using TechDevs.MyVehicles;
using TechDevs.NotificationPreferences;
using TechDevs.Shared.Models;
using TechDevs.Shared.Utils;
using TechDevs.MarketingPreferences;
using TechDevs.Clients.Offers;
using Audit.WebApi;
using Audit.Core;
using Microsoft.AspNetCore.Http.Internal;
using TechDevs.Products;

namespace TechDevs.Accounts.WebService
{
    public class Startup
    {
        private readonly IHostingEnvironment _env;

        public Startup(IConfiguration configuration, IHostingEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            MongoDBConfig.Configure();

            services.AddCors(options =>
            {
                // this defines a CORS policy called "default"
                options.AddPolicy("default", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("TechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKey")),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });

            // Repositories
            services.AddTransient<IClientRepository, ClientRepository>();
            services.AddTransient<IAuthUserRepository<Customer>, CustomerRepository>();
            services.AddTransient<IAuthUserRepository<Employee>, EmployeeRepository>();

            // Services
            services.AddTransient<IClientService, ClientService>();
            services.AddTransient<IClientThemeService, ClientThemeService>();
            services.AddTransient<IMyVehicleService, MyVehicleService>();
            services.AddTransient<IAuthUserService<Customer>, CustomerService>();
            services.AddTransient<IAuthUserService<Employee>, EmployeeService>();
            services.AddTransient<IAuthTokenService<Customer>, AuthTokenService<Customer>>();
            services.AddTransient<IAuthTokenService<Employee>, AuthTokenService<Employee>>();
            services.AddTransient<INotificationPreferencesService, NotificationPreferencesService>();
            services.AddTransient<IMarketingPreferencesService, MarketingPreferencesService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddTransient<IBasicOffersService, BasicOffersService>();
            services.AddTransient<IProductsService, ProductsService>();
            services.AddTransient<IProductsRepository, ProductsRepository>();
            services.AddTransient<IClientSubscriptionService, ClientSubscriptionService>();

            // Utils
            services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
            services.AddTransient<IStringNormaliser, UpperStringNormaliser>();
            services.AddTransient<IEmailer, DotNetEmailer>();

            // Configure the passowrd hashing algorithm
            services.Configure<BCryptPasswordHasherOptions>(options =>
            {
                options.WorkFactor = 10;
                options.EnhancedEntropy = false;
            });

            // Combine the secrets with the app config             
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<SMTPSettings>()
                .AddUserSecrets<MongoDbSettings>()
                .AddUserSecrets<AppSettings>()
                .AddEnvironmentVariables();
            // Configure
            services.Configure<SMTPSettings>(Configuration.GetSection(nameof(SMTPSettings)));
            services.Configure<MongoDbSettings>(Configuration.GetSection(nameof(MongoDbSettings)));

            if (_env.IsDevelopment())
            {
                services.Configure<AppSettings>(config =>
                {
                    config.InvitationSiteRoot = "http://localhost:4200";
                });
            }
            else
            {
                services.Configure<AppSettings>(Configuration.GetSection(nameof(AppSettings)));
            }

            // Setup the API Audit DB
            Audit.Core.Configuration
                .Setup()
                .UseMongoDB(config => config
                    .ConnectionString(Configuration.GetSection(nameof(MongoDbSettings)).GetValue<string>("ConnectionString"))
                    .Database(Configuration.GetSection(nameof(MongoDbSettings)).GetValue<string>("Database"))
                    .Collection("APIAudit"));


            services.AddMvc(mvc =>
         {
             mvc.AddAuditFilter(config => config
                  .LogAllActions()
                 // .LogActionIf(x => true)
                 .WithEventType("{verb}.{controller}.{action}")
                 .IncludeHeaders(ctx => !ctx.ModelState.IsValid)
                 .IncludeRequestBody()
                 .IncludeModelState()
                 .IncludeResponseBody());
         });

            // services.AddMvc();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "User Profile API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new ApiKeyScheme { In = "header", Description = "Please enter JWT with Bearer into field", Name = "Authorization", Type = "apiKey" });
                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>> {
                    { "Bearer", Enumerable.Empty<string>() },
                });
                c.OperationFilter<TechDevsClientKeyHeaderFilter>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseCors("default");


            // Add Custom configuration for Audit API

            SensitiveInformation.Custom();

            app.Use(async (context, next) =>
            {  // <----
                context.Request.EnableRewind();
                await next();
            });



            // app.UseAuditMiddleware(_ => _
            //             .FilterByRequest(rq => !rq.Path.Value.EndsWith("favicon.ico"))
            //             .WithEventType("{verb}:{url}")
            //             .IncludeHeaders()
            //             .IncludeResponseHeaders()
            //             .IncludeRequestBody()
            //             .IncludeResponseBody());


            // app.UseAuditMiddleware(_ => _
            // .WithEventType("{verb}:{url}")
            // .IncludeHeaders()
            //              .IncludeResponseHeaders()
            //              .IncludeRequestBody()
            //              .IncludeResponseBody());






            if (env.IsDevelopment())
            {
                // app.UseDeveloperExceptionPage();
            }



            app.UseAuthentication();

            //app.UseMiddleware(typeof(ErrorHandlingMiddleware));
            app.UseMvc();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Profile API v1");
            });


        }
    }

    public static class SensitiveInformation
    {
        public static void Custom()
        {
            Audit.Core.Configuration.AddCustomAction(Audit.Core.ActionType.OnEventSaving, action =>
            {
                var mvc = action.Event.GetWebApiAuditAction();//.GetMvcAuditAction();

                if (mvc.ActionName.ToUpper() == "LOGIN")
                {
                    mvc.RequestBody.Value = null;
                    if (mvc.ActionParameters.ContainsKey("request"))
                    {
                        dynamic x = mvc.ActionParameters["request"];
                        mvc.ActionParameters["request"] = RemoveSensitiveData(x);
                    }
                }
            });
        }

        private static object RemoveSensitiveData(object vm)
        {
            return vm;
        }

        private static LoginRequest RemoveSensitiveData(LoginRequest vm)
        {
            vm.Password = "{RemovedSensitiveInformation}";
            return vm;
        }
    }
}