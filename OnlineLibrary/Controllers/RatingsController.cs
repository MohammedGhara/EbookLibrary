using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OnlineLibrary.Models;

namespace OnlineLibrary.Controllers;

[Route("Ratings")]
public class RatingsController : Controller
{
    private readonly IConfiguration _configuration;

    public RatingsController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("GetRatingsForBook")]
    public IActionResult GetRatingsForBook(string bookID)
    {
        if (string.IsNullOrEmpty(bookID))
            return Json(new List<object>()); // Return empty list if bookID is null

        var ratingsList = new List<object>();
        string connectionString = _configuration.GetConnectionString("DefaultConnection");

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();
            // Adjust the query to match your column names
            string sql = @"
                SELECT Username, RatingStars, Comment, CreatedAt
                FROM Ratings
                WHERE BookID = @BookID
                ORDER BY CreatedAt DESC
            ";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@BookID", bookID);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ratingsList.Add(new
                        {
                            username = reader["Username"]?.ToString(),
                            ratingStars = reader["RatingStars"] != DBNull.Value ? (int)reader["RatingStars"] : 0,
                            comment = reader["Comment"]?.ToString(),
                            createdAt = reader["CreatedAt"] != DBNull.Value
                                ? ((DateTime)reader["CreatedAt"]).ToString("yyyy-MM-dd HH:mm")
                                : ""
                        });
                    }
                }
            }
        }

        return Json(ratingsList);
    }

    [HttpPost("AddRating")]
    public IActionResult AddRating(string BookID, int RatingStars, string Comment)
    {
        // Check user session
        var userSession = HttpContext.Session.GetString("User");
        if (string.IsNullOrEmpty(userSession))
        {
            return RedirectToAction("Login", "Users");
        }

        var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

        // Basic validation
        if (RatingStars < 1 || RatingStars > 5)
        {
            TempData["ErrorMessage"] = "Invalid Rating (must be between 1 and 5).";
            return RedirectToAction("ProfileView", "HomePage");
        }

        using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
        {
            conn.Open();

            // Insert into the Ratings table
            string insertSql = @"
                INSERT INTO Ratings (RatingID, BookID, Username, RatingStars, Comment, CreatedAt) 
                VALUES (@RatingID, @BookID, @Username, @RatingStars, @Comment, @CreatedAt);
            ";

            using (SqlCommand cmd = new SqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@RatingID", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("@BookID", BookID);
                cmd.Parameters.AddWithValue("@Username", loggedInUser.firstname); // or some unique user field
                cmd.Parameters.AddWithValue("@RatingStars", RatingStars);
                cmd.Parameters.AddWithValue("@Comment", (object)Comment ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                cmd.ExecuteNonQuery();
            }
        }

        TempData["SuccessMessage"] = "Rating submitted successfully!";
        return RedirectToAction("ProfileView", "HomePage");
    }
}