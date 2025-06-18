using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using OnlineLibrary.Models;
using Newtonsoft.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Net.Mail;
using System.Net;

namespace OnlineLibrary.Controllers
{
    [Route("")]
    public class HomePageController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public HomePageController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

       
        [HttpGet("")]
        [HttpGet("HomePage")]
        public IActionResult DisplayBooks(
            string title,
            string genre,
            string priceRange, // "low-to-high" or "high-to-low"
            string searchQuery, // partial search (title/author/publisher)
            string method, // "buy" or "borrow" (if needed)
            string ageLimit, // e.g. "18+", "12+", etc.
            string onSale // "On Sale" or "Not on Sale"
        )
        {
            try
            {
                // 1. Get all books from DB
                List<Book> books = GetBooksFromDatabase();
                var userSession = HttpContext.Session.GetString("User");
                bool isLoggedIn = !string.IsNullOrEmpty(userSession);
                string userType = null;
                ViewBag.IsUserLoggedIn = isLoggedIn;
                if (isLoggedIn)
                {
                    var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);
                    userType = loggedInUser?.Type; // "Admin" or "Customer"
                }

                // 2. Basic search across title, author, or publisher
                if (!string.IsNullOrEmpty(searchQuery))
                {
                    books = books.Where(b =>
                            (b.Title != null && b.Title.Contains(searchQuery)) ||
                            (b.Author != null && b.Author.Contains(searchQuery)) ||
                            (b.Publisher != null && b.Publisher.Contains(searchQuery)))
                        .ToList();
                }

                // 3. Filter by exact Title (from dropdown)
                if (!string.IsNullOrEmpty(title))
                {
                    books = books.Where(b => b.Title == title).ToList();
                }

                // 4. Filter by exact Genre (from dropdown)
                if (!string.IsNullOrEmpty(genre))
                {
                    books = books.Where(b => b.Genre == genre).ToList();
                }

                if (!string.IsNullOrEmpty(ageLimit))
                {
                    books = books.Where(b => b.AgeRestriction == ageLimit).ToList();
                }

                // 6. Filter by On Sale
                if (onSale == "On Sale")
                {
                    // Books that have a Discount > 0
                    books = books.Where(b => b.Discount.HasValue && b.Discount.Value > 0).ToList();
                }
                else if (onSale == "Not on Sale")
                {
                    // Books that have no discount or discount == 0
                    books = books.Where(b => !b.Discount.HasValue || b.Discount.Value == 0).ToList();
                }

                // 7. If you need "method" (buy vs. borrow), add your logic here
                if (!string.IsNullOrEmpty(method))
                {
                    // e.g., if (method == "buy") => books = books.Where(whatever) ...
                    // (Only if your DB schema supports it)
                }

                // 8. Sort by Price Range
                //    "low-to-high" => ascending
                //    "high-to-low" => descending
                if (!string.IsNullOrEmpty(priceRange))
                {
                    if (priceRange == "low-to-high")
                    {
                        books = books.OrderBy(b => b.Price).ToList();
                    }
                    else if (priceRange == "high-to-low")
                    {
                        books = books.OrderByDescending(b => b.Price).ToList();
                    }
                }


                // 9. Populate the dropdowns
                ViewBag.Titles = GetDistinctTitlesFromDatabase();
                ViewBag.Genres = GetDistinctGenresFromDatabase();
                ViewBag.AgeRestrictions = GetDistinctAgeRestrictionsFromDatabase();
 
                ViewBag.UserType = userType;
                // 10. Return the list of books to the "HomePage" view
                return View("HomePage", books);
            }
            catch
            {
                TempData["Message"] = "An error occurred while fetching books.";
                return View("HomePage", new List<Book>());
            }
        }


        [HttpGet("HomePage/Profile")]
        public IActionResult ProfileView()
        {
            // Get the logged-in user session
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                // If no session, redirect to login
                return RedirectToAction("Login", "Users");
            }

            // Deserialize the user from session
            CustomerModel loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);
            // Reset (clear) the existing collections to avoid duplicates
            loggedInUser.PurchaseBooks.Clear();
            loggedInUser.LoanedBook.Clear();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Fetch purchased books
                string purchasedBooksQuery = @"
    SELECT b.BookID, b.Title, b.Author, b.PublicationYear, b.Price, b.Genre,
           ob.PricePaid,b.BookUrl
    FROM OwnedBooks ob
    INNER JOIN Books b ON ob.BookId = b.BookId
    WHERE ob.Username = @Username";

                using (SqlCommand command = new SqlCommand(purchasedBooksQuery, connection))
                {
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        Book purchasedBook = new Book
                        {
                            BookID = reader.GetString(reader.GetOrdinal("BookID")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),
                            Author = reader.IsDBNull(reader.GetOrdinal("Author"))
                                ? "Unknown Author"
                                : reader.GetString(reader.GetOrdinal("Author")),
                            PublicationYear = reader.IsDBNull(reader.GetOrdinal("PublicationYear"))
                                ? (int?)null
                                : int.TryParse(reader.GetString(reader.GetOrdinal("PublicationYear")), out int year)
                                    ? year
                                    : (int?)null,
                            Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                            Genre = reader.GetString(reader.GetOrdinal("Genre")),
                            BookUrl = reader.IsDBNull(reader.GetOrdinal("BookUrl"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("BookUrl")),

                            // Get PricePaid from OwnedBooks table
                            PricePaid = reader.IsDBNull(reader.GetOrdinal("PricePaid"))
                                ? (decimal?)null
                                : reader.GetDecimal(reader.GetOrdinal("PricePaid"))
                        };

                        loggedInUser.PurchaseBooks.Add(purchasedBook);
                    }

                    reader.Close();
                }

                // Fetch borrowed books
                string borrowedBooksQuery = @"
            SELECT rb.BookID, rb.Author, rb.LoanDate, rb.EndDate, b.Title,b.BookUrl
            FROM RentedBook rb
            INNER JOIN Books b ON rb.BookId = b.BookId
            WHERE rb.Username = @Username";

                using (SqlCommand command = new SqlCommand(borrowedBooksQuery, connection))
                {
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        Book borrowedBook = new Book
                        {
                            BookID = reader.GetString(reader.GetOrdinal("BookID")),
                            Title = reader.GetString(reader.GetOrdinal("Title")),

                            LoanDate = reader.IsDBNull(reader.GetOrdinal("LoanDate"))
                                ? DateTime.MinValue // Default value or handle as needed
                                : reader.GetDateTime(reader.GetOrdinal("LoanDate")),
                            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate"))
                                ? DateTime.MinValue // Default value or handle as needed
                                : reader.GetDateTime(reader.GetOrdinal("EndDate")),
                                 BookUrl = reader.IsDBNull(reader.GetOrdinal("BookUrl"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("BookUrl"))
                        };
                        loggedInUser.LoanedBook.Add(borrowedBook);
                    }


                    reader.Close();
                }

                NotifyExpiringBorrows();
            }

            // Update the session with the updated user model
            HttpContext.Session.SetString("User", JsonConvert.SerializeObject(loggedInUser));

            return View("ProfileView", loggedInUser);
        }
        


        [HttpGet("NotifyExpiringBorrows")]
        public IActionResult NotifyExpiringBorrows()
        {
            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // We want all RentedBooks with an EndDate between "now" and "now + 5 days"
                string query = @"
            SELECT RentedBookID, Username, Email, Title, EndDate
            FROM RentedBook rb
            INNER JOIN Books b ON rb.BookID = b.BookID
            WHERE EndDate BETWEEN @Now AND @InFiveDays
        ";

                DateTime now = DateTime.Now;
                DateTime inFiveDays = now.AddDays(5);

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Now", now);
                    command.Parameters.AddWithValue("@InFiveDays", inFiveDays);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string email = reader["Email"].ToString();
                            string title = reader["Title"].ToString();
                            string username = reader["Username"].ToString();
                            DateTime endDate = Convert.ToDateTime(reader["EndDate"]);

                            // Compose email
                            string emailSubject = "Reminder: Book Borrowing Period Ending Soon";
                            string emailBody = $@"
                        <h1>Dear {username},</h1>
                        <p>This is a reminder that your borrowing period for the book '<strong>{title}</strong>' 
                           will end on <strong>{endDate:dddd, MMMM dd, yyyy}</strong>.</p>
                        <p>Please ensure you return the book by the due date to avoid any penalties.</p>
                        <p>Thank you for using OnlineLibrary!</p>";

                            // Send email notification
                            SendEmail(email, emailSubject, emailBody);
                        }
                    }
                }
            }

            // TempData["Message"] = "Email notifications sent for books expiring within the next 5 days.";
            return RedirectToAction("ManageBooks");
        }


        public void SendEmail(string to, string subject, string body)
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
        private List<string> GetDistinctAgeRestrictionsFromDatabase()
        {
            List<string> list = new List<string>();
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                connection.Open();
                string query = @"
            SELECT DISTINCT AgeRestriction
            FROM Books
            WHERE AgeRestriction IS NOT NULL
        ";

                using (SqlCommand command = new SqlCommand(query, connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string age = reader["AgeRestriction"] as string;
                        if (!string.IsNullOrEmpty(age))
                        {
                            list.Add(age);
                        }
                    }
                }
            }
            return list;
        }



        private List<Book> GetBooksFromDatabase()
        {
            var books = new List<Book>();
            using (var connection = new SqlConnection(_connectionString))
            {
                var query = "SELECT * FROM Books";
                var command = new SqlCommand(query, connection);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        books.Add(MapReaderToBook(reader));
                    }
                }
            }

            return books;
        }


        private Book MapReaderToBook(SqlDataReader reader)
        {
            return new Book
            {
                BookID = reader["BookID"].ToString(),
                Title = reader["Title"]?.ToString(),
                Author = reader["Author"]?.ToString(),
                ImageUrl = reader["ImageUrl"]?.ToString(),

                Publisher = reader["Publisher"]?.ToString(),
                PublicationYear = reader["PublicationYear"] as int?,
                Price = (decimal)reader["Price"],
                BorrowPrice = reader["BorrowPrice"] as decimal?,
                Discount = reader["Discount"] as decimal?,
                CopiesAvailable = Convert.ToInt32(reader["CopiesAvailable"]),
                CopiesAvailableRent = Convert.ToInt32(reader["CopiesAvailableRent"]),
                AgeRestriction = reader["AgeRestriction"]?.ToString(),
                Genre = reader["Genre"]?.ToString(),
            };
        }


        private List<string> GetDistinctTitlesFromDatabase()
        {
            var titles = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT DISTINCT Title FROM Books WHERE Title IS NOT NULL ORDER BY Title";
                using (var command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            titles.Add(reader["Title"].ToString());
                        }
                    }
                }
            }

            return titles;
        }

        [HttpPost]
        public IActionResult EnterWaitingList(string BookId, string bookAuthor, string Title)
        {
            // Get the logged-in user session
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                // If no session, redirect to login
                return RedirectToAction("Login", "Customer");
            }


            CustomerModel loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                string checkQuery = @"
            SELECT COUNT(*) 
            FROM WaitingList 
            WHERE BookId = @BookId AND Email = @Email";

                using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@BookId", BookId);
                    checkCommand.Parameters.AddWithValue("@Email", loggedInUser.email);

                    int count = (int)checkCommand.ExecuteScalar();
                    if (count > 0)
                    {
                        TempData["ErrorMessage"] = "You are already in the waiting list for this book.";
                        return RedirectToAction("DisplayBooks");
                    }
                }

                // Insert the customer and book details into the WaitingList table
                string insertQuery = @"
            INSERT INTO WaitingList (BookId, Author,TitleBook, Username, Email) 
            VALUES (@BookId, @BookAuthor,@TitleBook, @Username, @Email)";

                using (SqlCommand command = new SqlCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@BookId", BookId);
                    command.Parameters.AddWithValue("@BookAuthor", bookAuthor);
                    command.Parameters.AddWithValue("@TitleBook", Title);
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    command.Parameters.AddWithValue("@Email", loggedInUser.email);

                    command.ExecuteNonQuery(); // Execute the query
                }
            }

            TempData["SuccessMessage"] = "You have been added to the waiting list for this book!";
            return RedirectToAction("DisplayBooks"); // Redirect to the books page or any other relevant page
        }

        [HttpPost]
        [Route("HomePage/RemovePurchasedBook")]
        public IActionResult RemovePurchasedBook(string bookId)
        {
            // Get the logged-in user session
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                // If no session, redirect to login
                return RedirectToAction("Login", "CustomerController");
            }

            // Deserialize the user session to get the customer's details
            CustomerModel loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // Start a transaction for consistency
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Step 1: Remove the book from the OwnedBooks table
                        string deleteOwnedBookQuery =
                            "DELETE FROM OwnedBooks WHERE BookId = @BookId AND Username = @Username";
                        using (var deleteCommand = new SqlCommand(deleteOwnedBookQuery, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@BookId", bookId);
                            deleteCommand.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                            deleteCommand.ExecuteNonQuery();
                        }

                        // Step 2: Increment the CopiesAvailable column in the Books table
                        string updateCopiesQuery =
                            "UPDATE Books SET CopiesAvailable = CopiesAvailable + 1 WHERE BookID = @BookId";
                        using (var updateCommand = new SqlCommand(updateCopiesQuery, connection, transaction))
                        {
                            updateCommand.Parameters.AddWithValue("@BookId", bookId);
                            updateCommand.ExecuteNonQuery();
                        }

                        // Commit the transaction
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Rollback if there's an error
                        transaction.Rollback();
                        TempData["ErrorMessage"] = "An error occurred while removing the book.";
                        return RedirectToAction("Profile"); // Redirect back to the profile page
                    }
                }
            }

            TempData["SuccessMessage"] = "The book has been successfully removed.";
            return RedirectToAction("Profile"); // Redirect back to the profile page
        }

        [HttpPost]
        [Route("HomePage/ReturnBook")]
        public IActionResult ReturnBook(string bookID)
        {
            // Check if the user is logged in
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return RedirectToAction("Login", "Customer");
            }

            // Deserialize the logged-in user session
            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            // Connection string
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Start a transaction
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Remove the book from the RentedBook table
                        string deleteQuery = @"
                    DELETE FROM RentedBook 
                    WHERE BookID = @BookID AND Email = @UserEmail";

                        using (var deleteCommand = new SqlCommand(deleteQuery, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@BookID", bookID);
                            deleteCommand.Parameters.AddWithValue("@UserEmail", loggedInUser.email);
                            deleteCommand.ExecuteNonQuery();
                        }

                        // Increment CopiesAvailable in the Books table
                        string updateQuery = @"
                    UPDATE Books 
                    SET CopiesAvailableRent = CopiesAvailableRent + 1 
                    WHERE BookID = @BookID";

                        using (var updateCommand = new SqlCommand(updateQuery, connection, transaction))
                        {
                            updateCommand.Parameters.AddWithValue("@BookID", bookID);
                            updateCommand.ExecuteNonQuery();
                        }

                        // Commit the transaction
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Rollback in case of error
                        transaction.Rollback();
                        TempData["ErrorMessage"] = "An error occurred while returning the book: " + ex.Message;
                        return RedirectToAction("Profile");
                    }
                }
            }

            // Provide success feedback to the user
            TempData["SuccessMessage"] = "The book has been successfully returned.";
            return RedirectToAction("Profile");
        }


        private List<string> GetDistinctGenresFromDatabase()
        {
            var genres = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                string query = "SELECT DISTINCT Genre FROM Books WHERE Genre IS NOT NULL ORDER BY Genre";
                using (var command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            genres.Add(reader["Genre"].ToString());
                        }
                    }
                }
            }

            return genres;
        }
    }
}