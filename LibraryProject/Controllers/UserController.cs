using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using LibraryProject.Data;
using LibraryProject.Data.Enums;
using LibraryProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace LibraryProject.Controllers;

public class UserController : Controller
{
    private readonly MVCProjectContext _context;
    private readonly BookReturnHandler _bookReturnHandler;

    [ActivatorUtilitiesConstructor]
    public UserController(MVCProjectContext context, IConfiguration configuration)
    {
        _context = context;
        _bookReturnHandler = new BookReturnHandler(context, configuration);
    }

    public async Task<IActionResult> Index()
    {
        var users = await _context.Users.ToListAsync();
        return View(users);
    }

    public ActionResult ViewUser(User user)
    {
        // An action for viewing user profile that logged in.
        if (ModelState.IsValid)
        {
            return View("ViewUser", user);
        }
        else
        {
            return View("Register", user);
        }
    }

    private string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    private bool VerifyPassword(string password, string hashedPassword)
    {
        return HashPassword(password) == hashedPassword;
    }

    public async Task<IActionResult> ChangePassword(string Email)
    {
        if (string.IsNullOrEmpty(Email))
        {
            // This is for the initial GET request - just show the form
            return View();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null)
        {
            return Json(new { success = false, message = "Email address does not exist in our system." });
        }

        string newPassword = GenerateSecurePassword();
        try
        {
            // Update user's password in database with encrypted version
            user.Password = HashPassword(newPassword);
            user.IsPasswordChanged = 1;
            await _context.SaveChangesAsync();
            // Send email with new password
            EmailSender emailSender = new EmailSender();
            string emailSubject = "Password Change";
            string emailMessage =
                $"Your new password is: {newPassword}\n\nPlease change your password after logging in for security purposes.";

            emailSender.SendEmail(emailSubject, user.Username, emailMessage).Wait();
            return Json(new { success = true, message = "New password has been sent to your email." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An error occurred while processing your request." });
        }
    }

    [HttpGet]
    public IActionResult NewPassword()
    {
        return View();
    }


    [HttpPost]
    public async Task<IActionResult> NewPassword(string CurrentPassword, string NewPassword, string ConfirmPassword)
    {
        if (string.IsNullOrEmpty(CurrentPassword) || string.IsNullOrEmpty(NewPassword) ||
            string.IsNullOrEmpty(ConfirmPassword))
        {
            return Json(new { success = false, message = "All fields are required." });
        }

        if (NewPassword != ConfirmPassword)
        {
            return Json(new { success = false, message = "New password and confirmation password do not match." });
        }

        // Add password validation
        var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#\$%\^&\*])[A-Za-z\d!@#\$%\^&\*]{8,}$");
        if (!passwordRegex.IsMatch(NewPassword))
        {
            return Json(new
            {
                success = false,
                message =
                    "Password must contain at least one uppercase letter, one number, one special character (!,@,#, etc.), and lowercase letters, and be at least 8 characters long."
            });
        }

        string username = HttpContext.Session.GetString("Username");
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return Json(new { success = false, message = "User not found." });
        }

        // Verify current password
        if (!VerifyPassword(CurrentPassword, user.Password))
        {
            return Json(new { success = false, message = "Current password is incorrect." });
        }

        // Update password
        user.Password = HashPassword(NewPassword);
        user.IsPasswordChanged = 0;
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Your password has been changed successfully." });
    }

    
    // Register Get/Post
    [HttpGet]
    public IActionResult Register()
    {
        return View(new User()); // Pass a new User object
    }

    [HttpPost]
    public async Task<IActionResult> Register(User user, string PassConfirm)
    {
        user.cardNumber = "0";
        user.validDate = DateTime.Now;
        user.cvc = "0";
        //ModelState.Clear();
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == user.Username);
        var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Username", "Username is already taken");
            return View(user);
        }

        if (existingEmail != null)
        {
            ModelState.AddModelError("Email", "Email is already taken");
            return View(user);
        }
        
        if (user.Password != PassConfirm)
        {
            ModelState.AddModelError("Password", "Passwords do not match");
            return View(user);
        }
        
