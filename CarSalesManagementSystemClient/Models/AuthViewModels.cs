#pragma warning disable CS8618
using System.ComponentModel.DataAnnotations;

namespace CarSalesManagementSystemClient.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Họ tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận Mật khẩu")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; }
    }

    public class VerifyViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        public string Otp { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        public string Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        public string Otp { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mật khẩu mới")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
    }
}
