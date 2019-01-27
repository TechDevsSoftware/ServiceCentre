﻿using Microsoft.IdentityModel.Tokens;
using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechDevs.Shared.Models;

namespace TechDevs.Users
{
    public class AuthService<T> : IAuthService<T> where T : AuthUser, new()
    {
        private readonly IUserService<T> userService;
        private readonly IPasswordHasher passwordHasher;
        private readonly IAuthTokenService<T> tokenService;

        public AuthService(IUserService<T> userService, IPasswordHasher passwordHasher, IAuthTokenService<T> tokenService)
        {
            this.userService = userService;
            this.passwordHasher = passwordHasher;
            this.tokenService = tokenService;
        }

        public async Task<string> Login(string email, string password, string clientId)
        {
            var user = await userService.GetByEmail(email, clientId);
            var genuine = await ValidatePassword(user.EmailAddress, password, clientId);
            if (genuine) return tokenService.CreateToken(user.Id, clientId);
            return null;
        }

        public bool ValidateToken(string token, string clientKey)
        {
            if (token == null) throw new Exception("Token missing. Cannot authenticate user");
            if (clientKey == null) throw new Exception("ClientKey missing. Cannot autenticate user");

            var handler = new JwtSecurityTokenHandler();

            var validationParams = new TokenValidationParameters()
            {
                ValidateLifetime = false, // Because there is no expiration in the generated token
                ValidateAudience = false, // Because there is no audiance in the generated token
                ValidateIssuer = false,   // Because there is no issuer in the generated token
                RequireExpirationTime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKeyTechDevsKey")) // The same key as the one that generate the token
            };
            try
            {
                var result = handler.ValidateToken(token, validationParams, out var jwtToken);
                var tokenClientKey = result.Claims.FirstOrDefault(c => c.Type == "Gibson-ClientKey")?.Value;
                bool isValidClientKey = (tokenClientKey == clientKey);
                bool isExpired = (jwtToken.ValidTo < DateTime.UtcNow);
                if (isExpired) throw new Exception("Token has expired");
                return (isValidClientKey && !isExpired);
            }
            catch (Exception)
            {
                return false;
            }
        }

      

        public async Task<bool> ValidatePassword(string email, string password, string clientId)
        {
            try
            {
                var user = await userService.GetByEmail(email, clientId);
                if (user == null) return false;
                var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

    }

}