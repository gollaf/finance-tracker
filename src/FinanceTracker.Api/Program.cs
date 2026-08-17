using FinanceTracker.Application;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddApplication();

var app = builder.Build();

app.Run();
