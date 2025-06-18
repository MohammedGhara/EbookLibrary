using System.ComponentModel.DataAnnotations;

namespace OnlineLibrary.Models
{
    public class AdminModel
    {
        [Required(ErrorMessage = "The Username is required!")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Username must be between2 and 50 letters!")]
        public string username { get; set; }
        [Required(ErrorMessage = "Email is required!")]
        [EmailAddress(ErrorMessage = "Invalid email address!")]
        public string email { get; set; }
        [Required(ErrorMessage = "password is required!")]
        public string password { get; set; }
    }
}
