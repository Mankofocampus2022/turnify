namespace Turnify.Api.Models.DTOs
{
    // Este arregla el error de la línea 62
    public class ForgotPasswordDto
    {
        public string Email { get; set; } = string.Empty;
    }

    // Este arregla el error de la línea 79
    public class ResetPasswordDto
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}