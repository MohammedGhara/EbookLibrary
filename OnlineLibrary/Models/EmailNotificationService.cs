using Microsoft.Extensions.Hosting;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

public class EmailNotificationService : BackgroundService
{
    private readonly IConfiguration _configuration;

    public EmailNotificationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAndSendNotifications();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Run every 1 minute
        }
    }

    private async Task CheckAndSendNotifications()
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // Fetch records with EndDate = DateTime.Now + 5 days
            string query = @"
                SELECT Username, Email, BookID, EndDate
                FROM RentedBook
                WHERE DATEDIFF(DAY, GETDATE(), EndDate) = 5";

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (reader.Read())
                    {
                        string username = reader["Username"].ToString();
                        string email = reader["Email"].ToString();
                        string bookId = reader["BookID"].ToString();
                        DateTime endDate = reader.GetDateTime(reader.GetOrdinal("EndDate"));

                        // Send notification email
                        string subject = "Reminder: Book Borrowing Period Ending Soon";
                        string body = $@"
                            <h1>Hi {username},</h1>
                            <p>This is a reminder that your borrowing period for BookID {bookId} ends on {endDate:dd/MM/yyyy}.</p>
                            <p>Please return the book on time to avoid any penalties.</p>";

                        await SendEmail(email, subject, body);
                    }
                }
            }
        }
    }

    private async Task SendEmail(string toEmail, string subject, string body)
    {
        // Example email sending logic using an SMTP client
        using (var client = new System.Net.Mail.SmtpClient("smtp.your-email-provider.com"))
        {
            client.Port = 587; // SMTP port
            client.Credentials = new System.Net.NetworkCredential("your-email@example.com", "your-password");
            client.EnableSsl = true;

            var mailMessage = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress("your-email@example.com"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true // Allows HTML content
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"Email sent to {toEmail} successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");
            }
        }
    }
}
