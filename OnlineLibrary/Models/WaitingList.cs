namespace OnlineLibrary.Models
{
    public class WaitingList
    {
        public int ID { get; set; } /// Primary key, if applicable

        public string BookId { get; set; } // Title of the book

        public string Author { get; set; } // Author of the book

        public string TitleBook { get; set; }

        public string Username { get; set; } // Username of the person in the waiting list

        public string Email { get; set; } // Email of the person in the waiting list
        public int CopiesAvailableRent { get; set; }
    }
}   