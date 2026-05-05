using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceAPI.Models;
using EcommerceAPI.Data;

namespace EcommerceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // 1. LẤY DANH SÁCH - Giữ nguyên
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            return await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                })
                .ToListAsync();
        }

        // 2. THÊM MỚI - Giữ nguyên
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                user.PasswordHash = "123456";
            }

            user.CreatedAt = DateTime.UtcNow;
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, user);
        }

        // 3. CẬP NHẬT (ĐÃ SỬA LỖI 400)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, UserUpdateDto userDto)
        {
            // Kiểm tra ID khớp
            if (id != userDto.Id) return BadRequest("ID mismatch!");

            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null) return NotFound();

            // Cập nhật thông tin cơ bản
            existingUser.Name = userDto.Name;
            existingUser.Email = userDto.Email;
            existingUser.Role = userDto.Role;

            // CHỈ cập nhật mật khẩu nếu người dùng nhập mật khẩu mới
            if (!string.IsNullOrEmpty(userDto.PasswordHash))
            {
                existingUser.PasswordHash = userDto.PasswordHash;
            }

            // Đánh dấu là đã thay đổi
            _context.Entry(existingUser).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 4. XOÁ - Giữ nguyên
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    // LỚP TRUNG GIAN ĐỂ NHẬN DỮ LIỆU TỪ REACT (Đặt ở cuối file)
    public class UserUpdateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Customer";
        public string? PasswordHash { get; set; } // Dấu ? cho phép trường này bị NULL mà không báo lỗi 400
    }
}