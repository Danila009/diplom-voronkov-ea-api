using System.ComponentModel.DataAnnotations;

namespace Diploma.model.user
{
    public class UserDto
    {
        [Key] public int Id { get; set; }
        [Required] public string Login { get; set; } = string.Empty;
        [Required] public string FirstName { get; set; } = string.Empty;
        [Required] public string LastName { get; set; } = string.Empty;
        [Required] public string MidleName { get; set; } = string.Empty;
        [Required] public string Police { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Photo { get; set; } = null;
        public Doctor? Doctor { get; set; } = null;
        public Admin? Admin { get; set; } = null;
    }
}
