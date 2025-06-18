using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using OnlineLibrary.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;

namespace OnlineLibrary.Controllers
{
    public class Admin : Controller
    {
        private readonly IConfiguration _configuration;
        public string connectionString = "";

        public Admin(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = _configuration.GetConnectionString("DefaultConnection");
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

        public IActionResult ManageBooks()
        {
            var books = new List<Book>();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Books";
                var command = new SqlCommand(query, connection);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        books.Add(new Book
                        {
                            BookID = reader["BookID"].ToString(),
                            Title = reader["Title"].ToString(),
                            Author = reader["Author"].ToString(),
                            Publisher = reader["Publisher"].ToString(),
                            PublicationYear = reader["PublicationYear"] as int?,
                            Price = (decimal)reader["Price"],
                            BorrowPrice = reader["BorrowPrice"] as decimal?,
                            Discount = reader["Discount"] as decimal?,
                            CopiesAvailable = (int)reader["CopiesAvailable"],
                            AgeRestriction = reader["AgeRestriction"].ToString(),
                            CopiesAvailableRent = (int)reader["CopiesAvailableRent"],
                            Genre = reader["Genre"].ToString(),
                            ImageUrl = reader["ImageUrl"].ToString(),
                            BookUrl = reader["BookUrl"].ToString(),
                        });
                    }
                }
            }

