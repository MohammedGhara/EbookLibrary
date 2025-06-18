using System.Data.SqlClient;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add session services
builder.Services.AddDistributedMemoryCache(); // Required for session state
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(3600); // Session timeout of 1 hour
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Example hosted services from your code (adjust if needed)
builder.Services.AddHostedService<EmailNotificationService>();
builder.Services.AddHostedService<LoanExpirationService>();

// 1. Create the database if it doesn't exist
CreateDatabase("OnlineLibrary");

// Build the application
var app = builder.Build();

// 2. Check and create required tables
CheckAndCreateTables();

// 3. Seed initial data (Admin user + 25 Books)
SeedData();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Add session middleware
app.UseSession();

// Map your routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HomePage}/{action=HomePage1}/{id?}");

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Admin}/{action=ManageBooks}/{id?}");
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HomePage}/{action=DisplayBooks}/{id?}");

app.Run();

/// <summary>
/// Creates the specified database if it does not already exist.
/// </summary>
void CreateDatabase(string databaseName)
{
    // Adjust your master connection string as necessary
    var masterConnectionString =
      "Server=MGHARA;Database=Master;Trusted_Connection=True;";
    using (var connection = new SqlConnection(masterConnectionString))
    {
        connection.Open();
        var checkDbQuery = $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                CREATE DATABASE [{databaseName}]
            END";
        using (var command = new SqlCommand(checkDbQuery, connection))
        {
            command.ExecuteNonQuery();
        }
    }
}

