using System.Web;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using OnlineLibrary.Models;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace OnlineLibrary.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IConfiguration _configuration;

        public CustomerController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
            private static readonly Regex _badChars =
          new Regex(@"['"";@!*\\^#$%&()\-]", RegexOptions.Compiled);

        [HttpPost]
        public IActionResult Submit(CustomerModel customer)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (_badChars.IsMatch(customer.firstname) ||
           _badChars.IsMatch(customer.lastname) ||
           _badChars.IsMatch(customer.password) ||
           _badChars.IsMatch(customer.address))
            {
                TempData["ErrorMessage"] = "Invalid characters detected in input.";
                return RedirectToAction("SignUp");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
             INSERT INTO Customers 
                 (ID, FirstName, LastName, Email, Password, Address, CreatedAt, Type, CreditCardNumber, CreditCardValidDate, CreditCardCVC) 
                    VALUES 
                   (@Id, @FirstName, @LastName, @Email, @Password, @Address, @CreatedAt, @Type, @CreditCardNumber, @CreditCardValidDate, @CreditCardCVC)";


                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", customer.ID);
               
                    command.Parameters.AddWithValue("@FirstName", customer.firstname);
                    command.Parameters.AddWithValue("@LastName", customer.lastname);
                    command.Parameters.AddWithValue("@Email", customer.email);

                    // 1. Hash the password before saving to the database
                    var hashedPassword = HashPassword(customer.password);
                    command.Parameters.AddWithValue("@Password", hashedPassword);

                    command.Parameters.AddWithValue("@Address", customer.address);
                    command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                    command.Parameters.AddWithValue("@Type", "Customer");

                    command.Parameters.AddWithValue("@CreditCardNumber", customer.CreditCardNumber ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CreditCardValidDate", customer.CreditCardValidDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@CreditCardCVC", customer.CreditCardCVC ?? (object)DBNull.Value);


                    command.ExecuteNonQuery();
                }
            }

            // Send Welcome Email
            try
            {
                string emailSubject = "Welcome to Online Library";
                string emailBody = $@"
            <h1>Welcome, {customer.firstname}!</h1>
            <p>Thank you for signing up at Online Library. We’re excited to have you on board!</p>";


                // Use the new dynamic SendEmail method
                SendEmail(customer.email, emailSubject, emailBody);
                TempData["SuccessMessage"] = "Customer successfully signed up! A welcome email has been sent.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Customer signed up, but failed to send welcome email: {ex.Message}";
            }

            // Redirect back to the Login page
            return RedirectToAction("login");
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

       
        [HttpGet]
        public IActionResult VulnerableLogin()
        {
            return View();
        }




        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult SignUp()
        {
            CustomerModel user = new CustomerModel
            {
                ID = Request.Form["ID"],
                firstname = Request.Form["firstname"],
                lastname = Request.Form["lastname"],
                email = Request.Form["email"],
                password = Request.Form["password"],
                address = Request.Form["address"],
                CreditCardNumber = Request.Form["CreditCardNumber"],
                CreditCardValidDate = Request.Form["CreditCardValidDate"],
                CreditCardCVC = Request.Form["CreditCardCVC"]
            };
            return View("signup", user);
        }


        [HttpGet]
        public IActionResult SignUp(string id, string firstname, string lastname, string email, string password, string address, string creditCardNumber, string creditCardValidDate, string creditCardCVC)
        {
            CustomerModel user = new CustomerModel
            {
                ID = id,
                firstname = firstname,
                lastname = lastname,
                email = email,
                password = password,
                address = address,
                CreditCardNumber = creditCardNumber,
                CreditCardValidDate = creditCardValidDate,
                CreditCardCVC = creditCardCVC
            };

            return View("signup", user);
        }
        [HttpPost]
        public IActionResult VulnerableLogin(string firstname, string password)
        {
            using (SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();


                string query = $"SELECT * FROM Customers WHERE Firstname = '{firstname}' AND Password = '{password}'";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    try
                    {
                        SqlDataReader reader = command.ExecuteReader();

                        if (reader.Read())
                        {

                            var user = new CustomerModel
                            {
                                ID = reader["ID"].ToString(),
                                firstname = reader["FirstName"].ToString(),
                                lastname = reader["LastName"].ToString(),
                                email = reader["Email"].ToString(),
                                address = reader["Address"].ToString(),
                                Type = reader["Type"].ToString()
                            };

                            HttpContext.Session.SetString("User", JsonConvert.SerializeObject(user));

                            TempData["InjectedMessage"] = $"✅ Logged in as {user.firstname} (Type: {user.Type})";


                            if (user.Type == "Admin")
                                return RedirectToAction("Admin1", "Admin");

                            return RedirectToAction("DisplayBooks", "HomePage");
                        }
                        else
                        {
                            ViewBag.Message = "❌ Login failed. Try again or test an injection.";
                            return View("VulnerableLogin");
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewBag.Message = $"Error: {ex.Message}";
                        return View("VulnerableLogin");
                    }
                }
            }
        }


        [HttpPost]
        public IActionResult Login(string FirstName, string Password)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var specialPattern = new Regex(@"['"";@!*\^#\$%&\(\)\-]");

                if (specialPattern.IsMatch(FirstName))
                {
                    TempData["ErrorMessage"] = "Invalid characters detected in input.";
                    return RedirectToAction("Login");
                }



                string query =
                    "SELECT ID, FirstName, Email, Type FROM Customers WHERE FirstName = @FirstName AND Password = @Password";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FirstName", FirstName?.Trim() ?? string.Empty);

                    var hashedInput = HashPassword(Password?.Trim() ?? string.Empty);
                    command.Parameters.AddWithValue("@Password", hashedInput);

                    // Execute the query and read the data
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var loggedInUser = new CustomerModel
                            {
                                ID = reader.GetString(reader.GetOrdinal("ID")),
                                firstname = reader.GetString(reader.GetOrdinal("FirstName")),
                                email = reader.GetString(reader.GetOrdinal("Email")),
                                Type = reader.GetString(reader.GetOrdinal("Type"))
                            };

                            // Store user data in session, etc...
                            HttpContext.Session.SetString("User", JsonConvert.SerializeObject(loggedInUser));

                            if (loggedInUser.Type == "Admin")
                            {
                                return RedirectToAction("Admin1", "Admin");
                            }
                            else
                            {
                                TempData["SuccessMessage"] = "Login successful!";
                                return RedirectToAction("HomePage1", "HomePage");
                            }
                        }
                        else
                        {
                            // Invalid login
                            TempData["ErrorMessage"] = "Invalid username or password.";
                            return RedirectToAction("Login");
                        }
                    }
                }
            }
        }


        public IActionResult Logout()
        {
            // Clear the session
            HttpContext.Session.Clear();

            // Redirect to the login page
            return RedirectToAction("login");
        }

        [HttpGet]
        public IActionResult EditCustomer(int id)
        {
            CustomerModel customer = null;

            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "SELECT * FROM Customers WHERE firstname = @firstname";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CustomerID", id);
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
                                address = reader["Address"].ToString(),
                            };
                        }
                    }
                }
            }

            return View(customer);
        }

        [HttpPost]
        public IActionResult EditCustomer(CustomerModel updatedCustomer)
        {
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query =
                    "UPDATE Customers SET firstname = @firstName, LastName = @LastName, Email = @Email, Address=@Address WHERE CustomerID = @CustomerID";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@firstName", updatedCustomer.firstname);
                    command.Parameters.AddWithValue("@lastName", updatedCustomer.lastname);
                    command.Parameters.AddWithValue("@Email", updatedCustomer.email);
                    command.Parameters.AddWithValue("@Address", updatedCustomer.address);


                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

            return RedirectToAction("ManageCustomers");
        }

        [HttpPost]
        public IActionResult DeleteCustomer(int id)
        {
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "DELETE FROM Customers WHERE CustomerID = @CustomerID";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CustomerID", id);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

            return RedirectToAction("ManageCustomers");
        }


      
        [HttpGet]
        public IActionResult Login()
        {
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];
            return View();
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }
        [HttpPost]
        public IActionResult SendResetLink(string username)
        {
            string email = "";
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                var query = "SELECT Email FROM Customers WHERE FirstName = @Username";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", username?.Trim());
                    var result = command.ExecuteScalar();

                    if (result == null)
                    {
                        TempData["ErrorMessage"] = "No user found with that username.";
                        return RedirectToAction("ForgotPassword");
                    }

                    email = result.ToString();
                }
            }

            // Now we continue with the reset process
            int num1 = new Random().Next(1, 50);
            int num2 = new Random().Next(1, 50);
            int correct = new Random().Next(0, 2) == 0 ? num1 : num2;

            HttpContext.Session.SetInt32("ResetNumCorrect", correct);
            HttpContext.Session.SetString("ResetEmail", email);

            string resetUrl = Url.Action("VerifyIdentity", "Customer", new { n1 = num1, n2 = num2 }, Request.Scheme);

            string fullEmailBody = $@"
        <h2>Reset Your Password</h2>
        <p>Click the link below and complete verification to reset your password:</p>
        <p><a href='{resetUrl}'>Reset Password</a></p>
        <br>
        <p><strong>Your verification number is: {correct}</strong></p>";

            SendEmail(email, "Online Library – Password Reset & Verification", fullEmailBody);

            TempData["SuccessMessage"] = "An email has been sent to the user’s registered email address.";
            return RedirectToAction("Login");
        }


        [HttpGet]
        public IActionResult VerifyIdentity(int n1, int n2)
        {
            ViewBag.Number1 = n1;
            ViewBag.Number2 = n2;
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmNumber(int picked)
        {
            int? correct = HttpContext.Session.GetInt32("ResetNumCorrect");

            if (correct == picked)
            {
                // Success: proceed to reset password
                return RedirectToAction("ResetPassword");
            }
            else
            {
                // Fail: clear session to prevent reuse
                HttpContext.Session.Remove("ResetNumCorrect");
                HttpContext.Session.Remove("ResetEmail");

                // Set error message and redirect
                TempData["ErrorMessage"] = "Verification failed. You must request a new password reset.";
                return RedirectToAction("ForgotPassword");
            }
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string newPassword)
        {
            string email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Reset session expired.";
                return RedirectToAction("Login");
            }

            string hashed = HashPassword(newPassword);
            using (var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                var query = "UPDATE Customers SET Password = @Password WHERE Email = @Email";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Password", hashed);
                    command.Parameters.AddWithValue("@Email", email);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

            TempData["SuccessMessage"] = "Password reset successful!";
            return RedirectToAction("Login");
        }





    }
}