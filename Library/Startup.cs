using System.Linq;
using Library.API.Entities;
using Library.API.Swagger;
using Library.API.Model;
using Library.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;

namespace Library
{
    public class Startup
    {
        public static IConfiguration Configuration;
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(setupAction =>
                {
                    setupAction.ReturnHttpNotAcceptable = true;
                    setupAction.OutputFormatters.Add(new XmlDataContractSerializerOutputFormatter());
                    setupAction.InputFormatters.Add(new XmlDataContractSerializerInputFormatter());
                    var jsonOutputFormatter = setupAction.OutputFormatters
                        .OfType<JsonOutputFormatter>().FirstOrDefault();

                    if (jsonOutputFormatter != null)
                    {
                        jsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.marvin.hateoas+json");
                    }
                })
                .AddJsonOptions(opt =>
                {
                    opt.SerializerSettings.ContractResolver =
                        new CamelCasePropertyNamesContractResolver();
                });


            var connectionString = Configuration["connectionStrings:libraryDBConnectionString"];
            services.AddDbContext<LibraryContext>(o => o.UseSqlServer(connectionString));

            
            services.AddScoped<ILibraryRepository, LibraryRepository>();
            // Add singleton bc we use it first time it was requested, if we used e.g. addScoped action context will be null 
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            //url helper will generate helpers for an action, but it needs a context
            //in which the action run so we had to add ActionContextAccessor before
            services.AddScoped<IUrlHelper, UrlHelper>(implementationFactory =>
            {
                var actionContext = implementationFactory.GetService<IActionContextAccessor>().ActionContext;
                return new UrlHelper(actionContext);
            });

            services.AddTransient<ITypeHelperService, TypeHelperService>();
            services.AddHttpCacheHeaders(expirationHeader =>
                {
                    expirationHeader.MaxAge = 30;
                },
                validateModelOption =>
            {
                validateModelOption.MustRevalidate = true;

            });
            services.AddResponseCaching();
            services.AddSwag();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory, LibraryContext libraryContext)
        { 

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        context.Response.StatusCode=500;
                        await context.Response.WriteAsync("Fault happened");                        
                    });
                });
            }

            AutoMapper.Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<Author, AuthorDto>()
                    .ForMember(dest => dest.Name, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));

                cfg.CreateMap<Book, BookDto>();
                cfg.CreateMap<AuthorForCreationDto, Author>();
                cfg.CreateMap<BookForCreationDto, Book>();
                cfg.CreateMap<BookForUpdateDto, Book>();
                cfg.CreateMap<Book, BookForUpdateDto>();
            });
            
            libraryContext.EnsureSeedDataForContext();
            app.UseResponseCaching();
            app.UseHttpCacheHeaders(); 
            app.UseMvc();
            app.UseSwag();
        }
    }
}
