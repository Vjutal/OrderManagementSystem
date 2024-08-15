using System.ComponentModel.DataAnnotations;

namespace OrderService.Data.Types;

public record Order
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string ProductName { get; set; } = default!;
    
    [Range(1, 1000)]
    public int Quantity { get; set; }
    
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }
    
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    public bool IsDeleted { get; set; }
}