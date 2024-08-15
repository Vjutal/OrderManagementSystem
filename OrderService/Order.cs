using System.ComponentModel.DataAnnotations;

namespace Models;

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
}