using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
namespace OnlineLibrary.Models
{

    public class CustomerModel
    {
        [Required(ErrorMessage = "The ID is required!")]
        [StringLength(9, MinimumLength = 9, ErrorMessage = "ID must be exactly 9 characters!")]
        public string ID { get; set; }
        [Required(ErrorMessage ="The FirstName is required!")]
        [StringLength(50,MinimumLength = 2,ErrorMessage ="FirstName must be between2 and 50 letters!")]
        public string firstname { get; set; }
        [Required]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last Name must be between 2 and 50 letters!")]

        public string lastname { get; set; }
        [Required(ErrorMessage = "Email is required!")]
        [EmailAddress(ErrorMessage = "Invalid email address!")]
        public string email { get; set; }
        [Required(ErrorMessage = "Password is required!")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
           ErrorMessage = "Password must be at least 8 characters long, include 1 uppercase, 1 lowercase, 1 number, and 1 special character.")]
        public string password { get; set; }
        [Required]
        public string address { get; set; }
        public string Type { get; set; }

        [Required(ErrorMessage = "Credit Card Number is required!")]
        [StringLength(16, MinimumLength = 16, ErrorMessage = "Credit Card Number must be exactly 16 characters!")]
        public string CreditCardNumber { get; set; }

        [Required(ErrorMessage = "Credit Card Valid Date is required!")]
        [StringLength(10, ErrorMessage = "Credit Card Valid Date cannot exceed 10 characters.")]
        public string CreditCardValidDate { get; set; }

        [Required(ErrorMessage = "Credit Card CVC is required!")]
        [StringLength(3, MinimumLength = 3, ErrorMessage = "Credit Card CVC must be exactly 3 characters!")]
        public string CreditCardCVC { get; set; }

        public List<Book> PurchaseBooks { get; set; } = new List<Book>();

        public List<Book> LoanedBook { get; set; } = new List<Book>();

        public List<Book> Cart { get; set; } = new List<Book>();





        public CustomerModel() { }

    }
}
