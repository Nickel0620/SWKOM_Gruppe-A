using DAL.Context;
using DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using REST_API.MappingProfiles;
using REST_API.DTOs;
using FluentValidation;
using FluentValidation.AspNetCore;
using REST_API.Services;
using System.Diagnostics.CodeAnalysis;

namespace REST_API
{
    [ExcludeFromCodeCoverage]
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Register AutoMapper
            builder.Services.AddAutoMapper(typeof(DocumentProfile));

            // Register FluentValidation
            builder.Services.AddFluentValidationAutoValidation()
                            .AddFluentValidationClientsideAdapters();

            builder.Services.AddValidatorsFromAssemblyContaining<DocumentDTOValidator>();

            // Add services to the container.

            // Add CORS to the services
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    });
            });

            // Register DbContext to use PostgreSQL with the DocumentContext from DAL
            builder.Services.AddDbContext<DocumentContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Register the repository from DAL
            builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();



            builder.Services.AddHttpClient("DAL", client =>
            {
                client.BaseAddress = new Uri("http://dal:8081");
            });



            // Add controllers
            builder.Services.AddControllers();

            builder.Services.AddSingleton<IMessageQueueService, MessageQueueService>();
            builder.Services.AddHostedService<RabbitMqListenerService>();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Ensure the database and the Documents table are created
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DocumentContext>();
                dbContext.Database.EnsureCreated();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "V1");
                });
            }

            //app.UseHttpsRedirection();

            // Enable CORS
            app.UseCors("AllowAllOrigins");

            app.Urls.Add("http://*:8080");

            app.UseAuthorization();

            app.MapControllers();

            app.Run();

        }
    }
}