            return View(books);
        }

        [HttpPost]
        public IActionResult SetDiscount(string bookId, decimal discount)
        {
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "UPDATE Books SET Discount = @Discount WHERE BookID = @BookID";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Discount", discount);
                    command.Parameters.AddWithValue("@BookID", bookId);

                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

            TempData["SuccessMessage"] = "Discount applied successfully!";
            return RedirectToAction("ManageBooks");
        }

        [HttpGet]
        public IActionResult ManageWaitingList()
        {
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // Modify the query to join WaitingList and Books tables
                string query = @"
            SELECT 
                WL.BookId, 
                WL.ID, 
                WL.Author, 
                WL.TitleBook, 
                WL.Username, 
                WL.Email, 
                B.CopiesAvailableRent 
            FROM WaitingList WL
            INNER JOIN Books B ON WL.BookId = B.BookID"; // Join to fetch CopiesAvailableRent

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
                            CopiesAvailableRent = Convert.ToInt32(reader["CopiesAvailableRent"]) // Add this
                        });
                    }
                }

                return View(waitingList); // Pass the updated model with CopiesAvailableRent
            }
        }

        // Admin Dashboard
        public IActionResult Admin1()
        {
            return View("Admin");
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            return View(); // Admin dashboard page
        }

        // Add Book Page (GET)
        [HttpGet]
        public IActionResult AddBook()
        {
            return View(new Book());
        }

        [HttpPost]
        public IActionResult AddBooks(Book book)
        {
            if (book != null)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Check if the book with the same title and publication year already exists
                    string checkQuery = "SELECT COUNT(*) FROM Books WHERE Title = @title AND PublicationYear = @publicationYear";
                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@title", book.Title);
                        checkCommand.Parameters.AddWithValue("@publicationYear", book.PublicationYear);
                        checkCommand.Parameters.AddWithValue("@Publisher", book.Publisher ?? (object)DBNull.Value); // Ensure this is added

                        int count = (int)checkCommand.ExecuteScalar();

                        // If a book with the same title and year exists, show an error message
                        if (count > 0)
                        {
                            TempData["ErrorMessage"] = "A book with this title and publication year already exists.";
                            return View("AddBook", book); // Return to the AddBook form with the current book details
                        }
                    }

                    // If no duplicate exists, proceed with the insert
                    string query = @"
                INSERT INTO Books
                (ImageUrl,  Title, Author, PublicationYear, Price, BorrowPrice, Discount, CopiesAvailable, AgeRestriction,  Genre, Publisher,BookID,CopiesAvailableRent,BookUrl)
                VALUES
                (@value1,  @value3, @value4, @value5, @value6, @value7, @value8, @value9, @value10,  @value15, @value16,@value17,@value18,@value19)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@value1", book.ImageUrl ?? (object)DBNull.Value);

                        command.Parameters.AddWithValue("@value3", book.Title ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@value4", book.Author ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@value5", book.PublicationYear.HasValue ? (object)book.PublicationYear : DBNull.Value);
                        command.Parameters.AddWithValue("@value6", book.Price);
                        command.Parameters.AddWithValue("@value7", book.BorrowPrice.HasValue ? (object)book.BorrowPrice : DBNull.Value);
                        command.Parameters.AddWithValue("@value8", book.Discount.HasValue ? (object)book.Discount : DBNull.Value);
                        command.Parameters.AddWithValue("@value9", book.CopiesAvailable);
                        command.Parameters.AddWithValue("@value10", book.AgeRestriction ?? (object)DBNull.Value);

                        command.Parameters.AddWithValue("@value15", book.Genre ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@value16", book.Publisher ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@value17", Guid.NewGuid().ToString());
                        command.Parameters.AddWithValue("@value18", book.CopiesAvailableRent);
                        command.Parameters.AddWithValue("@value19", book.BookUrl ?? (object)DBNull.Value);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            TempData["SuccessMessage"] = "Book added successfully!";
                            return RedirectToAction("ManageBooks");
                        }
                        else
                        {
                            ModelState.AddModelError("", "Failed to add the book.");
                            return View("AddBook", book);
                        }
                    }
                }
            }
            else
            {
                // If the book is null, return the form without any messages
                return View("AddBook", book);
            }
        }

        // Edit Book Page (GET)
        [HttpGet]
        public IActionResult EditBook(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Invalid book ID.";
                return RedirectToAction("ManageBooks");
            }

            Book book = null;

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Books WHERE BookID = @BookID";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BookID", id);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            book = new Book
                            {
                                BookID = reader["BookID"].ToString(),
                                Title = reader["Title"].ToString(),
                                Author = reader["Author"].ToString(),
                                Publisher = reader["Publisher"].ToString(),
                                Price = Convert.ToDecimal(reader["Price"]),
                                BorrowPrice = reader["BorrowPrice"] as decimal?,
                                Discount = reader["Discount"] as decimal?,
                                Genre = reader["Genre"].ToString(),
                                ImageUrl = reader["ImageUrl"].ToString(),
                                BookUrl = reader["BookUrl"].ToString(),
                                CopiesAvailable = Convert.ToInt32(reader["CopiesAvailable"]),
                                CopiesAvailableRent = Convert.ToInt32(reader["CopiesAvailableRent"]),
                                AgeRestriction = reader["AgeRestriction"].ToString(),
                                PublicationYear = reader["PublicationYear"] as int?
                            };
                        }
                    }
                }
            }

            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("ManageBooks");
            }

            return View(book);
        }

        [HttpPost]
        public IActionResult EditBook(Book updatedBook)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid data. Please check the fields and try again.";
                return View(updatedBook);
            }

            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = @"
                UPDATE Books
                SET
                    Title = @Title,
                    Author = @Author,
                    Publisher = @Publisher,
                    Price = @Price,
                    BorrowPrice = @BorrowPrice,
                    Discount = @Discount,
                    Genre = @Genre,
                    ImageUrl = @ImageUrl,
                    CopiesAvailable = @CopiesAvailable,
                    AgeRestriction = @AgeRestriction,
                    PublicationYear = @PublicationYear,
                    CopiesAvailableRent = @CopiesAvailableRent
                WHERE BookID = @BookID";

                    using (var command = new SqlCommand(query, connection))
                    {
                        // Add parameters
                        command.Parameters.AddWithValue("@BookID", updatedBook.BookID);
                        command.Parameters.AddWithValue("@Title", updatedBook.Title ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Author", updatedBook.Author ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Publisher", updatedBook.Publisher ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Price", updatedBook.Price);
                        command.Parameters.AddWithValue("@BorrowPrice", updatedBook.BorrowPrice ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Discount", updatedBook.Discount ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Genre", updatedBook.Genre ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@ImageUrl", updatedBook.ImageUrl ?? (object)DBNull.Value);

                        command.Parameters.AddWithValue("@CopiesAvailable", updatedBook.CopiesAvailable);
                        command.Parameters.AddWithValue("@CopiesAvailableRent", updatedBook.CopiesAvailableRent);
                        command.Parameters.AddWithValue("@AgeRestriction", updatedBook.AgeRestriction ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@PublicationYear", updatedBook.PublicationYear ?? (object)DBNull.Value);

                        connection.Open();
                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            // After a successful update, call our new helper to auto-assign
                            AutoAssignFromWaitingList(updatedBook.BookID);

                            TempData["Message"] = "Book updated successfully!";
                        }
                        else
                        {
                            TempData["Error"] = "Book update failed. BookID might be invalid.";
                        }
                    }
                }

                return RedirectToAction("ManageBooks");
            }
            catch (SqlException sqlEx)
            {
                TempData["Error"] = $"SQL Error: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An unexpected error occurred: {ex.Message}";
            }

            return View(updatedBook);
        }

        // Delete Book
        [HttpGet]
        public IActionResult DeleteBook1(string id)
        {
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "DELETE FROM Books WHERE BookID = @BookID";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@BookID", id);

                connection.Open();
                command.ExecuteNonQuery();
            }

            return RedirectToAction("ManageBooks");
        }

        [HttpPost]
        public IActionResult DeleteBook(string id)
        {
            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = "DELETE FROM Books WHERE BookID = @BookID";
                    var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@BookID", id);

                    connection.Open();
                    command.ExecuteNonQuery();
                }

                return RedirectToAction("ManageBooks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while deleting book: {ex.Message}");
                ModelState.AddModelError("", "Failed to delete book.");
                return RedirectToAction("ManageBooks");
            }
        }

        // Helper Method to Retrieve All Books
        private List<Book> GetBooksFromDatabase()
        {
            var books = new List<Book>();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Books";
                var command = new SqlCommand(query, connection);

                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        books.Add(new Book
                        {
                            Title = reader["Title"].ToString(),
                            Author = reader["Author"].ToString(),
                            Publisher = reader["Publisher"].ToString(),
                            ImageUrl = reader["ImageUrl"].ToString(),
                            BookUrl = reader["BookUrl"].ToString(),
                            PublicationYear = reader["PublicationYear"] as int?,
                            Price = (decimal)reader["Price"],
                            BorrowPrice = reader["BorrowPrice"] as decimal?,
                            Discount = reader["Discount"] as decimal?,
                            CopiesAvailable = (int)reader["CopiesAvailable"],
                            CopiesAvailableRent = (int)reader["CopiesAvailableRent"],
                            AgeRestriction = reader["AgeRestriction"].ToString(),
                            Genre = reader["Genre"].ToString()
                        });
                    }
                }
            }

            return books;
        }

        public IActionResult BookGallery()
        {
            var books = new List<Book>();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Books";
                var command = new SqlCommand(query, connection);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        books.Add(new Book
                        {
                            BookID = reader["BookID"].ToString(),
                            Title = reader["Title"].ToString(),
                            Author = reader["Author"].ToString(),
                            ImageUrl = reader["ImageUrl"].ToString(),
                            BookUrl = reader["BookUrl"].ToString(),
                            Publisher = reader["Publisher"].ToString(),
                            PublicationYear = reader["PublicationYear"] as int?,
                            Price = (decimal)reader["Price"],
                            BorrowPrice = reader["BorrowPrice"] as decimal?,
                            Discount = reader["Discount"] as decimal?,
                            CopiesAvailable = (int)reader["CopiesAvailable"],
                            AgeRestriction = reader["AgeRestriction"].ToString(),
                            CopiesAvailableRent = (int)reader["CopiesAvailableRent"],
                            Genre = reader["Genre"].ToString()
                        });
                    }
                }
            }

            return View(books);
        }

        public IActionResult ManageUsers()
        {
            var customers = new List<CustomerModel>();

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Customers";
                var command = new SqlCommand(query, connection);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        customers.Add(new CustomerModel
                        {
                            firstname = reader["FirstName"].ToString(),
                            lastname = reader["LastName"].ToString(),
                            email = reader["Email"].ToString(),
                            address = reader["Address"].ToString(),
                            // Add any other properties you might have
                        });
                    }
                }
            }

            return View(customers);
        }

        [HttpGet]
        public IActionResult EditCustomer(string firstname)
        {
            if (string.IsNullOrEmpty(firstname))
            {
                TempData["Error"] = "Invalid customer firstname.";
                return RedirectToAction("ManageUsers");
            }

            CustomerModel customer = null;

            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = "SELECT * FROM Customers WHERE firstname = @firstname";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@firstname", firstname);
                        connection.Open();

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                customer = new CustomerModel
                                {
                                    firstname = reader["FirstName"].ToString(),
                                    lastname = reader["LastName"].ToString(),
                                    email = reader["Email"].ToString(),
                                    password = reader["Password"].ToString(),
                                    address = reader["Address"].ToString(),
                                    Type = reader["Type"].ToString()
                                };
                            }
                        }
                    }
                }

                if (customer == null)
                {
                    TempData["Error"] = "Customer not found.";
                    return RedirectToAction("ManageUsers");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction("ManageUsers");
            }

            return View(customer); // Pass the customer object to the view.
        }

        [HttpPost]
        public IActionResult EditCustomer(CustomerModel updatedCustomer)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid data. Please check the fields.";
                return View(updatedCustomer);
            }

            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = @"
     UPDATE Customers
     SET
         firstname = @firstname,
         lastname = @lastname,
         email = @email,
         password = @password,
         address = @address,
         Type = @Type
     WHERE firstname = @firstname"; // Using firstname to identify the record.

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@firstname", updatedCustomer.firstname ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@lastname", updatedCustomer.lastname ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@email", updatedCustomer.email ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@password", updatedCustomer.password ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@address", updatedCustomer.address ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Type", updatedCustomer.Type ?? (object)DBNull.Value);

                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }

                TempData["Success"] = "Customer details updated successfully.";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return View(updatedCustomer);
            }
        }

        [HttpPost]
        public IActionResult DeleteCustomer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Invalid customer ID.";
                return RedirectToAction("ManageCustomers"); // Redirect to your customer management page
            }

            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    var query = "DELETE FROM Customers WHERE FirstName = @FirstName";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@FirstName", id);
                        connection.Open();
                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            TempData["Success"] = "Customer deleted successfully.";
                        }
                        else
                        {
                            TempData["Error"] = "Customer not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred while deleting the customer: {ex.Message}";
            }

            return RedirectToAction("ManageUsers", "Admin"); // Redirect to your customer management page
        }

        public IActionResult ManageUsers1()
        {
            // Your logic to retrieve data
            return View("ManageUsers");
        }

        // Helper Method to Retrieve a Single Book by ID
        private Book GetBookByTitleAndAuthor(string title, string author)
        {
            Book book = null;

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Books WHERE Title = @Title AND Author = @Author";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Title", title);
                command.Parameters.AddWithValue("@Author", author);

                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        book = new Book
                        {
                            Title = reader["Title"].ToString(),
                            Author = reader["Author"].ToString(),
                            Publisher = reader["Publisher"].ToString(),
                            PublicationYear = reader["PublicationYear"] as int?,
                            Price = (decimal)reader["Price"],
                            BorrowPrice = reader["BorrowPrice"] as decimal?,
                            Discount = reader["Discount"] as decimal?,
                            CopiesAvailable = (int)reader["CopiesAvailable"],
                            CopiesAvailableRent = (int)reader["CopiesAvailableRent"],
                            AgeRestriction = reader["AgeRestriction"].ToString(),
                            Genre = reader["Genre"].ToString()
                        };
                    }
                }
            }

            return book;
        }

        private void AutoAssignFromWaitingList(string bookID)
        {
            // We can repeat the assignment as long as there's at least 1 copy
            // and at least 1 user waiting, in case multiple copies got freed.
            while (true)
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Check availability
                            string availabilityQuery = @"
                                SELECT CopiesAvailableRent
                                FROM Books
                                WHERE BookID = @BookID
                            ";
                            int copiesAvailable = 0;
                            using (var cmd = new SqlCommand(availabilityQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@BookID", bookID);
                                object result = cmd.ExecuteScalar();
                                if (result == null) break; // Book not found
                                copiesAvailable = Convert.ToInt32(result);
                            }

                            // If no copies, nothing to assign
                            if (copiesAvailable <= 0)
                            {
                                transaction.Rollback();
                                break;
                            }

                            // Check if there's a waiting user
                            string waitingListQuery = @"
                                SELECT TOP 1 ID, Username, Email, TitleBook
                                FROM WaitingList
                                WHERE BookID = @BookID
                                ORDER BY ID ASC
                            ";
                            int waitingId = 0;
                            string waitingUsername = null;
                            string waitingEmail = null;
                            string waitingTitle = null;

                            using (var waitCmd = new SqlCommand(waitingListQuery, connection, transaction))
                            {
                                waitCmd.Parameters.AddWithValue("@BookID", bookID);
                                using (var reader = waitCmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        waitingId = Convert.ToInt32(reader["ID"]);
                                        waitingUsername = reader["Username"].ToString();
                                        waitingEmail = reader["Email"].ToString();
                                        waitingTitle = reader["TitleBook"].ToString();
                                    }
                                }
                            }

                            // No one is waiting
                            if (waitingId == 0)
                            {
                                transaction.Rollback();
                                break;
                            }

                            // We have at least one copy and a waiting user => assign
                            string decrementQuery = @"
                                UPDATE Books
                                SET CopiesAvailableRent = CopiesAvailableRent - 1
                                WHERE BookID = @BookID
                            ";
                            using (var decCmd = new SqlCommand(decrementQuery, connection, transaction))
                            {
                                decCmd.Parameters.AddWithValue("@BookID", bookID);
                                decCmd.ExecuteNonQuery();
                            }

                            // Insert into RentedBook
                            string insertRentedQuery = @"
                                INSERT INTO RentedBook (RentedBookID, Username, BookID, LoanDate, EndDate, Email)
                                VALUES (@RentedBookID, @Username, @BookID, @LoanDate, @EndDate, @Email)
                            ";
                            using (var insCmd = new SqlCommand(insertRentedQuery, connection, transaction))
                            {
                                insCmd.Parameters.AddWithValue("@RentedBookID", Guid.NewGuid().ToString());
                                insCmd.Parameters.AddWithValue("@Username", waitingUsername);
                                insCmd.Parameters.AddWithValue("@BookID", bookID);
                                insCmd.Parameters.AddWithValue("@LoanDate", DateTime.Now);
                                insCmd.Parameters.AddWithValue("@EndDate", DateTime.Now.AddDays(30));
                                insCmd.Parameters.AddWithValue("@Email", waitingEmail);
                                insCmd.ExecuteNonQuery();
                            }

                            // Remove from waiting list
                            string deleteWaitingList = @"
                                DELETE FROM WaitingList
                                WHERE ID = @ID
                            ";
                            using (var delCmd = new SqlCommand(deleteWaitingList, connection, transaction))
                            {
                                delCmd.Parameters.AddWithValue("@ID", waitingId);
                                delCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            // Send email to the newly assigned user
                            string emailSubject = "Book Assignment Notification";
                            string emailBody = $@"
                                <h1>Dear {waitingUsername},</h1>
                                <p>We are pleased to inform you that the book '<strong>{waitingTitle}</strong>' 
                                   you were waiting for has been assigned to you automatically. 
                                   Please collect it at your earliest convenience.</p>";

                            SendEmail(waitingEmail, emailSubject, emailBody);

                            // Loop again in case multiple copies are available or multiple waiters
                        }
                        catch
                        {
                            transaction.Rollback();
                            break;
                        }
                    }
                }
            }
        }
    }
}
