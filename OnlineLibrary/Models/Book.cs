namespace OnlineLibrary.Models
{
    public class Book
    {
        public string BookID { get; set; } // Primary Key, changed to int
        public string Title { get; set; }
        public string Author { get; set; }
        public int? PublicationYear { get; set; }
        public decimal Price { get; set; }
        public decimal? PricePaid { get; set; }
        public decimal? BorrowPrice { get; set; }
        public decimal? Discount { get; set; }

        public int CopiesAvailableRent { get; set; }
        public int CopiesAvailable { get; set; }
        public string AgeRestriction { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public string ImageUrl { get; set; } // New property for image URL
        public string BookUrl { get; set; } // New property for image URL
     

        // New attributes for rental
        public DateTime LoanDate { get; set; } // Date the book was loaned
        public DateTime EndDate { get; set; } // Date the loan ends


    }
}


