using Microsoft.EntityFrameworkCore;
using BlogApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework
builder.Services.AddDbContext<BlogContext>(options =>
    options.UseInMemoryDatabase("BlogDb"));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Seed some initial data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogContext>();
    
    if (!context.Users.Any())
    {
        context.Users.AddRange(
            new User { Name = "John Doe", Email = "john@example.com" },
            new User { Name = "Jane Smith", Email = "jane@example.com" }
        );
        await context.SaveChangesAsync();
    }
}

app.Run();

// Make the Program class available for testing
public partial class Program { }