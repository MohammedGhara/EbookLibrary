using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OnlineLibrary.Models;
using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Data;
using System.Net;
using System.Net.Mail;


namespace OnlineLibrary.Controllers
{
    public class PaymentController : Controller
    {
        private PayPalEnvironment environment;
        private PayPalHttpClient client;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;


        public PaymentController(IConfiguration configuration)
        {
            _configuration = configuration;

            var clientId = configuration["PayPal:ClientId"];
            var clientSecret = configuration["PayPal:ClientSecret"];
            var isSandbox = configuration["PayPal:Environment"] == "sandbox";

            environment = isSandbox
                ? new SandboxEnvironment(clientId, clientSecret)
                : new LiveEnvironment(clientId, clientSecret);
            client = new PayPalHttpClient(environment);
        }

        public async Task<IActionResult> BorrowWithPayPal(decimal borrowAmount, int bookId)
        {
            var orderRequest = new OrderRequest()
            {
                CheckoutPaymentIntent = "CAPTURE",
                PurchaseUnits = new List<PurchaseUnitRequest>
                {
                    new PurchaseUnitRequest
                    {
                        AmountWithBreakdown = new AmountWithBreakdown
                        {
                            CurrencyCode = "USD",
                            Value = borrowAmount.ToString("F2")
                        }
                    }
                },
                ApplicationContext = new ApplicationContext
                {
                    ReturnUrl = Url.Action("BorrowSuccess", "Payment", new { bookId }, Request.Scheme),
                    CancelUrl = Url.Action("BorrowCancel", "Payment", null, Request.Scheme)
                }
            };

            var request = new OrdersCreateRequest();
            request.RequestBody(orderRequest);

            try
            {
                var response = await client.Execute(request);
                var result = response.Result<Order>();
                var approvalLink = result.Links.FirstOrDefault(link => link.Rel == "approve")?.Href;

                return Redirect(approvalLink);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"PayPal error: {ex.Message}";
                return RedirectToAction("payments");
            }
        }


        public async Task<IActionResult> BorrowSuccess(string token, int bookId)
        {
            var request = new OrdersCaptureRequest(token);

            try
            {
                var response = await client.Execute(request);

                // Update waiting list or borrow status after payment
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = "UPDATE Books SET WaitingListCount = WaitingListCount + 1 WHERE BookID = @BookID";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BookID", bookId);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                TempData["SuccessMessage"] = "Borrow payment completed successfully!";
                return RedirectToAction("Success");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"PayPal error: {ex.Message}";
                return RedirectToAction("payments");
            }
        }

        public IActionResult BorrowCancel()
        {
            TempData["ErrorMessage"] = "Borrow payment canceled by user.";
            return RedirectToAction("payments");
        }


        public async Task<IActionResult> PayWithPayPal(decimal amount)
        {
            var orderRequest = new OrderRequest()
            {
                CheckoutPaymentIntent = "CAPTURE",
                PurchaseUnits = new List<PurchaseUnitRequest>
                {
                    new PurchaseUnitRequest
                    {
                        AmountWithBreakdown = new AmountWithBreakdown
                        {
                            CurrencyCode = "USD",
                            Value = amount.ToString("F2")
                        }
                    }
                },
                ApplicationContext = new ApplicationContext
                {
                    ReturnUrl = Url.Action("PayPalSuccess", "Payment", null, Request.Scheme),
                    CancelUrl = Url.Action("PayPalCancel", "Payment", null, Request.Scheme)
                }
            };

            var request = new OrdersCreateRequest();
            request.RequestBody(orderRequest);

            try
            {
                var response = await client.Execute(request);
                var result = response.Result<Order>();
                var approvalLink = result.Links.FirstOrDefault(link => link.Rel == "approve")?.Href;

                return Redirect(approvalLink);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"PayPal error: {ex.Message}";
                return RedirectToAction("payments");
            }
        }

