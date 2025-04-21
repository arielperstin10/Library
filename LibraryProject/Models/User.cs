using System.Collections;
using System.ComponentModel.DataAnnotations;
using LibraryProject.Data.Enums;
namespace LibraryProject.Models;

public class User
{
    [Key]
    [Required]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "Username must be between 5 and 20 characters long.")]
    [RegularExpression(@"^[A-Za-z0-9]{5,}$", 
        ErrorMessage = "Username must only contain letters or numbers.")]
    public string Username { get; set; }
    [Required]
    [StringLength(50, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 50 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#\$%\^&\*])[A-Za-z\d!@#\$%\^&\*]{8,}$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one number, one special character (!,@,#, etc.), and lowercase letters.")]
    public string Password { get; set; }
    //[Required(ErrorMessage = "Invalid email address.")] 
    //[RegularExpression(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", ErrorMessage = "Invalid email address.")]
    public string Email { get; set; }
    [Required]
    [StringLength(20, ErrorMessage = "First name must be up to 20 characters long.")]
    [RegularExpression(@"^[A-Za-z]+$", ErrorMessage = "First name must contain only letters.")]
    public string FirstName { get; set; }
    [Required]
    [StringLength(20, ErrorMessage = "Last name must be up to 20 characters long.")]
    [RegularExpression(@"^[A-Za-z]+$", ErrorMessage = "Last name must contain only letters.")]
    public string LastName { get; set; }
    [Required(ErrorMessage = "Please select your gender.")]
    public Gender Gender { get; set; }
    [Required]
    public UserRole Role { get; set; }
    public int MaxBorrowed { get; set; }
    public int IsPasswordChanged { get; set; }
    
    public int id { get; set; }
    
    public string cardNumber { get; set; }
    
    public DateTime validDate { get; set; }
    
    public string cvc { get; set; }
    
}