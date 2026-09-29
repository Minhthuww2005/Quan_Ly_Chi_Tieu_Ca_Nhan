var builder = WebApplication.CreateBuilder(args);

// TODO(A): đăng ký DI, middleware, auth, EF Core và routes tại đây.

var app = builder.Build();

// TODO(A): cấu hình pipeline đúng thứ tự theo checklist 1A.1.

app.MapGet("/", () => "Revenue Management API skeleton");
app.Run();

public partial class Program { }

