namespace LoanSystemAPI.DTOs
{
    // THE ACCESS TOKEN GOES IN THE Authorization HEADER; THE REFRESH TOKEN IS ONLY SENT TO api/auth/refresh AND api/auth/logout
    public class AuthTokensDTO
    {
        public string AccessToken { get; set; } = "";
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; } = "";
        public DateTime RefreshTokenExpiresAt { get; set; }
    }
}
