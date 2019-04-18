using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Library.API.Swagger
{
    public static class Swagger
    {
        public static IServiceCollection AddSwag(this IServiceCollection services) 
        {                                                                          
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "My API", Version = "v1" }); 
            });

            return services;
        }

        public static IApplicationBuilder UseSwag(this IApplicationBuilder app)
        {
            app.UseSwagger();       
            app.UseSwaggerUI(c =>   
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });

            return app;
        }
    }
}
