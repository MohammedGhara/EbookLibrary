using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using OnlineLibrary.Models; // Adjust namespace as needed
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace OnlineLibrary.Controllers
{
    [Route("SiteRatings")]
    public class SiteRatingsController : Controller
    {
        private readonly IConfiguration _configuration;

        public SiteRatingsController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // GET: /SiteRatings/GetAllRatings
        [HttpGet("GetAllRatings")]
        public IActionResult GetAllRatings()
        {
            List<SiteRatingModel> ratings = new List<SiteRatingModel>();
            string connString = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                string sql = @"
                    SELECT SiteRatingID, Username, RatingStars, Comment, CreatedAt
                    FROM SiteRatings
                    ORDER BY CreatedAt DESC
                ";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ratings.Add(new SiteRatingModel
                        {
                            SiteRatingID = reader["SiteRatingID"].ToString(),
                            Username     = reader["Username"].ToString(),
                            RatingStars  = Convert.ToInt32(reader["RatingStars"]),
                            Comment      = reader["Comment"]?.ToString(),
                            CreatedAt    = Convert.ToDateTime(reader["CreatedAt"])
                        });
                    }
                }
            }

            return Json(ratings);
        }

        // POST: /SiteRatings/AddRating
        [HttpPost("AddRating")]
        public IActionResult AddRating([FromBody] SiteRatingRequest request)
        {
            // Check user session
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return Json(new { success = false, message = "User not logged in." });
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);
            if (loggedInUser == null)
            {
                return Json(new { success = false, message = "User session invalid." });
            }

            int ratingStars = request.RatingStars;
            if (ratingStars < 1 || ratingStars > 5)
            {
                return Json(new { success = false, message = "Invalid rating. Must be 1–5." });
            }

            string connString = _configuration.GetConnectionString("DefaultConnection");
            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                string insertSql = @"
                    INSERT INTO SiteRatings (SiteRatingID, Username, RatingStars, Comment, CreatedAt)
                    VALUES (@SiteRatingID, @Username, @RatingStars, @Comment, @CreatedAt)
                ";

                using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@SiteRatingID", Guid.NewGuid().ToString());
                    cmd.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    cmd.Parameters.AddWithValue("@RatingStars", ratingStars);
                    cmd.Parameters.AddWithValue("@Comment", (object)request.Comment ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    cmd.ExecuteNonQuery();
                }
            }

            return Json(new { success = true, message = "Site rating submitted successfully." });
        }
    }

    // A simple model for the POST request body
    public class SiteRatingRequest
    {
        public int RatingStars { get; set; }
        public string Comment { get; set; }
    }
    public class SiteRatingModel
    {
        public string SiteRatingID { get; set; }
        public string Username { get; set; }
        public int RatingStars { get; set; }
        public string Comment { get; set; }
        public System.DateTime CreatedAt { get; set; }
    }
}
