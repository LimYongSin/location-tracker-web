using System.Net.Http.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// ...existing code...
builder.Services.AddHttpClient();
// ...existing code...
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// Reverse geocoding endpoint
app.MapPost("/api/location", async (LocationRequest req, HttpClient http) =>
// ...existing code...
{
    // Replace with your OpenCage API key
    var apiKey = "40dce78c33564ab8afec0e56439ce259";
    var url = $"https://api.opencagedata.com/geocode/v1/json?q={req.Latitude}+{req.Longitude}&key={apiKey}&no_annotations=1&language=en";
    var response = await http.GetFromJsonAsync<OpenCageResponse>(url);
    var place = response?.results?.FirstOrDefault()?.formatted ?? "Unknown location";
    return Results.Ok(new { place });
});

// IP-based geolocation endpoint
app.MapPost("/api/location/ip", async (HttpContext context, HttpClient http) =>
{
    var ip = context.Connection.RemoteIpAddress?.ToString();
    // For local testing, fallback to a public IP
    if (string.IsNullOrWhiteSpace(ip) || ip == "::1" || ip == "127.0.0.1")
        ip = "8.8.8.8";
    var url = $"http://ip-api.com/json/{ip}";
    var response = await http.GetFromJsonAsync<IpApiResponse>(url);
    var place = response?.status == "success" ? $"{response.city}, {response.regionName}, {response.country}" : "Unknown location";
    return Results.Ok(new { place });
});

// Serve frontend HTML
app.MapGet("/index.html", () => Results.Content(@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Location Tracker</title>
  <style>body{font-family:sans-serif;margin:2em;}#result{margin-top:1em;font-size:1.2em;}</style>
</head>
<body>
  <h1>Location Tracker</h1>
  <button id=""locateBtn"">Get My Location</button>
  <div id=""result""></div>
  <script>
    const btn = document.getElementById('locateBtn');
    const result = document.getElementById('result');
    btn.onclick = () => {
      result.textContent = 'Detecting location...';
      if (navigator.geolocation) {
        navigator.geolocation.getCurrentPosition(async pos => {
          const lat = pos.coords.latitude;
          const lon = pos.coords.longitude;
          const res = await fetch('/api/location', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ latitude: lat, longitude: lon })
          });
          const data = await res.json();
          result.textContent = 'You are in: ' + data.place;
        }, async err => {
          // Fallback to IP-based
          const res = await fetch('/api/location/ip', { method: 'POST' });
          const data = await res.json();
          result.textContent = 'You are in: ' + data.place + ' (IP-based)';
        });
      } else {
        result.textContent = 'Geolocation not supported.';
      }
    };
  </script>
</body>
</html>", "text/html"));

app.Run();

// Minimal DTOs
record LocationRequest([property: JsonPropertyName("latitude")] double Latitude, [property: JsonPropertyName("longitude")] double Longitude);

class OpenCageResponse
{
    public List<OpenCageResult>? results { get; set; }
}
class OpenCageResult
{
    public string? formatted { get; set; }
}
class IpApiResponse
{
    public string? status { get; set; }
    public string? city { get; set; }
    public string? regionName { get; set; }
    public string? country { get; set; }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
