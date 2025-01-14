using System.ComponentModel.DataAnnotations;

namespace LibraryProject.Models;

public class Contact
{
    [Key]
    public int ContactId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Message { get; set; }
    public DateTime SubmissionDate { get; set; }
}