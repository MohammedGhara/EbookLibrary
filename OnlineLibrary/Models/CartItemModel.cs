namespace OnlineLibrary.Models;

public class CartItemModel
{
    public string? CartID { get; set; }
    public string? Username { get; set; }
    public string BookID { get; set; }
    public string Title { get; set; }
    public string ActionType { get; set; } // "Buy" or "Borrow"
    public int? Days { get; set; } // Nullable for "Buy"
    public int? Copies { get; set; } // Nullable for "Borrow"
    public DateTime? AddedAt { get; set; }
}

public class Cartdetails
{
    public string? CartID { get; set; }
  
}