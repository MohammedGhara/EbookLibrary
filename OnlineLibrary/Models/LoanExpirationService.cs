using Microsoft.Extensions.Hosting;
using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

public class LoanExpirationService : BackgroundService
{
    private readonly IConfiguration _configuration;

    public LoanExpirationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CheckAndReturnExpiredBooks();
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
       
        }
    }

    private void CheckAndReturnExpiredBooks()
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // Query to get expired books
            string query = @"
                SELECT BookID 
                FROM RentedBook 
                WHERE EndDate <= @CurrentDate";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@CurrentDate", DateTime.Now);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string bookID = reader["BookID"].ToString();
                        ReturnBook(bookID); // Call the ReturnBook logic
                    }
                }
            }
        }
    }

    private void ReturnBook(string bookID)
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Remove the book from the RentedBook table
                    string deleteQuery = @"
                        DELETE FROM RentedBook 
                        WHERE BookID = @BookID";

                    using (var deleteCommand = new SqlCommand(deleteQuery, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@BookID", bookID);
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

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                }
            }
        }
    }
}
