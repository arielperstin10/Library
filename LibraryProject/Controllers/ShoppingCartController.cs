using System.Text;
using System.Text.Json.Nodes;
using LibraryProject.Data;
using LibraryProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace LibraryProject.Controllers;

public class ShoppingCartController : Controller
{
    private string PaypalClientId { get; set; } = "";
    private string PaypalSecret { get; set; } = "";
    private string PaypalUrl { get; set; } = ""; 
    
    private readonly MVCProjectContext _context;

    public ShoppingCartController(MVCProjectContext context, IConfiguration configuration)
    {
        PaypalClientId = configuration["PaypalSettings:ClientId"];
        PaypalSecret = configuration["PaypalSettings:Secret"];
        PaypalUrl = configuration["PaypalSettings:Url"];
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.PaypalClientId = PaypalClientId;
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return RedirectToAction("Login", "User");
        }

        // First, check and fix any inconsistencies
        var borrowedItems = await _context.ShoppingCarts
            .Where(s => s.Username == username && s.Action.ToLower() == "borrow")
            .ToListAsync();

        // If there are more than 3 borrowed items, convert excess to buy
        if (borrowedItems.Count > 3)
        {
            var itemsToConvert = borrowedItems.Skip(3);
            foreach (var item in itemsToConvert)
            {
                var book = await _context.Books.FindAsync(item.BookId);
                item.Action = "buy";
                item.Price = book.IsOnDiscount ? book.DiscountedBuyPrice.Value : book.BuyPrice;
            }
            await _context.SaveChangesAsync();
        }

        var cartItems = await _context.ShoppingCarts
            .Where(s => s.Username == username)
            .Join(_context.Books,
                s => s.BookId,
                b => b.BookId,
                (s, b) => new
                {
                    s.Username,
                    s.BookId,
                    s.Action,
                    s.Quantity,
                    s.Price,
                    b.Title,
                    b.Author,
                    b.IsAvailableToBuy,
                    b.IsAvailableToBorrow,
                    b.AvailableCopies
                })
            .ToListAsync();

