using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OnlineLibrary.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace OnlineLibrary.Controllers
{
    public class CartController : Controller
    {
        private readonly IConfiguration _configuration;

        public CartController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost]
        public IActionResult AddToCart([FromBody] CartItemModel cartItem)
        {
            if (cartItem == null)
            {
                return Json(new { success = false, message = "Invalid cart item data received." });
            }

            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return Json(new { success = false, message = "User not logged in." });
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                var query = @"
        INSERT INTO Cart (CartID, Username, BookID, Title, ActionType, Days, Copies, AddedAt)
        VALUES (@CartID, @Username, @BookID, @Title, @ActionType, @Days, @Copies, @AddedAt)";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CartID", Guid.NewGuid().ToString());
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);
                    command.Parameters.AddWithValue("@BookID", cartItem.BookID);
                    command.Parameters.AddWithValue("@Title", cartItem.Title);
                    command.Parameters.AddWithValue("@ActionType", cartItem.ActionType);
                    command.Parameters.AddWithValue("@Days",
                        cartItem.ActionType == "Borrow" ? (object)cartItem.Days : DBNull.Value);
                    command.Parameters.AddWithValue("@Copies",
                        cartItem.ActionType == "Buy" ? (object)cartItem.Copies : DBNull.Value);
                    command.Parameters.AddWithValue("@AddedAt", DateTime.Now);

                    command.ExecuteNonQuery();
                }
            }

            return Json(new { success = true });
        }


        [HttpGet]
        public IActionResult ViewCart()
        {
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return Json(new List<CartItemModel>()); // Return an empty list if not logged in
            }

            var loggedInUser = JsonConvert.DeserializeObject<CustomerModel>(userSession);

            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                var query = "SELECT * FROM Cart WHERE Username = @Username";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Username", loggedInUser.firstname);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        var cartItems = new List<CartItemModel>();
                        while (reader.Read())
                        {
                            cartItems.Add(new CartItemModel
                            {
                                CartID = reader["CartID"].ToString(),
                                Username = reader["Username"].ToString(),
                                BookID = reader["BookID"].ToString(),
                                Title = reader["Title"].ToString(),
                                ActionType = reader["ActionType"].ToString(),
                                Days = reader["Days"] != DBNull.Value ? (int?)reader["Days"] : null,
                                Copies = reader["Copies"] != DBNull.Value ? (int?)reader["Copies"] : null,
                                AddedAt = DateTime.Parse(reader["AddedAt"].ToString())
                            });
                        }

                        return Json(cartItems);
                    }
                }
            }
        }

        [HttpPost]
        public IActionResult RemoveFromCart([FromBody] Cartdetails cartId)
        {
            var userSession = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(userSession))
            {
                return Json(new { success = false, message = "User not logged in." });
            }

            var cartIdValue = cartId.CartID; // Retrieve the CartID from the Cartdetails object
            using (SqlConnection connection =
                   new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();

                // Ensure the parameter name matches both in the query and in AddWithValue
                var query = "DELETE FROM Cart WHERE CartID = @CartID";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CartID", cartIdValue);
                    command.ExecuteNonQuery();
                }
            }

            return Json(new { success = true });
        }
    }
}