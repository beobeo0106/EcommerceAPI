using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceAPI.Data;
using EcommerceAPI.Models;
using Microsoft.AspNetCore.Http; // <-- Thêm dòng này để xử lý IFormFile
using System.IO;                 // <-- Thêm dòng này để xử lý lưu file vào thư mục

namespace EcommerceAPI.Controllers
{
    [Route("api/v1/[controller]")] // Route sẽ là: /api/v1/products
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Inject Database Context vào Controller
        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/v1/products
        // Lấy danh sách tất cả sản phẩm (Ai cũng xem được)
        // GET: api/v1/products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            // THÊM .Include(p => p.Images) ĐỂ LẤY KÈM ẢNH (Lưu ý: "Images" là tên thuộc tính List<ProductImage> trong model Product của bạn. Nếu bạn đặt tên khác thì đổi lại cho đúng nhé)
            var products = await _context.Products
                                         .Include(p => p.Images) // Join sang bảng ProductImages
                                         .ToListAsync();
            return products;
        }

        // GET: api/v1/products/5
        // Lấy chi tiết 1 sản phẩm theo ID (Ai cũng xem được)
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm!" });
            }

            return product;
        }

        // POST: api/v1/products
        // Thêm một sản phẩm mới (CHỈ ADMIN)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Product>> PostProduct(Product product)
        {
            // Thiết lập ngày tạo
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // Trả về HTTP 201 Created kèm theo thông tin sản phẩm vừa tạo
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        // PUT: api/v1/products/5
        // Cập nhật thông tin sản phẩm (CHỈ ADMIN)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutProduct(int id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest(new { message = "ID sản phẩm không khớp!" });
            }

            // Cập nhật ngày chỉnh sửa
            product.UpdatedAt = DateTime.UtcNow;

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(id))
                {
                    return NotFound(new { message = "Không tìm thấy sản phẩm!" });
                }
                else
                {
                    throw;
                }
            }

            return NoContent(); // Cập nhật thành công không cần trả về body
        }

        // DELETE: api/v1/products/5
        // Xóa một sản phẩm (CHỈ ADMIN)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Không tìm thấy sản phẩm!" });
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa sản phẩm thành công!" });
        }

        // ==========================================
        // ĐÂY LÀ HÀM UPLOAD ẢNH MỚI ĐƯỢC THÊM VÀO
        // ==========================================
        // POST: api/v1/products/{productId}/images
        [HttpPost("{productId}/images")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadProductImage(int productId, IFormFile file, [FromForm] bool isDefault = false)
        {
            // 1. Kiểm tra sản phẩm có tồn tại không
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound(new { message = "Không tìm thấy sản phẩm!" });

            // 2. Kiểm tra file có hợp lệ không
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn một file ảnh!" });

            // Chỉ cho phép ảnh (đuôi jpg, png, jpeg...)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Chỉ chấp nhận file ảnh (.jpg, .png, .jpeg, .gif)" });

            // 3. Tạo thư mục lưu trữ nếu chưa có (Lưu vào wwwroot/uploads/products)
            var folderName = Path.Combine("wwwroot", "uploads", "products");
            if (!Directory.Exists(folderName))
            {
                Directory.CreateDirectory(folderName);
            }

            // 4. Tạo tên file mới để không bị trùng (dùng GUID)
            var fileName = $"{Guid.NewGuid()}{extension}";
            var exactPath = Path.Combine(Directory.GetCurrentDirectory(), folderName, fileName);

            // 5. Copy file từ request vào máy chủ
            using (var stream = new FileStream(exactPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 6. Lưu đường dẫn vào Database
            var productImage = new ProductImage
            {
                ProductId = productId,
                ImageUrl = $"/uploads/products/{fileName}", // Đường dẫn để web/app tải ảnh
                IsDefault = isDefault,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductImages.Add(productImage);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Upload ảnh thành công!", imageUrl = productImage.ImageUrl });
        }
        // ==========================================

        // Hàm hỗ trợ kiểm tra sản phẩm có tồn tại không
        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}