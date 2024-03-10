using DoMyThingWorker.BGServices;
using DoMyThingWorker.Models;
using DoMyThingWorker.Processors;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<VFSInspector>();
builder.Services.AddTransient<IProcessor<FindVFSAppointmentModel, FindVFSAppointmentResponseModel>, FindVFSAppointmentSlotProcessor>();
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();