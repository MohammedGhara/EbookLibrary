using System.ComponentModel.DataAnnotations;

namespace OnlineLibrary.Models
{
    public class payment2
    {
        [Required(ErrorMessage = "Card number is required.")]
        [StringLength(16, MinimumLength = 16, ErrorMessage = "Card number must be 16 digits.")]
        [RegularExpression("^[0-9]{16}$", ErrorMessage = "Card number must be numeric and exactly 16 digits.")]
        public string cardnum { get; set; }

        [Required(ErrorMessage = "Cardholder name is required.")]
        [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "Cardholder name must only contain letters and spaces.")]
        public string cardholder { get; set; }

        [Required(ErrorMessage = "Expiration date is required.")]
        [DataType(DataType.Date)]
        [CustomValidation(typeof(ExpiryDateValidator), "ValidateExpiryDate", ErrorMessage = "Expiration date must be in the future.")]
        public string expdate { get; set; }

        [Required(ErrorMessage = "CVV is required.")]
        [StringLength(3, MinimumLength = 3, ErrorMessage = "CVV must be exactly 3 digits.")]
        [RegularExpression("^[0-9]{3}$", ErrorMessage = "CVV must be numeric and exactly 3 digits.")]
        public string cvv { get; set; }
    }
    public static class ExpiryDateValidator
    {
        public static ValidationResult ValidateExpiryDate(string expdate, ValidationContext context)
        {
            if (string.IsNullOrEmpty(expdate))
            {
                return new ValidationResult("Expiration date is required.");
            }

            // Handle MM/YY format
            var dateParts = expdate.Split('/');
            if (dateParts.Length == 2 &&
                int.TryParse(dateParts[0], out int month) &&
                int.TryParse(dateParts[1], out int year))
            {
                year += 2000; // Convert YY to YYYY (e.g., 30 -> 2030)
                var expiryDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1); // Last day of the month

                if (expiryDate > DateTime.Now)
                {
                    return ValidationResult.Success;
                }
                else
                {
                    return new ValidationResult("Expiration date must be in the future.");
                }
            }

            return new ValidationResult("Invalid expiration date format. Use MM/YY.");
        }
    }

}