        public async Task<IActionResult> PayPalSuccess(string token, string title, int publicationYear)
        {
            var request = new OrdersCaptureRequest(token);

            try
            {
                var response = await client.Execute(request);

                // Decrease book copies after a successful payment
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = @"
                UPDATE Books 
                SET CopiesAvailable = CopiesAvailable - 1 
                WHERE Title = @Title AND PublicationYear = @PublicationYear AND CopiesAvailable > 0";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Title", title);
                        command.Parameters.AddWithValue("@PublicationYear", publicationYear);

                        connection.Open();
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            TempData["ErrorMessage"] = "Book is out of stock!";
                            return RedirectToAction("payments");
                        }
                    }
                }

                TempData["SuccessMessage"] = "Payment completed successfully!";
                return RedirectToAction("Success");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"PayPal error: {ex.Message}";
                return RedirectToAction("payments");
            }
        }


        public IActionResult PayPalCancel()
        {
            TempData["ErrorMessage"] = "Payment canceled by user.";
            return RedirectToAction("payments");
        }


        [HttpPost]
        public IActionResult ProcessPayment(
           string actionType,
           string BookID,
           int? days,
           string CreditCardNumber,
           string CreditCardValidDate,
           string CreditCardCVC)
        {
            // Get the logged-in user from the session
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                TempData["ErrorMessage"] = "User not logged in.";
                return RedirectToAction("Login", "Customer");
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            // 🟡 Update credit card details in DB (if needed)
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                string updateCardQuery = @"
            UPDATE Customers
            SET CreditCardNumber = @CreditCardNumber,
                CreditCardValidDate = @CreditCardValidDate,
                CreditCardCVC = @CreditCardCVC
            WHERE ID = @ID";

                using (SqlCommand command = new SqlCommand(updateCardQuery, connection))
                {
                    command.Parameters.AddWithValue("@CreditCardNumber", CreditCardNumber ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CreditCardValidDate", CreditCardValidDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CreditCardCVC", CreditCardCVC ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ID", loggedInUser.ID);
                    command.ExecuteNonQuery();
                }
            }

            // ✅ Proceed with book logic
            if (actionType == "Buy")
            {
                BuyBook(BookID);
            }
            else if (actionType == "Borrow")
            {
                BorrowBook(BookID, days);
            }

            TempData["SuccessMessage"] = "Payment completed successfully!";
            return RedirectToAction("DisplayBooks", "HomePage");
        }

        [HttpPost]
        public IActionResult CheckoutCart()
        {
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return Json(new { success = false, message = "User not logged in." });
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);
            var cartItems = new List<CartItemModel>();

            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // Fetch all cart items for the user
                var cartItemsQuery = "SELECT * FROM Cart WHERE Username = @Username";

                using (SqlCommand command = new SqlCommand(cartItemsQuery, connection))
                {
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cartItems.Add(new CartItemModel
                            {
                                CartID = reader.GetString(reader.GetOrdinal("CartID")),
                                BookID = reader.GetString(reader.GetOrdinal("BookID")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                ActionType = reader.GetString(reader.GetOrdinal("ActionType")),
                                Days = reader.IsDBNull(reader.GetOrdinal("Days"))
                                    ? null
                                    : reader.GetInt32(reader.GetOrdinal("Days")),
                                Copies = reader.IsDBNull(reader.GetOrdinal("Copies"))
                                    ? null
                                    : reader.GetInt32(reader.GetOrdinal("Copies")),
                                AddedAt = reader.GetDateTime(reader.GetOrdinal("AddedAt"))
                            });
                        }
                    }
                }

                // Process each cart item
                foreach (var item in cartItems)
                {
                    if (item.ActionType == "Buy")
                    {
                        BuyBook(item.BookID.ToString());
                    }
                    else if (item.ActionType == "Borrow")
                    {
                        BorrowBook(item.BookID.ToString(), item.Days);
                    }
                }

                // Clear the cart after checkout
                var clearCartQuery = "DELETE FROM Cart WHERE Username = @Username";
                using (SqlCommand command = new SqlCommand(clearCartQuery, connection))
                {
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    command.ExecuteNonQuery();
                }
            }

            // Send a confirmation email
            string emailSubject = "Your Checkout Confirmation";
            string emailBody =
                "<h1>Checkout Completed</h1><p>Thank you for your purchase/borrow. Below are the details:</p><ul>";

            foreach (var item in cartItems)
            {
                emailBody += $"<li><strong>{item.Title}</strong> - {item.ActionType} - " +
                             $"{(item.ActionType == "Buy" ? $"Copies: {item.Copies}" : $"Days: {item.Days}")}</li>";
            }

            emailBody += "</ul><p>We hope you enjoy your books!</p>";

            SendEmail(loggedInUser.email, emailSubject, emailBody);
            return Json(new { success = true, message = "Checkout completed successfully." });
        }

        private void SendEmail(string to, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient("smtp.gmail.com", 587))
                {
                    // Use credentials from configuration or environment variables
                    string _email = _configuration["EmailSettings:Email"];
                    string _password = _configuration["EmailSettings:Password"];

                    client.Credentials = new NetworkCredential(_email, _password);
                    client.EnableSsl = true;

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_email),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true // To ensure the email body supports HTML content
                    };
                    mailMessage.To.Add(to);

                    client.Send(mailMessage);
                }

                Console.WriteLine($"Email sent to: {to}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
            }
        }

        // This action just shows the payment page
        // GET: Payment/payments?amount=...&bookId=...&paymentType=Buy/Borrow
        public IActionResult payments(decimal? amount, int? bookId, string paymentType)
        {
            // Store amount in TempData
            if (amount.HasValue)
            {
                TempData["BookPrice"] = amount.Value.ToString("F2");
            }

            // Store book ID in TempData
            if (bookId.HasValue)
            {
                TempData["BookId"] = bookId.Value;
            }

            // Store the payment type in TempData
            TempData["PaymentType"] = string.IsNullOrWhiteSpace(paymentType) ? "Buy" : paymentType;

            // Keep TempData for next request
            TempData.Keep();

            // Pass to ViewBag for display on the page
            ViewBag.BookPrice = TempData["BookPrice"];
            ViewBag.BookId = TempData["BookId"];
            ViewBag.PaymentType = TempData["PaymentType"];

            // Return the payment view (payment.cshtml)
            return View(new payment2());
        }

        // POST: Payment/ProceedToPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProceedToPayment(payment2 model, int? bookId, string paymentType)
        {
            Console.WriteLine("✅ [DEBUG] Entered ProceedToPayment method.");

            if (TempData["BookId"] == null || TempData["PaymentType"] == null)
            {
                Console.WriteLine("❌ [DEBUG] TempData is missing BookId or PaymentType.");
                TempData["ErrorMessage"] = "Required payment details are missing.";
                return RedirectToAction("Payments");
            }

            if (bookId == null || bookId <= 0)
            {
                Console.WriteLine("❌ [DEBUG] BookId invalid.");
                TempData["ErrorMessage"] = "Invalid Book ID.";
                return RedirectToAction("Payments");
            }




            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                Console.WriteLine("❌ [DEBUG] User session is null or empty.");
                TempData["ErrorMessage"] = "User session expired. Please login again.";
                return RedirectToAction("Login", "Customer");
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            if (string.IsNullOrEmpty(loggedInUser?.ID))
            {
                Console.WriteLine("❌ [DEBUG] LoggedInUser ID is null or empty.");
                TempData["ErrorMessage"] = "Error: Missing user ID. Please login again.";
                return RedirectToAction("Login", "Customer");
            }

            Console.WriteLine($"📝 [DEBUG] LoggedInUser ID = {loggedInUser.ID}");

            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                Console.WriteLine("✅ [DEBUG] Database connection opened.");

                var updateQuery = @"
            UPDATE Customers
            SET CreditCardNumber = @CreditCardNumber,
                CreditCardValidDate = @CreditCardValidDate,
                CreditCardCVC = @CreditCardCVC
            WHERE ID = @ID";

                using (SqlCommand command = new SqlCommand(updateQuery, connection))
                {
                    command.Parameters.AddWithValue("@CreditCardNumber", model.cardnum ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CreditCardValidDate", model.expdate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CreditCardCVC", model.cvv ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@ID", loggedInUser.ID);

                    int rowsAffected = command.ExecuteNonQuery();
                    Console.WriteLine($"✅ [DEBUG] Database updated successfully. Rows affected: {rowsAffected}");
                }
            }

            string userEmail = loggedInUser.email;
            if (!string.IsNullOrEmpty(userEmail))
            {
                Console.WriteLine($"📧 [DEBUG] Sending email to {userEmail}.");
                string subject = "Payment Processed Successfully";
                string body = $@"
            Dear Customer,<br><br>
            Your payment for the book (ID: {bookId}) has been successfully processed as a '{paymentType}' transaction.<br><br>
            Thank you for using our service!<br><br>
            Best regards,<br>Online Library Team";

                SendEmail(userEmail, subject, body);
            }

            Console.WriteLine("✅ [DEBUG] Finished ProceedToPayment method successfully.");

            string action;
            switch (paymentType.ToLowerInvariant())
            {
                case "buy":
                    action = "BuyBook";
                    break;
                case "borrow":
                    action = "BorrowBook";
                    break;
                default:
                    Console.WriteLine("❌ [DEBUG] Invalid payment type.");
                    TempData["ErrorMessage"] = "Invalid payment type.";
                    return RedirectToAction("Payments");
            }

            return RedirectToAction(action, new { bookId });
        }

        // Utility method to get the logged-in user's email
        private string GetLoggedInUserEmail()
        {
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return null;
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);
            return loggedInUser?.email; // Assuming the CustomerModel contains an Email property
        }


        [HttpPost]
        public IActionResult BorrowBook(string bookId, int? days)
        {
            if (!days.HasValue || days.Value <= 0)
            {
                TempData["ErrorMessage"] = "Invalid number of days specified.";
                return RedirectToAction("DisplayBooks");
            }

            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return RedirectToAction("Login", "Users");
            }

            CustomerModel loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // Check if the customer has already borrowed 3 books
                string countQuery = @"
                    SELECT COUNT(*) 
                    FROM RentedBook 
                    WHERE Username = @Firstname";

                using (SqlCommand countCommand = new SqlCommand(countQuery, connection))
                {
                    countCommand.Parameters.AddWithValue("@Firstname", loggedInUser.firstname);

                    int borrowedBooksCount = (int)countCommand.ExecuteScalar();
                    if (borrowedBooksCount >= 3)
                    {
                        TempData["ErrorMessage"] = "You cannot borrow more than 3 books at a time.";
                        return RedirectToAction("DisplayBooks");
                    }
                }

                // Check if the book has already been borrowed
                string checkQuery = @"
            SELECT 1 
            FROM RentedBook
            WHERE Username = @Firstname AND BookId = @BookId";

                using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@Firstname", loggedInUser.firstname);
                    checkCommand.Parameters.AddWithValue("@BookId", bookId);

                    var result = checkCommand.ExecuteScalar();
                    if (result != null)
                    {
                        TempData["ErrorMessage"] = "You have already borrowed this book.";
                        return RedirectToAction("Success");
                    }
                }

                // Fetch book details
                string bookQuery = "SELECT * FROM Books WHERE BookId = @BookId";
                using (SqlCommand bookCommand = new SqlCommand(bookQuery, connection))
                {
                    bookCommand.Parameters.AddWithValue("@BookId", bookId);

                    SqlDataReader reader = bookCommand.ExecuteReader();
                    if (reader.Read())
                    {
                        int copiesAvailableRent = reader.GetInt32(reader.GetOrdinal("CopiesAvailableRent"));
                        if (copiesAvailableRent <= 0)
                        {
                            TempData["ErrorMessage"] = "CannotBeBorrowed";
                            return RedirectToAction("DisplayBooks");
                        }

                        string bookTitle = reader.GetString(reader.GetOrdinal("Title"));
                        string author = reader.GetString(reader.GetOrdinal("Author"));

                        reader.Close();

                        // Update book stock
                        string updateBookStockQuery =
                            "UPDATE Books SET CopiesAvailableRent = CopiesAvailableRent - 1 WHERE BookId = @BookId";
                        using (SqlCommand updateBookStockCommand = new SqlCommand(updateBookStockQuery, connection))
                        {
                            updateBookStockCommand.Parameters.AddWithValue("@BookId", bookId);
                            updateBookStockCommand.ExecuteNonQuery();
                        }

                        // Add book to the rented list
                        DateTime loanDate = DateTime.Now;
                        DateTime endDate = loanDate.AddDays(days.Value);

                        string insertRentedBookQuery = @"
                    INSERT INTO RentedBook (RentedBookID, Username, BookId, Author, LoanDate, EndDate, Email)
                    VALUES (@RentedBookID, @Firstname, @BookId, @Author, @LoanDate, @EndDate, @Email)";
                        using (SqlCommand insertRentedBookCommand = new SqlCommand(insertRentedBookQuery, connection))
                        {
                            insertRentedBookCommand.Parameters.AddWithValue("@Firstname", loggedInUser.firstname);
                            insertRentedBookCommand.Parameters.AddWithValue("@BookId", bookId);
                            insertRentedBookCommand.Parameters.AddWithValue("@Author", author);
                            insertRentedBookCommand.Parameters.AddWithValue("@LoanDate", loanDate);
                            insertRentedBookCommand.Parameters.AddWithValue("@EndDate", endDate);
                            insertRentedBookCommand.Parameters.AddWithValue("@Email", loggedInUser.email);
                            insertRentedBookCommand.Parameters.AddWithValue("@RentedBookID", Guid.NewGuid().ToString());
                            insertRentedBookCommand.ExecuteNonQuery();
                        }

                        string emailSubject = "Borrow Confirmation";
                        string emailBody = $@"
                    <h1>Dear {loggedInUser.firstname},</h1>
                    <p>Thank you for your Borrow!</p>
                    <p>You have successfully Borrowed the book '<strong>{bookId}</strong>'.</p>
                   
                    <p>We hope you enjoy your reading experience.</p>";

                        SendEmail(loggedInUser.email, emailSubject, emailBody);

                    }
                }
            }

            TempData["SuccessMessage"] = "Book borrowed successfully!";
            return RedirectToAction("HomePage", "HomePage");
        }


        [HttpPost]
        public IActionResult BuyBook(string bookId)
        {
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return RedirectToAction("Login", "Users");
            }

            CustomerModel loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // 1) Check if the book has already been purchased
                string checkQuery = @"
            SELECT 1 
            FROM OwnedBooks 
            WHERE Username = @Firstname AND BookId = @BookId";

                using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@Firstname", loggedInUser.firstname);
                    checkCommand.Parameters.AddWithValue("@BookId", bookId);

                    var result = checkCommand.ExecuteScalar();
                    if (result != null)
                    {
                        TempData["ErrorMessage"] = "You have already purchased this book.";
                        return RedirectToAction("Success");
                    }
                }

                // 2) Fetch book details (including Price & Discount)
                string bookQuery = "SELECT Title, Price, Discount, CopiesAvailable FROM Books WHERE BookId = @BookId";
                using (SqlCommand bookCommand = new SqlCommand(bookQuery, connection))
                {
                    bookCommand.Parameters.AddWithValue("@BookId", bookId);

                    using (SqlDataReader reader = bookCommand.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            TempData["ErrorMessage"] = "Book not found.";
                            return RedirectToAction("DisplayBooks");
                        }

                        int copiesAvailable = reader.GetInt32(reader.GetOrdinal("CopiesAvailable"));
                        if (copiesAvailable <= 0)
                        {
                            TempData["ErrorMessage"] = "This book is out of stock.";
                            return RedirectToAction("DisplayBooks");
                        }

                        decimal price = reader.GetDecimal(reader.GetOrdinal("Price"));

                        // "Discount" may be NULL in DB, so handle carefully:
                        decimal discount = 0;
                        if (!reader.IsDBNull(reader.GetOrdinal("Discount")))
                        {
                            discount = reader.GetDecimal(reader.GetOrdinal("Discount"));
                        }

                        // *** Calculate final price after discount ***
                        // If discount = 10, it means 10% off => finalPrice = price - (price * (10/100)) 
                        decimal finalPrice = (discount > 0)
                            ? price - (price * (discount / 100))
                            : price;

                        // [Optional] You can store finalPrice somewhere or do further logic

                        reader.Close();

                        // 3) Update book stock
                        string updateBookStockQuery =
                            "UPDATE Books SET CopiesAvailable = CopiesAvailable - 1 WHERE BookId = @BookId";
                        using (SqlCommand updateBookStockCommand = new SqlCommand(updateBookStockQuery, connection))
                        {
                            updateBookStockCommand.Parameters.AddWithValue("@BookId", bookId);
                            updateBookStockCommand.ExecuteNonQuery();
                        }

                        // 4) Insert into OwnedBooks
                        string insertOwnedBookQuery = @"
    INSERT INTO OwnedBooks (Id, Username, BookId, Email, PricePaid)
    VALUES (@Id, @Firstname, @BookId, @Email, @PricePaid)";
                        using (SqlCommand insertOwnedBookCommand = new SqlCommand(insertOwnedBookQuery, connection))
                        {
                            insertOwnedBookCommand.Parameters.AddWithValue("@Firstname", loggedInUser.firstname);
                            insertOwnedBookCommand.Parameters.AddWithValue("@BookId", bookId);
                            insertOwnedBookCommand.Parameters.AddWithValue("@Email", loggedInUser.email);
                            insertOwnedBookCommand.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                            insertOwnedBookCommand.Parameters.AddWithValue("@PricePaid", finalPrice);
                            insertOwnedBookCommand.ExecuteNonQuery();
                        }


                        string emailSubject = "Purchase Confirmation";
                                string emailBody = $@"
                    <h1>Dear {loggedInUser.firstname},</h1>
                    <p>Thank you for your purchase!</p>
                    <p>You have successfully purchased the book '<strong>{bookId}</strong>'.</p>
                    <p>Total Amount Paid: <strong>${finalPrice:F2}</strong></p>
                    <p>We hope you enjoy your reading experience.</p>";

                        SendEmail(loggedInUser.email, emailSubject, emailBody);

                    }
                }
            }


            TempData["SuccessMessage"] = "Book purchased successfully!";
            return RedirectToAction("HomePage", "HomePage");
        }


        [HttpGet]
        public IActionResult ManageWaitingList()
        {
            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                string query = "SELECT TitleBook AS  BookId,ID, Author, TitleBook, Username, Email FROM WaitingList";
                List<WaitingList> waitingList = new List<WaitingList>();

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        waitingList.Add(new WaitingList
                        {
                            ID = Convert.ToInt32(reader["ID"]),

                            BookId = reader["BookId"]?.ToString(),
                            Author = reader["Author"]?.ToString(),
                            TitleBook = reader["TitleBook"]?.ToString(),
                            Username = reader["Username"]?.ToString(),
                            Email = reader["Email"]?.ToString(),
                        });
                    }
                }

                return View(waitingList);
            }
        }


        public IActionResult Pay(string actionType, string bookTitle)
        {
            return View("Pay");
        }
    }
}