        if (ModelState.IsValid)
        {
            try
            {
                user.Password = HashPassword(user.Password);
                user.Role = UserRole.User;
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("Username", user.Username);
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occured while registering user.");
                return View(user);
            }
        }

        return View(user);
    }

    //Login Get/Post
    [HttpGet]
    public IActionResult Login()
    {
        // An action for user login.
        return View("Login");
    }

    public async Task<IActionResult> Login(string Email, string Password)
    {
        // SQL Injection:
        /*
        string query = $"SELECT * FROM Users WHERE Email = '{Email}' AND Password = '{Password}'";
        var users = await _context.Users
            .FromSqlRaw(query)
            .ToListAsync();

        var user = users.FirstOrDefault();

        if (user == null)
        {
            ViewBag.Message = "No matching user found";
            return View("Login");
        }

        HttpContext.Session.SetString("Username", user.Username);
        return RedirectToAction("Index", "Home");
        */
        
        
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);

        if (user == null || !VerifyPassword(Password, user.Password))
        {
            ViewBag.Message = user == null ? "No user found with this email" : "Incorrect password";
            return View("Login");
        }

        // Store username in session
        HttpContext.Session.SetString("Username", user.Username);
        
        return RedirectToAction("Index", "Home");
        
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear(); // Clear all session data
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
    string username = HttpContext.Session.GetString("Username");
    if (string.IsNullOrEmpty(username))
    {
        return RedirectToAction("Login", "User");
    }
    // Get user details
    var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    if (user == null)
    {
        return NotFound();
    }
    // Get user's orders - simplified
    var orders = await _context.Orders
        .Where(o => o.Username == username)
        .OrderByDescending(o => o.OrderDate)
        .Take(5)
        .Select(o => new
        {
            o.OrderId,
            o.Action,
            o.Price,
            o.OrderDate
        })
        .ToListAsync();
    // Get user's reviews
    var reviews = await _context.Reviews
        .Where(r => r.Username == username)
        .OrderByDescending(r => r.CreatedAt)
        .Take(5)
        .Join(_context.Books,
            review => review.BookId,
            book => book.BookId,
            (review, book) => new
            {
                BookTitle = book.Title,
                review.Content,
                review.Rating,
                review.CreatedAt
            })
        .ToListAsync();
    var totalOrders = await _context.Orders
        .CountAsync(o => o.Username == username);
    var waitingList = await _context.WaitingList
        .Where(w => w.Username == username)
        .Join(_context.Books,
            w => w.BookId,
            b => b.BookId,
            (w, b) => new
            {
                w.Position,
                w.JoinDate,
                BookTitle = b.Title,
                b.ImageUrl,
                b.BorrowPrice
            })
        .OrderBy(w => w.Position)
        .ToListAsync();
    // Handle expired orders
    // Handle expired orders
    var currentTime = DateTime.Now;
    var expiredOrders = await _context.Orders
        .Where(o => o.BorrowEndDate <= currentTime && o.IsReturned == 0)
        .ToListAsync();

    foreach (var order in expiredOrders)
    {
        order.IsReturned = 1;
        order.IsRemoved = 1;
        
        // Handle waiting list for this book
        await _bookReturnHandler.HandleBookReturn(order.BookId);
    }
    
    if (expiredOrders.Any())
    {
        await _context.SaveChangesAsync();
    }
    // Get user's books - modified to include exact datetime comparison
    var userBooks = await _context.Orders
        .Where(o => o.Username == username &&
                    (o.Action == "Buy" && (o.IsRemoved == null || o.IsRemoved == 0) || 
                     (o.Action == "Borrow" && DateTime.Now <= o.BorrowEndDate))) // Changed < to <=
        .Join(_context.Books,
            order => order.BookId,
            book => book.BookId,
            (order, book) => new
            {
                BookId = book.BookId,
                Title = book.Title,
                Author = book.Author,
                ImageUrl = book.ImageUrl,
                Action = order.Action,
                BorrowEndDate = order.BorrowEndDate,
                OrderDate = order.OrderDate,
                IsPdfAvailable = book.IsPdfAvailable,
                IsEpubAvailable = book.IsEpubAvailable,
                IsMobiAvailable = book.IsMobiAvailable,
                IsF2bAvailable = book.IsF2bAvailable
            })
        .ToListAsync();
    var currentDate = DateTime.Now;
    var fiveDaysFromNow = DateTime.Now.AddDays(5);

    // Get books that will expire within 5 days
    var booksNearingExpiration = await _context.Orders
        .Where(o => o.Username == username &&
                    o.Action == "Borrow" &&
                    o.BorrowEndDate > currentDate &&
                    o.BorrowEndDate <= fiveDaysFromNow &&
                    o.IsReturned != 1)
        .Join(_context.Books,
            order => order.BookId,
            book => book.BookId,
            (order, book) => new
            {
                Title = book.Title,
                EndDate = order.BorrowEndDate
            })
        .ToListAsync();

    ViewBag.BooksNearingExpiration = booksNearingExpiration;
    ViewBag.UserBooks = userBooks;
    ViewBag.WaitingList = waitingList;
    ViewBag.TotalOrders = totalOrders;
    ViewBag.Orders = orders;
    ViewBag.Reviews = reviews;
    return View(user);
}


    private string GenerateSecurePassword()
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";

        Random random = new Random();
        var password = new StringBuilder();

        // Ensure at least one of each required character type
        password.Append(upperCase[random.Next(upperCase.Length)]); // Capital letter
        password.Append(lowerCase[random.Next(lowerCase.Length)]); // Lowercase letter
        password.Append(digits[random.Next(digits.Length)]); // Number
        password.Append(special[random.Next(special.Length)]); // Special character

        // Fill remaining length (total 8 characters) with random chars from all types
        string allChars = upperCase + lowerCase + digits + special;
        for (int i = 4; i < 8; i++)
        {
            password.Append(allChars[random.Next(allChars.Length)]);
        }

        // Shuffle the password characters
        return new string(password.ToString().ToCharArray().OrderBy(x => random.Next()).ToArray());
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUserBook([FromBody] DeleteBookRequest request)
    {
        Console.WriteLine($"Received bookId: {request.BookId}"); // Debug log
        string username = HttpContext.Session.GetString("Username");

        using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
        {
            await connection.OpenAsync();
            using (var command = new OracleCommand(
                       "UPDATE PERSTIN.ORDERS SET ISREMOVED = 1 " +
                       "WHERE USERNAME = :Username AND BOOKID = :BookId", 
                       connection))
            {
                command.Parameters.Add(new OracleParameter("Username", username));
                command.Parameters.Add(new OracleParameter("BookId", request.BookId));  // Changed from bookId to request.BookId
        
                var rowsAffected = await command.ExecuteNonQueryAsync();
        
                if (rowsAffected > 0)
                {
                    return Json(new { success = true, redirect = true });
                }
            }
        }

        return Json(new { success = false, message = "Book not found" });
    }
    
    
    public class DeleteBookRequest
    {
        public int BookId { get; set; }
    }
    
    [HttpGet]
    public IActionResult DownloadBook(int bookId, string title, string format)
    {
        // Create a simple text file with book info
        var content = $"This is a sample {format.ToUpper()} file for {title}";
        var fileName = $"{title.Replace(" ", "_")}_{format}.txt";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
    
        return File(bytes, "text/plain", fileName);
    }
    
    
    [HttpGet]
    public async Task<IActionResult> ManageUsers()
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "User");
        }
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user.Role != UserRole.Admin)
        {
            TempData["Message"] = "Access Denied! You do not have the required permissions.";
            return RedirectToAction("Index", "Home");
        }
        var users = await _context.Users.ToListAsync();
        return View(users);
    }
    
    [HttpPost]
    public async Task<IActionResult> ChangeRole(string username, UserRole newRole)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user != null)
        {
            user.Role = newRole;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "User role updated successfully.";
        }
        return RedirectToAction("ManageUsers");
    }

    [HttpPost]
    public async Task<IActionResult> RemoveUser(string username)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "User removed successfully.";
        }

        return RedirectToAction("ManageUsers");
    }

}
