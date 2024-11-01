using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

const string SECRET_KEY =
    "eyJhbGciOiJIUzI1NiJ9.ew0KICAic3ViIjogIjEyMzQ1Njc4OTAiLA0KICAibmFtZSI6ICJBbmlzaCBOYXRoIiwNCiAgImlhdCI6IDE1MTYyMzkwMjINCn0.crF9lwWoybaNLZpPxe7V9ADc_FNpnQHWnI8rVrFyFm0";
var KEY = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SECRET_KEY));

var builder = WebApplication.CreateBuilder(args);

var todos = new List<Todo>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = KEY
        };
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/login", (LoginRequest model) =>
{
    var claims = new[]
    {
        new Claim(ClaimTypes.Name, model.Username)
    };

    var creds = new SigningCredentials(KEY, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: "todoappapi",
        audience: "todoappclient",
        claims: claims,
        expires: DateTime.Now.AddMinutes(30),
        signingCredentials: creds
    );

    for (int i = 1; i <= 3; i++)
    {
        todos.Add(new Todo
        {
            CreatedBy = model.Username,
            CreatedAt = DateTime.Now.AddHours(-i).AddMinutes(-17 * i).AddSeconds(31 * i),
            Title = $"{model.Username}'s example ToDo task #{i}"
        });
    }

    return Results.Ok(new
    {
        token = new JwtSecurityTokenHandler().WriteToken(token)
    });
});

app.MapGet("/todos", (HttpContext context) =>
{
    var user = context.User.Identity.Name;
    return todos.Where(t => t.CreatedBy == user).OrderBy(e => e.Done).Select(e => new TodoDto(e));
}).RequireAuthorization();

app.MapPost("/todos", (TodoDto todo, HttpContext context) =>
{
    var newTodo = new Todo(todo, context.User.Identity.Name);
    todos.Add(newTodo);
    return Results.Created($"/todos/{todos.IndexOf(newTodo)}", new TodoDto(newTodo));
}).RequireAuthorization();

app.MapDelete("/todos/{id}", (int id) =>
{
    var todo = todos.SingleOrDefault(e => e.Id == id);
    if (todo is not null)
    {
        todos.Remove(todo);
        return Results.NoContent();
    }

    return Results.NotFound();
}).RequireAuthorization();

app.MapPut("/todos/{id}/toggle", (int id) =>
{
    var todo = todos.SingleOrDefault(e => e.Id == id);
    if (todo is null)
    {
        return Results.NotFound();
    }

    todo.Done = !todo.Done;
    return Results.Ok(new TodoDto(todo));
}).RequireAuthorization();

app.Run();

public record Todo
{
    private static int _id = 0;
    public int Id = _id++;
    public string Title { get; init; }
    public string CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool Done { get; set; }

    public Todo(TodoDto todo, string user)
    {
        Title = todo.Title;
        CreatedAt = DateTime.Now;
        CreatedBy = user;
        Done = todo.Done;
    }

    public Todo(string title, string createdBy, DateTime createdAt, bool done)
    {
        Title = title;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Done = done;
    }

    public Todo()
    {
    }
}

public record TodoDto
{
    public int Id { get; init; }
    public string Title { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool Done { get; init; }

    public TodoDto(Todo todo)
    {
        Id = todo.Id;
        Title = todo.Title;
        CreatedAt = todo.CreatedAt;
        Done = todo.Done;
    }

    public TodoDto(string title, DateTime createdAt, bool done)
    {
        Title = title;
        CreatedAt = createdAt;
        Done = done;
    }

    public TodoDto()
    {
    }
}

public record LoginRequest(string Username);
