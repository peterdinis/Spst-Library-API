namespace AdminService.Dtos
{
    public class GenerateAdminCodeDto
    {
        public int? ExpiryHours { get; set; }
    }

    public class ValidateAdminCodeDto
    {
        public string AdminCode { get; set; } = string.Empty;
        public bool ClearAfterValidation { get; set; } = true;
    }
}