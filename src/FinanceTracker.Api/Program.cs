using FinanceTracker.Application;
using FinanceTracker.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Backs app.UseExceptionHandler() below: any exception that isn't already
// a Result failure -- a genuine bug, per ADR 0004 -- gets turned into a
// 500 ProblemDetails response that never leaks exception details, except
// in Development, where the exception's own ToString() is attached so
// local debugging still works without needing a debugger attached.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (!builder.Environment.IsDevelopment())
            return;

        var exception = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is not null)
            context.ProblemDetails.Extensions["exception"] = exception.ToString();
    };
});

var app = builder.Build();

app.UseExceptionHandler();

app.Run();

// Exposes the top-level Program for FinanceTracker.Api.IntegrationTests'
// WebApplicationFactory<Program> once that project exists.
public partial class Program;
