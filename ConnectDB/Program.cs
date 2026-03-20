using Microsoft.EntityFrameworkCore;
using ConnectDB.Data; // Đảm bảo namespace này khớp với file AppDbContext.cs của bạn

var builder = WebApplication.CreateBuilder(args);

// 1. ĐĂNG KÝ SERVICES VÀO CONTAINER
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CHÈN ĐOẠN NÀY ĐỂ KẾT NỐI DATABASE ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// -----------------------------------------

var app = builder.Build();

// 2. CẤU HÌNH HTTP REQUEST PIPELINE (MIDDLEWARE)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Lưu ý: Nếu bạn có dùng Authentication/Authorization thì để ở đây
app.UseAuthorization();

app.MapControllers();

app.Run();