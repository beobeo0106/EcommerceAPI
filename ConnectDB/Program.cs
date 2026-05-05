using Microsoft.EntityFrameworkCore;
using EcommerceAPI.Data;
// THƯ VIỆN BẮT BUỘC CHO JWT
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
// THƯ VIỆN BẮT BUỘC ĐỂ TẠO Ổ KHÓA TRÊN SWAGGER
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. ĐĂNG KÝ SERVICES CƠ BẢN
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddEndpointsApiExplorer();
// ==========================================
// THÊM MỚI: CẤU HÌNH CORS (CHO PHÉP FRONTEND TRUY CẬP)
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()   // Cho phép tất cả các nguồn (Localhost, Vercel...)
              .AllowAnyMethod()   // Cho phép GET, POST, PUT, DELETE
              .AllowAnyHeader();  // Cho phép tất cả các Header (bao gồm Token)
    });
});

// ==========================================
// ĐÃ SỬA: CẤU HÌNH SWAGGER ĐỂ CÓ NÚT NHẬP TOKEN
// ==========================================
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập token JWT theo chuẩn: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// CẤU HÌNH KẾT NỐI DATABASE (SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));


// ==========================================
// CẤU HÌNH BẢO MẬT JWT 
// ==========================================
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});
// ==========================================


var app = builder.Build();

// 2. HTTP REQUEST PIPELINE (MIDDLEWARE)

// ĐÃ SỬA: Xóa bỏ lệnh if để Swagger hiện ra trên Somee
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");

app.UseHttpsRedirection();
app.UseStaticFiles();

// KIỂM TRA ĐĂNG NHẬP   
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();