/// <summary>
/// Checks and creates the necessary tables if they do not exist.
/// </summary>
void CheckAndCreateTables()
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    var tableDefinitions = new Dictionary<string, string>
    {
        {
            "Books", @"CREATE TABLE dbo.Books (
                        Title varchar(255),
                        Author varchar(255),
                        PublicationYear varchar(4),
                        Price decimal,
                        BorrowPrice decimal,
                        Discount decimal,
                        CopiesAvailable int,
                        CopiesAvailableRent int DEFAULT 0,
                        AgeRestriction varchar(10),
                        Rating decimal,
                        Genre varchar(50),
                        BookID nvarchar(50) PRIMARY KEY,
                        ImageUrl nvarchar(MAX),
                        BookUrl nvarchar(MAX),
                        Publisher nvarchar(255)


                    )"
        },
        {
            "Customers", @"CREATE TABLE dbo.Customers (
                        ID nvarchar(50) PRIMARY KEY,
                        FirstName nvarchar(50),
                        LastName nvarchar(50),
                        Email nvarchar(100),
                        Password nvarchar(255),
                        Address nvarchar(255),
                        CreatedAt datetime,
                        Type nvarchar(50),
                        CreditCardNumber nvarchar(50),
                        CreditCardValidDate nvarchar(10),
                        CreditCardCVC nvarchar(10)
                    )"
        },
        {
            "OwnedBooks", @"CREATE TABLE dbo.OwnedBooks (
                   Id nvarchar(50) PRIMARY KEY,
                       Username nvarchar(50),
                   BookId nvarchar(255),
                  Email nvarchar(255),
                  PricePaid decimal(10, 2)  -- New column to store the purchase price
             )"
        },
        {
            "RentedBook", @"CREATE TABLE dbo.RentedBook (
                        RentedBookID nvarchar(50) PRIMARY KEY,
                        Username nvarchar(50),
                        BookId nvarchar(255),
                        Author nvarchar(255),
                        Email nvarchar(255),
                        LoanDate datetime,
                        EndDate datetime
                    )"
        },
        {
            "WaitingList", @"CREATE TABLE dbo.WaitingList (
                        ID INT IDENTITY(1,1) PRIMARY KEY,
                        BookId nvarchar(255),
                        Author nvarchar(255),
                        TitleBook NVARCHAR(255) NULL,
                        Username nvarchar(100),
                        Email nvarchar(255)
                    )"
        },
        {
            "Cart", @"CREATE TABLE dbo.Cart (
                        CartID nvarchar(255) PRIMARY KEY,
                        Username NVARCHAR(100) NOT NULL,
                        BookID nvarchar(50) NOT NULL,
                        Title NVARCHAR(200) NOT NULL,
                        ActionType NVARCHAR(10) NOT NULL, -- 'Buy' or 'Borrow'
                        Days INT NULL,                     -- Nullable for 'Buy'
                        Copies INT NULL,                   -- Nullable for 'Borrow'
                        AddedAt DATETIME NOT NULL
                    )"
        },
        {
            "Ratings", @"CREATE TABLE dbo.Ratings (
                    RatingID nvarchar(50) PRIMARY KEY,
                    BookID nvarchar(50) NOT NULL,
                    Username nvarchar(100) NOT NULL,
                    RatingStars int NOT NULL CHECK (RatingStars BETWEEN 1 AND 5),
                    Comment nvarchar(MAX),
                    CreatedAt datetime NOT NULL,
                )"
        },
        {
            "SiteRatings", @"CREATE TABLE dbo.SiteRatings (
                SiteRatingID nvarchar(50) PRIMARY KEY,
                Username nvarchar(100) NOT NULL,
                RatingStars int NOT NULL CHECK (RatingStars BETWEEN 1 AND 5),
                Comment nvarchar(MAX) NULL,
                CreatedAt datetime NOT NULL
            )"
        }
    };

    using (var connection = new SqlConnection(connectionString))
    {
        connection.Open();

        foreach (var table in tableDefinitions)
        {
            // Check if the table exists
            string checkTableQuery = $@"
                IF NOT EXISTS (
                    SELECT * FROM sys.objects 
                    WHERE object_id = OBJECT_ID(N'[dbo].[{table.Key}]') AND type in (N'U')
                )
                BEGIN
                    {table.Value}
                END";

            using (var command = new SqlCommand(checkTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
    }
}

/// <summary>
/// Seeds the database with an Admin user and 25 sample books.
/// </summary>
void SeedData()
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // 1) Seed the Admin user
    SeedAdminUser(connectionString);
    System.Threading.Thread.Sleep(100);  // 🟢 זמן קצר לפני הוספת לקוחות

    SeedCustomerUsers(connectionString);
    // 2) Seed 25 books
    SeedBooks(connectionString);
}
string HashPassword(string password)
{
    using (var sha256 = SHA256.Create())
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
/// <summary>
/// Inserts an Admin user with a hashed password if none exists.
/// </summary>
void SeedAdminUser(string connectionString)
{
    /// <summary>
    /// A simple method to hash a password using SHA256. 
    /// (Prefer stronger algorithms in production.)
    /// </summary>


    using var connection = new SqlConnection(connectionString);
    connection.Open();

    // Check if admin already exists
    string checkAdminQuery = "SELECT COUNT(*) FROM Customers WHERE Email = 'm7md.gara.m@gmail.com'";
    using (var checkCmd = new SqlCommand(checkAdminQuery, connection))
    {
        int count = (int)checkCmd.ExecuteScalar();
        if (count == 0)
        {
            // Admin does not exist, insert
            string insertAdminQuery = @"
                INSERT INTO Customers
                (ID, FirstName, LastName, Email, Password, Address, CreatedAt, Type)
                VALUES
                (@ID, @FirstName, @LastName, @Email, @Password, @Address, @CreatedAt, @Type)";

            using (var insertCmd = new SqlCommand(insertAdminQuery, connection))
            {
                insertCmd.Parameters.AddWithValue("@ID", 0);

                insertCmd.Parameters.AddWithValue("@FirstName", "Admin");
                insertCmd.Parameters.AddWithValue("@LastName", "User");
                insertCmd.Parameters.AddWithValue("@Email", "m7md.gara.m@gmail.com");

                // Hash the password "123"
                string hashedPassword = HashPassword("123");
                insertCmd.Parameters.AddWithValue("@Password", hashedPassword);

                insertCmd.Parameters.AddWithValue("@Address", "123 Admin Street");
                insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                insertCmd.Parameters.AddWithValue("@Type", "Admin");

                insertCmd.ExecuteNonQuery();
            }

            Debug.WriteLine("Admin user seeded successfully.");
        }
        else
        {
            Debug.WriteLine("Admin user already exists; skipping seed.");
        }
    }
}

bool IsCustomerValid(CustomerSeedModel customer)
{
   
    if (!Regex.IsMatch(customer.CreditCardNumber, @"^(\d{4}[-\s]?){3}\d{4}$"))
        return false;

    if (!Regex.IsMatch(customer.CreditCardValidDate, @"^(0[1-9]|1[0-2])\/\d{2}$"))
        return false;

   
    if (!Regex.IsMatch(customer.CreditCardCVC, @"^\d{3}$"))
        return false;

    if (!Regex.IsMatch(customer.Email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
        return false;

    if (!Regex.IsMatch(customer.ID, @"^\d{9}$"))
        return false;

    return true;
}

void SeedCustomerUsers(string connectionString)
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();

    var customers = GetSeedCustomers();

    foreach (var c in customers)
    {
        if (!IsCustomerValid(c))
        {
            Debug.WriteLine($"❌ Invalid customer: {c.ID} | Email: {c.Email} | Card: {c.CreditCardNumber}");
            continue;
        }
        string checkExistQuery = "SELECT COUNT(*) FROM Customers WHERE ID = @ID";
        using var checkCmd = new SqlCommand(checkExistQuery, connection);
        checkCmd.Parameters.AddWithValue("@ID", c.ID);

        int count = (int)checkCmd.ExecuteScalar();
        if (count > 0) continue;

        string insertCustomerQuery = @"
            INSERT INTO Customers
            (ID, FirstName, LastName, Email, Password, Address, CreatedAt, Type, CreditCardNumber, CreditCardValidDate, CreditCardCVC)
            VALUES
            (@ID, @FirstName, @LastName, @Email, @Password, @Address, @CreatedAt, @Type, @CreditCardNumber, @CreditCardValidDate, @CreditCardCVC)";

        using var insertCmd = new SqlCommand(insertCustomerQuery, connection);
        insertCmd.Parameters.AddWithValue("@ID", c.ID);
        insertCmd.Parameters.AddWithValue("@FirstName", c.FirstName);
        insertCmd.Parameters.AddWithValue("@LastName", c.LastName);
        insertCmd.Parameters.AddWithValue("@Email", c.Email);

        string hashedPassword = HashPassword(c.Password);
        insertCmd.Parameters.AddWithValue("@Password", hashedPassword);

        insertCmd.Parameters.AddWithValue("@Address", c.Address);
        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
        insertCmd.Parameters.AddWithValue("@Type", "Customer");
        insertCmd.Parameters.AddWithValue("@CreditCardNumber", c.CreditCardNumber);
        insertCmd.Parameters.AddWithValue("@CreditCardValidDate", c.CreditCardValidDate);
        insertCmd.Parameters.AddWithValue("@CreditCardCVC", c.CreditCardCVC);

        insertCmd.ExecuteNonQuery();
    }

    Console.WriteLine("✅ 10 customers seeded successfully.");
}


/// <summary>
/// Inserts 25 sample books if no books exist yet.
/// </summary>
void SeedBooks(string connectionString)
{
    using var connection = new SqlConnection(connectionString);
    connection.Open();

    // Check if there are already any books in the table
    string checkBooksQuery = "SELECT COUNT(*) FROM Books";
    using (var checkBooksCmd = new SqlCommand(checkBooksQuery, connection))
    {
        int count = (int)checkBooksCmd.ExecuteScalar();
        if (count > 0)
        {
            Debug.WriteLine("Books already exist; skipping seed.");
            return;
        }
    }

    // Insert 25 books
    var booksToSeed = GetSeedBooks();
    string insertBookQuery = @"
        INSERT INTO Books 
        (Title, Author, PublicationYear, Price, BorrowPrice, Discount, CopiesAvailable, 
         CopiesAvailableRent, AgeRestriction, Rating, Genre, BookID, ImageUrl, BookUrl, Publisher)
        VALUES
        (@Title, @Author, @PublicationYear, @Price, @BorrowPrice, @Discount, @CopiesAvailable,
         @CopiesAvailableRent, @AgeRestriction, @Rating, @Genre, @BookID, @ImageUrl, @BookUrl, @Publisher)";

    foreach (var book in booksToSeed)
    {
        using var insertCmd = new SqlCommand(insertBookQuery, connection);
        insertCmd.Parameters.AddWithValue("@Title", book.Title);
        insertCmd.Parameters.AddWithValue("@Author", book.Author);
        insertCmd.Parameters.AddWithValue("@PublicationYear", book.PublicationYear);
        insertCmd.Parameters.AddWithValue("@Price", book.Price);
        insertCmd.Parameters.AddWithValue("@BorrowPrice", book.BorrowPrice);
        insertCmd.Parameters.AddWithValue("@Discount", book.Discount);
        insertCmd.Parameters.AddWithValue("@CopiesAvailable", book.CopiesAvailable);

        // Always set 3 copies available for rent
        insertCmd.Parameters.AddWithValue("@CopiesAvailableRent", 3);

        insertCmd.Parameters.AddWithValue("@AgeRestriction", book.AgeRestriction);
        insertCmd.Parameters.AddWithValue("@Rating", book.Rating);
        insertCmd.Parameters.AddWithValue("@Genre", book.Genre);
        insertCmd.Parameters.AddWithValue("@BookID", book.BookID);
        insertCmd.Parameters.AddWithValue("@ImageUrl", book.ImageUrl);
        insertCmd.Parameters.AddWithValue("@BookUrl", book.BookUrl);
        insertCmd.Parameters.AddWithValue("@Publisher", book.Publisher);

        insertCmd.ExecuteNonQuery();
    }

    Debug.WriteLine("25 books seeded successfully.");
}

/// <summary>
/// Returns a list of 25 sample books with realistic data.
/// </summary>
List<BookSeedModel> GetSeedBooks()
{
    return new List<BookSeedModel>
    {
       new BookSeedModel
        {
            Title = "The Da Vinci Code",
            Author = "Dan Brown",
            PublicationYear = "2003",
            Price = 11.99m,
            BorrowPrice = 3.09m,
            Discount = 5.0m,
            CopiesAvailable = 13,
            AgeRestriction = "16+",
            Rating = 4.5m,
            Genre = "Fantasy",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://c7.alamy.com/comp/K38TH6/the-da-vinci-code-date-2006-K38TH6.jpg",
            BookUrl = "https://dn790007.ca.archive.org/0/items/TheDaVinciCode_201308/The%20Da%20Vinci%20Code.pdf",
            Publisher = "Doubleday"
        },
        new BookSeedModel
        {
            Title = "The Kite Runner",
            Author = "Khaled Hosseini",
            PublicationYear = "2003",
            Price = 13.99m,
            BorrowPrice = 3.49m,
            Discount = 6.5m,
            CopiesAvailable = 7,
            AgeRestriction = "15+",
            Rating = 4.6m,
            Genre = "Adventure",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://images.ctfassets.net/qpn1gztbusu2/26RUt4RVykbS2lYsNxqS9t/02713b81bb235be2bea3b984044ceae3/title-kite-runner-social.jpg",
            BookUrl = "https://mrsmeganparrish.weebly.com/uploads/3/8/0/5/38056115/the_kite_runner.pdf",
            Publisher = "Riverhead Books"
        },
        new BookSeedModel
        {
            Title = "Harry Potter and the Order of the Phoenix",
            Author = "J.K. Rowling",
            PublicationYear = "2003",
            Price = 14.99m,
            BorrowPrice = 4.49m,
            Discount = 10.0m,
            CopiesAvailable = 20,
            AgeRestriction = "12+",
            Rating = 4.9m,
            Genre = "Fantasy",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://m.media-amazon.com/images/I/71bSzRZj5JL.AC_UF1000,1000_QL80.jpg",
            BookUrl = "https://afgjilibrary.wordpress.com/wp-content/uploads/2020/05/hp5-harry-potter-and-the-order-of-the-phoenix.pdf",
            Publisher = "Bloomsbury"
        },
        new BookSeedModel
        {
            Title = "Twilight",
            Author = "Stephenie Meyer",
            PublicationYear = "2005",
            Price = 12.99m,
            BorrowPrice = 3.99m,
            Discount = 7.0m,
            CopiesAvailable = 15,
            AgeRestriction = "14+",
            Rating = 4.3m,
            Genre = "Romance",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://m.media-amazon.com/images/I/615ZIxEDozL.jpg",
            BookUrl = "https://dn790000.ca.archive.org/0/items/Book3Eclipse/Book%201%20-%20Twilight.pdf",
            Publisher = "Little, Brown"
        },
        new BookSeedModel
        {
            Title = "The Hunger Games",
            Author = "Suzanne Collins",
            PublicationYear = "2008",
            Price = 11.99m,
            BorrowPrice = 3.19m,
            Discount = 6.0m,
            CopiesAvailable = 18,
            AgeRestriction = "13+",
            Rating = 4.7m,
            Genre = "Horror",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://m.media-amazon.com/images/I/91IMwiZCOHL.jpg",
            BookUrl = "https://www.deyeshigh.co.uk/downloads/literacy/world_book_day/the_hunger_games_-_trilogy.pdf",
            Publisher = "Scholastic Press"
        },
        new BookSeedModel
        {
            Title = "The Fault in Our Stars",
            Author = "John Green",
            PublicationYear = "2012",
            Price = 10.99m,
            BorrowPrice = 2.99m,
            Discount = 5.0m,
            CopiesAvailable = 10,
            AgeRestriction = "12+",
            Rating = 4.5m,
            Genre = "Romance",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1394995335i/20821105.jpg",
            BookUrl = "https://www.juhsd.net/site/handlers/filedownload.ashx?moduleinstanceid=4480&dataid=7745&FileName=The-Fault-in-Our-Stars.pdf",
            Publisher = "Dutton Books"
        },
        new BookSeedModel
        {
            Title = "Divergent",
            Author = "Veronica Roth",
            PublicationYear = "2011",
            Price = 9.99m,
            BorrowPrice = 3.49m,
            Discount = 4.0m,
            CopiesAvailable = 12,
            AgeRestriction = "13+",
            Rating = 4.3m,
            Genre = "Historical",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://m.media-amazon.com/images/I/81H1MhBbPbL.AC_UF1000,1000_QL80.jpg",
            BookUrl = "https://www.sausd.us/cms/lib/CA01000471/Centricity/Domain/241/Divergent.pdf",
            Publisher = "HarperCollins"
        },
        new BookSeedModel
        {
            Title = "Gone Girl",
            Author = "Gillian Flynn",
            PublicationYear = "2012",
            Price = 14.99m,
            BorrowPrice = 4.99m,
            Discount = 10.0m,
            CopiesAvailable = 8,
            AgeRestriction = "16+",
            Rating = 4.6m,
            Genre = "Adventure",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://alittleblogofbooks.com/wp-content/uploads/2013/09/gone-girl.jpg",
            BookUrl = "https://icrrd.com/public/media/15-05-2021-082725Gone-Girl-Gillian-Flynn.pdf",
            Publisher = "Crown Publishing"
        },
        new BookSeedModel
        {
            Title = "A Court of Thorns and Roses",
            Author = "Sarah J. Maas",
            PublicationYear = "2015",
            Price = 13.99m,
            BorrowPrice = 3.49m,
            Discount = 5.0m,
            CopiesAvailable = 10,
            AgeRestriction = "16+",
            Rating = 4.4m,
            Genre = "Fantasy",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://m.media-amazon.com/images/I/81Z-yHX8OcL.AC_UF1000,1000_QL80.jpg",
            BookUrl = "https://ekladata.com/qF9VkEbqNqW6gfrA0aXS9ryvX40/Tome-1-A-Court-of-thorns-and-roses.pdf",
            Publisher = "Bloomsbury"
        },
        new BookSeedModel
        {
            Title = "Throne of Glass",
            Author = "Sarah J. Maas",
            PublicationYear = "2012",
            Price = 12.49m,
            BorrowPrice = 3.49m,
            Discount = 0.0m,
            CopiesAvailable = 9,
            AgeRestriction = "15+",
            Rating = 4.7m,
            Genre = "Fantasy",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1673566495i/76703559.jpg",
            BookUrl = "https://onceuponabook.home.blog/wp-content/uploads/2019/01/1_throne_of_glass_-_sarah_j_maas.pdf",
            Publisher = "Bloomsbury"
        },


         new BookSeedModel
        {
            Title = "AnAanA",
            Author = "Charlotte Brontë",
            PublicationYear = "1847",
            Price = 10.99m,
            BorrowPrice = 2.99m,
            Discount = 0.0m,
            CopiesAvailable = 7,
            AgeRestriction = "12+",
            Rating = 4.3m,
            Genre = "Romance",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSa02iDgGtK07iabm_143heTU5kOzEQTDbLbQ&s",
            BookUrl = "https://www.writersunlimited.nl/winternachten/documenten/20210113200045.pdf",
            Publisher = "Smith, Elder & Co."
        },
         new BookSeedModel
        {
            Title = "To Kill a Mockingbird",
            Author = "Harper Lee",
            PublicationYear = "1960",
            Price = 10.99m,
            BorrowPrice = 2.99m,
            Discount = 5.0m,
            CopiesAvailable = 15,
            AgeRestriction = "13+",
            Rating = 4.8m,
            Genre = "Mystery",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://prodimage.images-bn.com/pimages/9780060935467_p1_v4_s600x595.jpg",
            BookUrl = "https://www.raio.org/TKMFullText.pdf",
            Publisher = "J.B. Lippincott & Co."
        },
        new BookSeedModel
        {
            Title = "1984",
            Author = "George Orwell",
            PublicationYear = "1949",
            Price = 11.99m,
            BorrowPrice = 2.49m,
            Discount = 6.5m,
            CopiesAvailable = 10,
            AgeRestriction = "16+",
            Rating = 4.6m,
            Genre = "Horror",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://media.gettyimages.com/id/482637682/photo/authorities-investigate-journalists-over-possible-treason.jpg?s=2048x2048&w=gi&k=20&c=JrVlx-PY_KwJr4coCLEXhdYFw75NPVscL4EAKpnPGuY=",
            BookUrl = "https://rauterberg.employee.id.tue.nl/lecturenotes/DDM110%20CAS/Orwell-1949%201984.pdf",
            Publisher = "Secker & Warburg"
        },
        new BookSeedModel
        {
            Title = "Pride and Prejudice",
            Author = "Jane Austen",
            PublicationYear = "1813",
            Price = 8.99m,
            BorrowPrice = 2.79m,
            Discount = 7.0m,
            CopiesAvailable = 12,
            AgeRestriction = "12+",
            Rating = 4.7m,
            Genre = "Romance",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://shop.bbc.com/cdn/shop/products/Pride_Prejudice_DVD_BD_combo.jpg?v=1727619570",
            BookUrl = "https://giove.isti.cnr.it/demo/eread/Libri/joy/Pride.pdf",
            Publisher = "T. Egerton, Whitehall"
        },
        new BookSeedModel
        {
            Title = "The Catcher in the Rye",
            Author = "J.D. Salinger",
            PublicationYear = "1951",
            Price = 9.99m,
            BorrowPrice = 2.49m,
            Discount = 0.0m,
            CopiesAvailable = 8,
            AgeRestriction = "15+",
            Rating = 4.2m,
            Genre = "Mystery",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://bookonlinepk.com/cdn/shop/files/305917f7-3b48-4106-b877-a0262f14687d_250x.jpg?v=1715859116",
            BookUrl = "https://giove.isti.cnr.it/demo/eread/Libri/sad/Rye.pdf",
            Publisher = "Little, Brown and Company"
        },
         new BookSeedModel
        {
            Title = "The Midnight Library",
            Author = "Matt Haig",
            PublicationYear = "2020",
            Price = 13.99m,
            BorrowPrice = 3.49m,
            Discount = 8.0m,
            CopiesAvailable = 15,
            AgeRestriction = "16+",
            Rating = 4.7m,
            Genre = "Historical",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://anylang.net/sites/default/files/covers/midnight-library_2.jpg",
            BookUrl = "https://icrrd.com/public/media/14-05-2021-102937The-Midnight%20-Library-Matt%20Haig.pdf",
            Publisher = "Canongate Books"
        },
         new BookSeedModel
        {
            Title = "Project Hail Mary",
            Author = "Andy Weir",
            PublicationYear = "2021",
            Price = 16.99m,
            BorrowPrice = 4.99m,
            Discount = 10.0m,
            CopiesAvailable = 10,
            AgeRestriction = "16+",
            Rating = 4.9m,
            Genre = "Fantasy",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1607709203i/55313155.jpg",
            BookUrl = "https://ia801707.us.archive.org/11/items/project-mary-hail/Project%20Mary%20Hail.pdf",
            Publisher = "Ballantine Books"
        },
        new BookSeedModel
        {
            Title = "The Silent Patient",
            Author = "Alex Michaelides",
            PublicationYear = "2019",
            Price = 14.99m,
            BorrowPrice = 3.99m,
            Discount = 7.0m,
            CopiesAvailable = 12,
            AgeRestriction = "16+",
            Rating = 4.6m,
            Genre = "Horror",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1668782119i/40097951.jpg",
            BookUrl = "https://whsmith.com.au/wp-content/uploads/2019/02/The-Silent-Patient-first-chaper.pdf",
            Publisher = "Celadon Books"
        },
        new BookSeedModel
        {
            Title = "Where the Crawdads Sing",
            Author = "Delia Owens",
            PublicationYear = "2018",
            Price = 15.99m,
            BorrowPrice = 4.29m,
            Discount = 5.0m,
            CopiesAvailable = 18,
            AgeRestriction = "16+",
            Rating = 4.8m,
            Genre = "Mystery",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://judithmckinnon.com/wp-content/uploads/2022/11/sing.jpg?w=656    ",
            BookUrl = "https://dn790006.ca.archive.org/0/items/wherethecrawdadssing/Where-the-Crawdads-Sing.pdf",
            Publisher = "G.P. Putnam's Sons"
        },
        new BookSeedModel
        {
            Title = "The Giver of Stars",
            Author = "Jojo Moyes",
            PublicationYear = "2019",
            Price = 13.99m,
            BorrowPrice = 3.49m,
            Discount = 6.0m,
            CopiesAvailable = 14,
            AgeRestriction = "14+",
            Rating = 4.5m,
            Genre = "Adventure",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://rovingbookwormng.com/wp-content/uploads/2020/11/img_2259.jpg",
            BookUrl = "http://103.203.175.90:81/fdScript/RootOfEBooks/E%20Book%20collection%20-%202024%20-%20I/RARE%20BOOKS/The%20Giver%20of%20Stars%20by%20Jojo%20Moyes.pdf",
            Publisher = "Penguin Books"
        },
        new BookSeedModel
        {
            Title = "Anxious People",
            Author = "Fredrik Backman",
            PublicationYear = "2020",
            Price = 12.99m,
            BorrowPrice = 3.69m,
            Discount = 7.0m,
            CopiesAvailable = 11,
            AgeRestriction = "12+",
            Rating = 4.4m,
            Genre = "Historical",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://static.bookofthemonth.com/cdn-cgi/image/format=auto,width=3840/https://static.bookofthemonth.com/covers/list/AQuietLife_200x300.jpg",
            BookUrl = "https://icrrd.com/public/media/15-05-2021-070512Anxious-People-Fredrik-Backman.pdf",
            Publisher = "Atria Books"
        },
        new BookSeedModel
        {
            Title = "Atomic Habits",
            Author = "James Clear",
            PublicationYear = "2018",
            Price = 11.99m,
            BorrowPrice = 3.49m,
            Discount = 5.0m,
            CopiesAvailable = 15,
            AgeRestriction = "12+",
            Rating = 4.8m,
            Genre = "Romance",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://m.media-amazon.com/images/I/61dPufe7aeL.AC_UF1000,1000_QL80.jpg",
            BookUrl = "https://dn790007.ca.archive.org/0/items/atomic-habits-pdfdrive/Atomic%20habits%20%28%20PDFDrive%20%29.pdf",
            Publisher = "Avery Publishing"
        },
        new BookSeedModel
        {
            Title = "Verity",
            Author = "Colleen Hoover",
            PublicationYear = "2018",
            Price = 13.99m,
            BorrowPrice = 4.19m,
            Discount = 6.0m,
            CopiesAvailable = 10,
            AgeRestriction = "16+",
            Rating = 4.5m,
            Genre = "Fantasy",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1633397087i/59218704.jpg",
            BookUrl = "https://pdfdrive.com.co/wp-content/pdfh/Verity-By-Colleen-Hoover.pdf",
            Publisher = "Amazon Publishing"
        },
        new BookSeedModel
        {
            Title = "It Ends With Us",
            Author = "Colleen Hoover",
            PublicationYear = "2016",
            Price = 12.99m,
            BorrowPrice = 3.69m,
            Discount = 7.0m,
            CopiesAvailable = 18,
            AgeRestriction = "16+",
            Rating = 4.6m,
            Genre = "Romance",
            BookID = Guid.NewGuid().ToString(),
            ImageUrl = "https://www.evenaar.net/wp-content/uploads/product-covers-3/9789020557527_front-cover-original.jpg",
            BookUrl = "https://icrrd.com/public/media/15-05-2021-052358It-Ends-with-Us.pdf",
            Publisher = "Atria Books"
        },
         new BookSeedModel
         {
        Title = "The Shining",
        Author = "Stephen King",
        PublicationYear = "1977",
        Price = 14.99m,
        BorrowPrice = 3.99m,
        Discount = 10.0m,
        CopiesAvailable = 8,
        AgeRestriction = "16+",
        Rating = 4.5m,
        Genre = "Horror",
        BookID = Guid.NewGuid().ToString(),
        ImageUrl = "https://images-na.ssl-images-amazon.com/images/S/compressed.photo.goodreads.com/books/1353277730i/11588.jpg",
        BookUrl = "https://englishprofi.com.ua/wp-content/uploads/Stephen-King-The-Shining.pdf",
        Publisher = "Horror"
        },


    };
}

List<CustomerSeedModel> GetSeedCustomers()
{
    return new List<CustomerSeedModel>
    {
        new CustomerSeedModel { ID = "123456789", FirstName = "Lina", LastName = "Amin", Email = "lina.amin@example.com", Address = "1 Main St", CreditCardNumber = "1234567812345671", CreditCardValidDate = "12/25", CreditCardCVC = "798", Password = "Lina123" },
        new CustomerSeedModel { ID = "981234567", FirstName = "Ahmad", LastName = "Saleh", Email = "ahmad.saleh@example.com", Address = "2 River Rd", CreditCardNumber = "1234567812345672", CreditCardValidDate = "11/25", CreditCardCVC = "123", Password = "Ahmad123" },
        new CustomerSeedModel { ID = "098123456", FirstName = "Sara", LastName = "Hassan", Email = "sara.hassan@example.com", Address = "3 Forest Ln", CreditCardNumber = "1234567812345673", CreditCardValidDate = "10/26", CreditCardCVC = "343", Password = "Sara123" },
        new CustomerSeedModel { ID = "092223456", FirstName = "Omar", LastName = "Nasser", Email = "omar.nasser@example.com", Address = "4 Garden Ave", CreditCardNumber = "1234567812345674", CreditCardValidDate = "09/26", CreditCardCVC = "257", Password = "Omar123" },
        new CustomerSeedModel { ID = "000123456", FirstName = "Noor", LastName = "Samir", Email = "noor.samir@example.com", Address = "5 Lake Dr", CreditCardNumber = "1234567812345675", CreditCardValidDate = "08/27", CreditCardCVC = "158", Password = "Noor123" },
        new CustomerSeedModel { ID = "098133666", FirstName = "Yara", LastName = "Faris", Email = "yara.faris@example.com", Address = "6 Desert Rd", CreditCardNumber = "1234567812345676", CreditCardValidDate = "07/28", CreditCardCVC = "190", Password = "Yara123" },
        new CustomerSeedModel { ID = "098444777", FirstName = "Khaled", LastName = "Adel", Email = "khaled.adel@example.com", Address = "7 Hill St", CreditCardNumber = "1234567812345677", CreditCardValidDate = "06/28", CreditCardCVC = "334", Password = "Khaled123" },
        new CustomerSeedModel { ID = "123449189", FirstName = "Rania", LastName = "Majed", Email = "rania.majed@example.com", Address = "8 Valley Blvd", CreditCardNumber = "1234567812345678", CreditCardValidDate = "05/29", CreditCardCVC = "118", Password = "Rania123" },
        new CustomerSeedModel { ID = "123451010", FirstName = "Hani", LastName = "Tariq", Email = "hani.tariq@example.com", Address = "9 Ocean Way", CreditCardNumber = "1234567812345679", CreditCardValidDate = "04/29", CreditCardCVC = "702", Password = "Hani123" },
        new CustomerSeedModel { ID = "213892345", FirstName = "Mona", LastName = "Karem", Email = "mona.karem@example.com", Address = "10 Mountain Rd", CreditCardNumber = "1234567812345680", CreditCardValidDate = "03/30", CreditCardCVC = "166", Password = "Mona123" },
    };
}



/// <summary>
/// A simple model to hold seed data for the Books table.
/// </summary>
class BookSeedModel
{
    public string Title { get; set; }
    public string Author { get; set; }
    public string PublicationYear { get; set; }
    public decimal Price { get; set; }
    public decimal BorrowPrice { get; set; }
    public decimal Discount { get; set; }
    public int CopiesAvailable { get; set; }
    public string AgeRestriction { get; set; }
    public decimal Rating { get; set; }
    public string Genre { get; set; }
    public string BookID { get; set; }
    public string ImageUrl { get; set; }
    public string BookUrl { get; set; }
    public string Publisher { get; set; }
}

class CustomerSeedModel
{
    public string ID { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public string CreditCardNumber { get; set; }
    public string CreditCardValidDate { get; set; }
    public string CreditCardCVC { get; set; }

    public string Password { get; set; }  
}