        return View(cartItems);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateQuantity(int bookId, int quantity)
    {
        string username = HttpContext.Session.GetString("Username");
        var cartItem = await _context.ShoppingCarts
            .FirstOrDefaultAsync(s => s.BookId == bookId && s.Username == username);

        if (cartItem != null)
        {
            var book = await _context.Books.FindAsync(bookId);
            cartItem.Quantity = quantity;

            // Calculate the current price based on action and discount
            double pricePerItem;
            if (cartItem.Action.ToLower() == "buy")
            {
                // Check if book is on discount
                pricePerItem = book.IsOnDiscount ? book.DiscountedBuyPrice.Value : book.BuyPrice;
            }
            else
            {
                pricePerItem = book.BorrowPrice; // Borrow price doesn't have discounts
            }

            cartItem.Price = pricePerItem * quantity;

            await _context.SaveChangesAsync();

            // Calculate new totals and count
            var newSubtotal = await _context.ShoppingCarts
                .Where(s => s.Username == username)
                .SumAsync(s => s.Price);

            var itemCount = await _context.ShoppingCarts
                .Where(s => s.Username == username)
                .CountAsync();

            return Json(new
            {
                success = true,
                newPrice = cartItem.Price,
                newSubtotal = newSubtotal,
                newTotal = newSubtotal + 10,
                itemCount = itemCount
            });
        }

        return Json(new { success = false });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveItem(int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
        var cartItem = await _context.ShoppingCarts
            .FirstOrDefaultAsync(s => s.BookId == bookId && s.Username == username);

        if (cartItem != null)
        {
            // If removing a borrowed book, increment available copies
            if (cartItem.Action.ToLower() == "borrow")
            {
                var book = await _context.Books.FindAsync(bookId);
                book.AvailableCopies++;
            }

            _context.ShoppingCarts.Remove(cartItem);
            await _context.SaveChangesAsync();

            var newSubtotal = await _context.ShoppingCarts
                .Where(s => s.Username == username)
                .SumAsync(s => s.Price);

            var itemCount = await _context.ShoppingCarts
                .Where(s => s.Username == username)
                .CountAsync();

            return Json(new
            {
                success = true,
                newSubtotal = newSubtotal,
                newTotal = newSubtotal + 10,
                itemCount = itemCount
            });
        }

        return Json(new { success = false });
    }

    [HttpPost]
   public async Task<IActionResult> UpdateAction(int bookId, string newAction)
   {
    try
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return Json(new { success = false, message = "User not logged in" });
        }

        var existingItem = await _context.ShoppingCarts
            .FirstOrDefaultAsync(s => s.BookId == bookId && s.Username == username);
        
        Console.WriteLine($"Existing cart item found: {existingItem != null}");

        if (existingItem != null)
        {
            Console.WriteLine($"Current action: {existingItem.Action}, New action: {newAction}");
            
            if (newAction.ToLower() == "borrow")
            {
                // Count cart items (excluding current book)
                var cartBorrowCount = await _context.ShoppingCarts
                    .CountAsync(sc => sc.Username == username && 
                               sc.Action.ToLower() == "borrow" && 
                               sc.BookId != bookId);

                // Count active orders
                var activeBorrowCount = await _context.Orders
                    .CountAsync(o => o.Username == username && 
                               o.Action.ToLower() == "borrow" && 
                               (o.IsReturned == 0 || o.IsReturned == null) && 
                               (o.IsRemoved == 0 || o.IsRemoved == null));

                var totalBorrowCount = cartBorrowCount + activeBorrowCount;
                Console.WriteLine($"Total borrow count: {totalBorrowCount} (Cart: {cartBorrowCount}, Active: {activeBorrowCount})");

                if (totalBorrowCount >= 3)
                {
                    Console.WriteLine("Borrow limit exceeded");
                    return Json(new { 
                        success = false, 
                        message = "You can only borrow up to 3 books at a time.",
                        currentAction = existingItem.Action
                    });
                }
            }

            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return Json(new { success = false, message = "Book not found" });
            }

            if (existingItem.Action.ToLower() == "borrow" && newAction.ToLower() == "buy")
            {
                book.AvailableCopies++;
            }
            else if (existingItem.Action.ToLower() == "buy" && newAction.ToLower() == "borrow")
            {
                book.AvailableCopies--;
            }

            _context.ShoppingCarts.Remove(existingItem);

            double price;
            if (newAction.ToLower() == "buy")
            {
                price = book.IsOnDiscount ? book.DiscountedBuyPrice.Value : book.BuyPrice;
            }
            else
            {
                price = book.BorrowPrice;
            }

            var newCartItem = new ShoppingCart
            {
                Username = username,
                BookId = bookId,
                Action = newAction.ToLower(),
                Quantity = 1,
                Price = price
            };
        
            _context.ShoppingCarts.Add(newCartItem);
            await _context.SaveChangesAsync();

            var newSubtotal = await _context.ShoppingCarts
                .Where(s => s.Username == username)
                .SumAsync(s => s.Price);

            Console.WriteLine("Update successful");
            return Json(new { 
                success = true, 
                newPrice = newCartItem.Price,
                newQuantity = newCartItem.Quantity,
                newSubtotal = newSubtotal,
                newTotal = newSubtotal + 10,
                currentAction = newCartItem.Action
            });
        }
        else
        {
            Console.WriteLine("Cart item not found");
            return Json(new { success = false, message = "Cart item not found" });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in UpdateAction: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return Json(new { success = false, message = $"Error: {ex.Message}" });
    }
}
    
    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }
        var book = await _context.Books.FindAsync(request.BookId);
        
        if (book == null)
        {
            return Json(new { success = false, message = "Book not found" });
        }

        // Check borrow limit if action is borrow
        if (request.Action.ToLower() == "borrow")
        {
            var currentBorrowCount = await _context.ShoppingCarts
                .CountAsync(sc => sc.Username == username && 
                                  sc.Action.ToLower() == "borrow");

            if (currentBorrowCount >= 3)
            {
                return Json(new { 
                    success = false, 
                    message = "You can only borrow up to 3 books at a time."
                });
            }

            if (book.AvailableCopies <= 0)
            {
                return Json(new { 
                    success = false, 
                    message = "No copies available for borrowing."
                });
            }

            // Decrease available copies for borrow
            book.AvailableCopies--;
        }

        // Calculate price based on action
        double price = request.Action.ToLower() == "buy" 
            ? (book.IsOnDiscount ? book.DiscountedBuyPrice.Value : book.BuyPrice)
            : book.BorrowPrice;

        var cartItem = new ShoppingCart
        {
            Username = username,
            BookId = request.BookId,
            Action = request.Action.ToLower(),
            Quantity = request.Quantity,
            Price = price
        };

        await _context.ShoppingCarts.AddAsync(cartItem);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Book added to cart for {request.Action}" });
    }

    public class AddToCartRequest
    {
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public string Action { get; set; }
    }
    
    [HttpPost]
    public async Task<IActionResult> CheckCartStatus([FromBody] int bookId)
    {
        string username = HttpContext.Session.GetString("Username");
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        // Get the existing cart item if any
        var existingCartItem = await _context.ShoppingCarts
            .FirstOrDefaultAsync(sc => sc.Username == username && sc.BookId == bookId);

        if (existingCartItem != null)
        {
            // If it's a borrow action, block it
            if (existingCartItem.Action.ToLower() == "borrow")
            {
                return Json(new { inCart = true, message = "This book is already in your cart" });
            }
            // If it's a buy action, allow it (by saying it's not in cart)
            return Json(new { inCart = false });
        }

        return Json(new { inCart = false });
    }
    
    private async Task<string> GetPaypalAccessToken()
    {
        string accessToken = "";
        
        string url = PaypalUrl + "/v1/oauth2/token";
        using (var client = new HttpClient())
        {
            string credentials64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(PaypalClientId + ":" + PaypalSecret));
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + credentials64);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Content = new StringContent("grant_type=client_credentials", null, "application/x-www-form-urlencoded");
            var httpResponse = await client.SendAsync(requestMessage);

            if (httpResponse.IsSuccessStatusCode)
            {
                var strResponse = await httpResponse.Content.ReadAsStringAsync();
                var jsonResponse = JsonNode.Parse(strResponse);
                if (jsonResponse != null)
                {
                    accessToken = jsonResponse["access_token"]?.ToString() ?? "";
                }
            }
        }
        
        return accessToken;
    }

    [HttpPost]
    public async Task<JsonResult> CreateOrder([FromBody] JsonObject data)
    {
        try 
        {
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
            {
                return new JsonResult(new { error = "User not logged in" }) { StatusCode = 401 };
            }

            var hasItems = await _context.ShoppingCarts
                .AnyAsync(s => s.Username == username);

            if (!hasItems)
            {
                return new JsonResult(new { error = "Shopping cart is empty" }) { StatusCode = 400 };
            }
            
            var totalAmount = data?["amount"]?.ToString();
            
            // Validate amount format
            if (!decimal.TryParse(totalAmount, out decimal amount))
            {
                return new JsonResult(new { error = "Invalid amount format" }) { StatusCode = 400 };
            }

            // Ensure amount is positive and has exactly 2 decimal places
            amount = Math.Round(amount, 2);
            if (amount <= 0)
            {
                return new JsonResult(new { error = "Amount must be greater than 0" }) { StatusCode = 400 };
            }

            // Format amount with exactly 2 decimal places
            string formattedAmount = amount.ToString("0.00");

            // Create the request body
            JsonObject createOrderRequest = new JsonObject
            {
                {
                    "intent", "CAPTURE"
                },
                {
                    "purchase_units", new JsonArray
                    {
                        new JsonObject
                        {
                            {
                                "amount", new JsonObject
                                {
                                    { "currency_code", "USD" },
                                    { "value", formattedAmount }
                                }
                            }
                        }
                    }
                }
            };

            // Get access token
            string accessToken = await GetPaypalAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                return new JsonResult(new { error = "Failed to authenticate with PayPal" }) { StatusCode = 500 };
            }

            // Send request
            string url = $"{PaypalUrl}/v2/checkout/orders";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                var jsonContent = createOrderRequest.ToString();
                
                requestMessage.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var httpResponse = await client.SendAsync(requestMessage);
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                
                if (httpResponse.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonNode.Parse(responseContent);
                    string paypalOrderId = jsonResponse?["id"]?.ToString();

                    if (!string.IsNullOrEmpty(paypalOrderId))
                    {
                        return new JsonResult(new { id = paypalOrderId });
                    }
                    
                    return new JsonResult(new { error = "Invalid PayPal response" }) { StatusCode = 500 };
                }
                
                return new JsonResult(new { error = $"PayPal API error: {responseContent}" }) { StatusCode = (int)httpResponse.StatusCode };
            }
        }
        catch (Exception ex)
        {
            return new JsonResult(new { error = "Internal server error" }) { StatusCode = 500 };
        }
    }

    [HttpPost]
    public async Task<JsonResult> CompleteOrder([FromBody] JsonObject data)
    {
        var orderId = data?["orderId"]?.ToString();
        if (orderId == null)
        {
            return new JsonResult("error");
        }
        
        string accessToken = await GetPaypalAccessToken();
        
        string url = PaypalUrl + "/v2/checkout/orders/" + orderId + "/capture";

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Content = new StringContent("", Encoding.UTF8, "application/json");
            
            var httpResponse = await client.SendAsync(requestMessage);

            if (httpResponse.IsSuccessStatusCode)
            {
                var strResponse = await httpResponse.Content.ReadAsStringAsync();
                var jsonResponse = JsonNode.Parse(strResponse);

                if (jsonResponse != null)
                {
                    string paypalOrderStatus = jsonResponse["status"]?.ToString() ?? "";
                    if (paypalOrderStatus == "COMPLETED")
                    {
                        string username = HttpContext.Session.GetString("Username");
                        if (username == null)
                        {
                            return new JsonResult(new
                            {
                                status = "error",
                                message = "User not logged in."
                            });
                        }

                        int nextOrderId;
                        using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
                        {
                            await connection.OpenAsync();
                            using (var command = new OracleCommand("SELECT PERSTIN.ORDER_SEQ.NEXTVAL FROM DUAL", connection))
                            {
                                nextOrderId = Convert.ToInt32(await command.ExecuteScalarAsync());
                            }
                        }

                        var cartItems = await _context.ShoppingCarts
                            .Where(item => item.Username == username)
                            .ToListAsync();

                        using (var connection = new OracleConnection(_context.Database.GetConnectionString()))
                        {
                            await connection.OpenAsync();
                            foreach (var item in cartItems)
                            {
                                using (var command = new OracleCommand(
                                    "INSERT INTO Orders (OrderId, Username, BookId, Action, Quantity, Price, OrderDate, BorrowStartDate, BorrowEndDate, IsReturned) " +
                                    "VALUES (:OrderId, :Username, :BookId, :Action, :Quantity, :Price, :OrderDate, :BorrowStartDate, :BorrowEndDate, :IsReturned)", 
                                 connection))
                            {
                             var currentDate = DateTime.Now;

                             // Basic parameters
                             command.Parameters.Add(new OracleParameter("OrderId", nextOrderId));
                             command.Parameters.Add(new OracleParameter("Username", item.Username));
                             command.Parameters.Add(new OracleParameter("BookId", item.BookId));
                             command.Parameters.Add(new OracleParameter("Action", item.Action));
                             command.Parameters.Add(new OracleParameter("Quantity", item.Quantity));
                             command.Parameters.Add(new OracleParameter("Price", item.Price));
                             command.Parameters.Add(new OracleParameter("OrderDate", currentDate));
                            
                             if (item.Action == "Borrow")
                             {
                                 var startDateParam = new OracleParameter("BorrowStartDate", OracleDbType.Date)
                                 {
                                     Value = currentDate
                                 };
                                 command.Parameters.Add(startDateParam);

                                 var endDateParam = new OracleParameter("BorrowEndDate", OracleDbType.Date)
                                 {
                                     //Testing with Min to see if there any changes
                                     Value = currentDate.AddMinutes(3)
                                     // Value = currentDate.AddDays(30)
                                 };
                                 command.Parameters.Add(endDateParam);

                                 var isReturnedParam = new OracleParameter("IsReturned", OracleDbType.Int32)
                                 {
                                     Value = 0
                                 };
                                 command.Parameters.Add(isReturnedParam);
                             }
                             else
                             {
                                 var startDateParam = new OracleParameter("BorrowStartDate", OracleDbType.Date)
                                 {
                                     Value = DBNull.Value
                                 };
                                 command.Parameters.Add(startDateParam);

                                 var endDateParam = new OracleParameter("BorrowEndDate", OracleDbType.Date)
                                 {
                                     Value = DBNull.Value
                                 };
                                 command.Parameters.Add(endDateParam);

                                 var isReturnedParam = new OracleParameter("IsReturned", OracleDbType.Int32)
                                 {
                                     Value = DBNull.Value
                                 };
                                 command.Parameters.Add(isReturnedParam);
                             } await command.ExecuteNonQueryAsync();
                            } 
                            }
                        }
                        await UpdateWaitingList(username, cartItems);
                        _context.ShoppingCarts.RemoveRange(cartItems);
                        await _context.SaveChangesAsync();
                        
                        
                        var subject = $"Order Confirmation - Order #{nextOrderId}";
                        var messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine($"Thank you for your order #{nextOrderId}!");
                        messageBuilder.AppendLine("\nOrder Details:");

                        foreach (var item in cartItems)
                        {
                            messageBuilder.AppendLine($"\nBook ID: {item.BookId}");
                            messageBuilder.AppendLine($"Action: {item.Action}");
                            messageBuilder.AppendLine($"Quantity: {item.Quantity}");
                            messageBuilder.AppendLine($"Price: ${item.Price}");
    
                            if (item.Action == "Borrow")
                            {
                                messageBuilder.AppendLine($"Return Date: {DateTime.Now.AddDays(30):yyyy-MM-dd}");
                            }
                        }

                        var totalAmount = cartItems.Sum(item => item.Price * item.Quantity);
                        messageBuilder.AppendLine($"\nTotal Amount Inc. taxes: ${totalAmount + 10}");

                        try 
                        {
                            var emailSender = new EmailSender();
                            emailSender.SendEmail(subject, username, messageBuilder.ToString()).Wait();
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with order completion
                            Console.WriteLine($"Failed to send email: {ex.Message}");
                        }
                        
                        
                        // SAVE ORDER IN DB!!!
                        return new JsonResult(new
                        {
                            status = "success",
                            redirectUrl = Url.Action("Index", "Home")
                        });
                    }
                }
            }
        }

        return new JsonResult("error");
    }
    // In your ShoppingCartController, modify the CompleteOrder method

    private async Task UpdateWaitingList(string username, List<ShoppingCart> cartItems)
    {
        foreach (var item in cartItems)
        {
            // Find and remove any waiting list entries for books that were ordered
            var waitingListEntry = await _context.WaitingList
                .FirstOrDefaultAsync(w => w.Username == username && w.BookId == item.BookId);

            if (waitingListEntry != null)
            {
                // Get the current position before removing
                int removedPosition = waitingListEntry.Position;
            
                // Remove the entry
                _context.WaitingList.Remove(waitingListEntry);
            
                // Update positions for remaining users
                var remainingEntries = await _context.WaitingList
                    .Where(w => w.BookId == item.BookId && w.Position > removedPosition)
                    .ToListAsync();

                foreach (var entry in remainingEntries)
                {
                    entry.Position--; // Decrease position by 1
                }
            }
        }
        await _context.SaveChangesAsync();
    }
    
}






